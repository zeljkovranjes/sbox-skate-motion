namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;
using Vector4 = System.Numerics.Vector4;
using static LimbSolverMath;

/// <summary>Produces weighted contact directions, impact channels and retained rig feedback.</summary>
internal static class PhysicalContactFeedback
{
	private static ReadOnlySpan<int> BoneGroups => [-1, 0, 1, 2, 2, 2, 1, 3, 3, 3, 1, 1, 1, 1, 1, 6, 6, 4, 4, 7, 7, 5, 5, 1];

	internal static void Update( PhysicalContactFeedbackState state, PhysicalPoseResetState readback,
		LimbRigidTransform[] bodyPoses, float[] bodyWeights, PhysicalContactFeedbackSettings settings,
		PhysicalContactFeedbackInput input, PhysicalRiderContact[] contacts )
	{
		UpdateSpan( state, readback, bodyPoses, bodyWeights, settings, input, contacts );
	}

	internal static void UpdateSpan( PhysicalContactFeedbackState state, PhysicalPoseResetState readback,
		LimbRigidTransform[] bodyPoses, float[] bodyWeights, PhysicalContactFeedbackSettings settings,
		PhysicalContactFeedbackInput input, ReadOnlySpan<PhysicalRiderContact> contacts )
	{
		if ( readback.Poses.Length != 26 || bodyPoses.Length < 24 || bodyWeights.Length != 24 )
			throw new ArgumentException( "Contact feedback requires 24 canonical bones and the 26-body readback." );
		if ( !float.IsFinite( input.DeltaTime ) || input.DeltaTime <= 0f )
			throw new ArgumentOutOfRangeException( nameof( input ) );
		foreach ( var contact in contacts )
			if ( contact.Bone < 0 || contact.Bone >= 24 )
				throw new ArgumentOutOfRangeException( nameof( contacts ) );
		foreach ( var pin in state.Pins )
			if ( pin.Bone < 0 || pin.Bone >= 24 )
				throw new ArgumentOutOfRangeException( nameof( state ) );

		Begin( state, readback, input, settings.Duration );
		var squared = Dot( input.Velocity, input.Velocity );
		var speed = SquareRoot( squared );
		var direction = speed > 9.999999974752427e-7f
			? Scale( input.Velocity, InverseSquareRoot( squared ) ) : Vector3.Zero;
		foreach ( var contact in contacts )
			Accumulate( state, readback, bodyWeights, settings, input, contact, direction, speed );

		var feedback = state.Feedback;
		feedback.ControlFlags[ input.SecondaryMode ? 8 : 7 ] = Flag( input.SecondaryMode
			? PointPinch( feedback ) : GroupPinch( feedback, bodyPoses ) );
		feedback.Blend = settings.Duration == 0f ? 1f
			: 1f - Math.Clamp( feedback.ControlScalars[ 0 ] / settings.Duration, 0f, 1f );
		feedback.ControlFlags[ 3 ] = Flag( settings.Duration != 0f && feedback.Blend < 1f );
	}

	private static void Begin( PhysicalContactFeedbackState state, PhysicalPoseResetState readback,
		PhysicalContactFeedbackInput input, float duration )
	{
		var feedback = state.Feedback;
		var scalars = feedback.ControlScalars;
		scalars[ 0 ] -= input.DeltaTime;
		scalars[ 5 ] += input.DeltaTime;
		scalars[ 2 ] = scalars[ 6 ] = scalars[ 7 ] = scalars[ 8 ] = scalars[ 9 ] = 0f;
		feedback.Counter = 0;
		feedback.SelectedIndex = -1;
		Array.Clear( feedback.ControlFlags );
		feedback.ControlFlags[ 6 ] = Flag( input.SecondaryMode );
		state.AnyContact = false;
		for ( var bone = 0; bone < 24; bone++ )
			feedback.BoneWeights[ bone ] -= input.DeltaTime;
		Array.Clear( feedback.BoneFlags );
		Array.Clear( feedback.BoneVectors );
		Array.Clear( feedback.BoneScalars );
		Array.Clear( feedback.BoneValues );
		Array.Clear( feedback.BoneControlFlags );
		Array.Clear( feedback.GroupScalars );
		Array.Clear( feedback.GroupVectors );
		Array.Fill( feedback.GroupIndices, -1 );
		// Group material values survive when no stronger contact replaces their group.
		feedback.PrimaryReference = -Vector4.UnitY;
		feedback.ReferencePosition = Vector4.Zero;
		feedback.FirstDelta = feedback.SecondDelta = -Vector3.UnitY;
		if ( !input.SecondaryMode )
			scalars[ 3 ] = scalars[ 4 ] = scalars[ 5 ] = 0f;
		foreach ( var pin in state.Pins )
		{
			pin.RetainedNear &= input.SecondaryMode;
			pin.Near = false;
			pin.WorldPosition = readback.Poses[ pin.Bone ].Transform( pin.LocalPosition );
		}
		if ( input.ResetTimer )
			scalars[ 0 ] = duration;
	}

	private static void Accumulate( PhysicalContactFeedbackState state, PhysicalPoseResetState readback,
		float[] weights, PhysicalContactFeedbackSettings settings, PhysicalContactFeedbackInput input,
		PhysicalRiderContact contact, Vector3 direction, float referenceSpeed )
	{
		var feedback = state.Feedback;
		var flags = feedback.ControlFlags;
		var kind = (contact.SurfaceFlags >> 7) & 31;
		state.AnyContact = true;
		if ( contact.OtherKind == 4 && contact.Bone <= 20 && ((1 << contact.Bone) & 0x198000) != 0 )
			flags[ 10 ] = 1;
		if ( kind == 6 )
			flags[ 0 ] = 1;
		if ( kind == 12 )
		{
			flags[ 13 ] = 1;
			feedback.ControlScalars[ 9 ] = contact.Point.Y;
		}
		if ( !Accepted( input, settings, contact ) )
			return;
		References( feedback, input.SecondaryMode, contact, kind );
		var pinned = false;
		foreach ( var pin in state.Pins )
		{
			var offset = contact.Point - pin.WorldPosition;
			if ( pin.Bone != contact.Bone || Dot( offset, offset ) >= pin.RadiusSquared )
				continue;
			pin.Near = pin.RetainedNear = true;
			pinned = true;
		}
		// Capacity is tested before priority: even a later stronger weight cannot
		// displace a full selection during this update.
		if ( feedback.Counter >= 20 )
			return;
		Select( state, contact );
		var firstMass = Mass( contact.FirstBody );
		var secondMass = Mass( contact.SecondBody );
		var mass = contact.SelectSecondBody ? secondMass : firstMass;
		var light = mass > 9.999999747378752e-5f && mass < settings.LightMassLimit;
		var impulseScale = ((firstMass + secondMass) / (firstMass * secondMass)) * .00027777781360782683f;
		var impulse = Scale( contact.Impulse, impulseScale );
		var impulseSpeed = SquareRoot( Dot( impulse, impulse ) );
		var reference = ReferenceVelocity( input, readback.Deltas[ contact.Bone ], direction, referenceSpeed );
		var relative = (contact.SelectSecondBody ? contact.SecondBody.Velocity : contact.FirstBody.Velocity) - reference;
		var speed = MathF.Abs( Dot( relative, contact.Normal ) );
		if ( light )
			speed *= .5f;
		speed = Classify( feedback, settings, input, contact, speed );
		if ( contact.Bone > 0 )
		{
			Impact( feedback, contact, pinned, relative, speed, weights[ contact.Bone ] * impulseSpeed );
			feedback.ControlScalars[ 0 ] = settings.Duration;
		}
		if ( feedback.AuthoredFlags[ contact.Bone ] == 0 || light )
		{
			flags[ 1 ] = 1;
			flags[ 9 ] = Flag( flags[ 2 ] != 0 || input.SpecialState );
		}
		else
		{
			flags[ 2 ] = 1;
			flags[ 9 ] = 1;
		}
	}

	private static bool Accepted( PhysicalContactFeedbackInput input, PhysicalContactFeedbackSettings settings,
		PhysicalRiderContact contact )
	{
		if ( input.SecondaryMode || (input.ActorGate && !input.Mode600) )
			return true;
		var height = Dot( input.Up, contact.Point - input.Position );
		var threshold = input.SpecialState ? .30000001192092896f : settings.MinimumHeight;
		return height >= threshold || (!input.SpecialState
			&& MathF.Abs( Dot( contact.Normal, input.Up ) ) <= settings.MaximumAlignment);
	}

	private static void References( PhysicalRigFeedbackState feedback, bool secondary,
		PhysicalRiderContact contact, int kind )
	{
		var normal = new Vector4( contact.Normal, 0f );
		if ( normal.Y > feedback.PrimaryReference.Y )
			feedback.PrimaryReference = normal;
		if ( !secondary )
		{
			if ( contact.Bone is 15 or 19 && normal.Y > feedback.ReferencePosition.Y )
				feedback.ReferencePosition = normal;
		}
		else if ( kind == 10 && normal.Y > feedback.FirstDelta.Y )
		{
			feedback.FirstDelta = contact.Normal;
			feedback.ControlFlags[ 11 ] = 1;
		}
		else if ( kind == 11 && normal.Y > feedback.SecondDelta.Y )
		{
			feedback.SecondDelta = contact.Normal;
			feedback.ControlFlags[ 12 ] = 1;
		}
	}

	private static void Select( PhysicalContactFeedbackState state, PhysicalRiderContact contact )
	{
		var feedback = state.Feedback;
		var weight = feedback.AuthoredWeights[ contact.Bone ];
		feedback.BoneWeights[ contact.Bone ] = 0f;
		feedback.BoneFlags[ contact.Bone ] = 1;
		if ( weight > feedback.ControlScalars[ 2 ] )
		{
			feedback.ControlScalars[ 2 ] = weight;
			feedback.Counter = 0;
		}
		if ( weight != feedback.ControlScalars[ 2 ] )
			return;
		state.Normals[ feedback.Counter ] = contact.Normal;
		state.Bones[ feedback.Counter++ ] = contact.Bone;
	}

	private static float Mass( PhysicalContactBody body ) => body.Fixed ? 999.9999389648438f : 1f / body.InverseMass;

	private static Vector3 ReferenceVelocity( PhysicalContactFeedbackInput input, Vector3 boneVelocity,
		Vector3 direction, float referenceSpeed )
	{
		if ( input.SecondaryMode )
			return Scale( input.PreviousVelocity + boneVelocity, .5f );
		if ( input.SpecialState || input.AlternateReference )
			return new Vector3( input.PreviousVelocity.X, 0f, input.PreviousVelocity.Z );
		var projected = Scale( direction, Dot( boneVelocity, direction ) );
		var length = SquareRoot( Dot( projected, projected ) );
		if ( length < 1.52587890625e-5f )
			return projected;
		return Scale( Scale( projected, MathF.Min( length, referenceSpeed ) ), Reciprocal( length ) );
	}

	private static float Classify( PhysicalRigFeedbackState feedback, PhysicalContactFeedbackSettings settings,
		PhysicalContactFeedbackInput input, PhysicalRiderContact contact, float speed )
	{
		if ( contact.OtherKind == 8 )
		{
			feedback.ControlFlags[ 4 ] = 1;
			feedback.ControlScalars[ 7 ] = MathF.Max( feedback.ControlScalars[ 7 ], speed );
		}
		else if ( contact.OtherKind == 11 )
			feedback.ControlScalars[ 8 ] = MathF.Max( feedback.ControlScalars[ 8 ], speed );
		else if ( contact.OtherKind == 5 )
		{
			speed *= input.AlternateObjectScale ? settings.AlternateObjectSpeedScale : settings.ObjectSpeedScale;
			feedback.ControlScalars[ 6 ] = MathF.Max( feedback.ControlScalars[ 6 ], speed );
			if ( contact.OtherIdentifier.HasValue )
				feedback.SelectedIndex = contact.OtherIdentifier.Value;
		}
		if ( contact.OtherKind != 4 )
			feedback.ControlFlags[ 5 ] = 1;
		return speed;
	}

	private static void Impact( PhysicalRigFeedbackState feedback, PhysicalRiderContact contact, bool pinned,
		Vector3 relative, float speed, float weightedImpulse )
	{
		var bone = contact.Bone;
		var group = BoneGroups[ bone ];
		var channel = bone * 2 + (pinned ? 1 : 0);
		var boneStronger = speed > feedback.BoneScalars[ channel ];
		var groupStronger = speed > feedback.GroupScalars[ group * 3 ];
		if ( !boneStronger && !groupStronger )
			return;
		var cross = Cross( relative, contact.Normal );
		if ( boneStronger )
		{
			feedback.BoneScalars[ channel ] = speed;
			feedback.BoneValues[ channel ] = contact.SurfaceFlags;
			feedback.BoneVectors[ bone * 5 + (pinned ? 1 : 0) ] = new Vector4( contact.Normal, 0f );
			feedback.BoneVectors[ bone * 5 + (pinned ? 3 : 2) ] = new Vector4( cross, 0f );
			if ( !pinned )
				feedback.BoneVectors[ bone * 5 + 4 ] = new Vector4( contact.Point, 0f );
			var category = contact.OtherKind switch { 8 => 0, 11 => 1, 5 => 2, _ => 3 };
			feedback.BoneControlFlags[ bone * 4 + category ] = 1;
		}
		if ( !groupStronger )
			return;
		feedback.GroupScalars[ group * 3 ] = speed;
		feedback.GroupScalars[ group * 3 + 1 ] = weightedImpulse;
		feedback.GroupScalars[ group * 3 + 2 ] = SquareRoot( Dot( cross, cross ) );
		feedback.GroupVectors[ group ] = new Vector4( contact.Normal, 0f );
		feedback.GroupValues[ group ] = contact.SurfaceFlags & 127;
		feedback.GroupIndices[ group ] = bone;
	}

	private static bool GroupPinch( PhysicalRigFeedbackState feedback, LimbRigidTransform[] poses )
	{
		for ( var first = 0; first < 8; first++ )
		{
			var firstBone = feedback.GroupIndices[ first ];
			if ( feedback.GroupScalars[ first * 3 ] <= 0f || feedback.AuthoredFlags[ firstBone ] == 0 )
				continue;
			var firstNormal = Spatial( feedback.GroupVectors[ first ] );
			for ( var second = 0; second < 8; second++ )
			{
				var secondBone = feedback.GroupIndices[ second ];
				if ( second == first || feedback.GroupScalars[ second * 3 ] <= 0f || feedback.AuthoredFlags[ secondBone ] == 0 )
					continue;
				if ( Dot( firstNormal, Spatial( feedback.GroupVectors[ second ] ) ) < -.699999988079071f
					&& Dot( poses[ secondBone ].Translation - poses[ firstBone ].Translation, firstNormal ) < 0f )
					return true;
			}
		}
		return false;
	}

	private static bool PointPinch( PhysicalRigFeedbackState feedback )
	{
		for ( var first = 1; first < 23; first++ )
		{
			if ( feedback.BoneScalars[ first * 2 ] <= 0f )
				continue;
			var normal = Spatial( feedback.BoneVectors[ first * 5 ] );
			for ( var second = first + 1; second < 24; second++ )
			{
				if ( feedback.BoneScalars[ second * 2 ] <= 0f )
					continue;
				if ( Dot( normal, Spatial( feedback.BoneVectors[ second * 5 ] ) ) < -.9900000095367432f
					&& MathF.Abs( Dot( normal, Spatial( feedback.BoneVectors[ first * 5 + 4 ] )
						- Spatial( feedback.BoneVectors[ second * 5 + 4 ] ) ) ) < .029999999329447746f )
					return true;
			}
		}
		return false;
	}

	private static Vector3 Spatial( Vector4 value ) => new( value.X, value.Y, value.Z );
	private static byte Flag( bool value ) => (byte)(value ? 1 : 0);
}
