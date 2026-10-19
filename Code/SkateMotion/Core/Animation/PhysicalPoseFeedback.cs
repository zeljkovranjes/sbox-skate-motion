namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;
using static LimbSolverMath;

/// <summary>Updates animation errors and selects direct or contact-filtered physical feedback.</summary>
internal sealed class PhysicalPoseFeedback
{
	private readonly LimbSolverMath _math;
	private readonly LimbSignedAngle _angles;
	private const float RightAngle = 1.5707963705062866f;
	private const float MinimumLength = 9.999999974752427e-7f;

	internal PhysicalPoseFeedback( LimbSolverMath math )
	{
		ArgumentNullException.ThrowIfNull( math );
		_math = math;
		_angles = new( math, 9.999999747378752e-5f, 6.2831854820251465f, .15915493667125702f );
	}

	internal void Update( PhysicalPoseFeedbackState state, PhysicalPoseResetState readback,
		PhysicalPosePreparationState preparation, LimbRigidTransform[] mutablePose,
		PhysicalPoseFeedbackInput input, Vector3[] contacts )
	{
		ArgumentNullException.ThrowIfNull( contacts );
		UpdateCore( state, readback, preparation, mutablePose, input, contacts );
	}

	internal void UpdateFromContacts( PhysicalPoseFeedbackState state, PhysicalPoseResetState readback,
		PhysicalPosePreparationState preparation, LimbRigidTransform[] mutablePose,
		PhysicalPoseFeedbackInput input, PhysicalContactFeedbackState contacts )
	{
		ArgumentNullException.ThrowIfNull( contacts );
		UpdateCore( state, readback, preparation, mutablePose, input,
			contacts.Normals.AsSpan( 0, contacts.Feedback.Counter ) );
	}

	private void UpdateCore( PhysicalPoseFeedbackState state, PhysicalPoseResetState readback,
		PhysicalPosePreparationState preparation, LimbRigidTransform[] mutablePose,
		PhysicalPoseFeedbackInput input, ReadOnlySpan<Vector3> contacts )
	{
		ArgumentNullException.ThrowIfNull( state );
		if ( !float.IsFinite( input.DeltaTime ) || input.DeltaTime <= 0f )
			throw new ArgumentOutOfRangeException( nameof( input ) );
		PhysicalPoseReadback.UpdateErrors( readback, preparation, input.ModelToWorld, mutablePose );
		state.PreviousPosition = state.Position;
		state.Position = readback.WeightedPhysicalPosition;
		state.Velocity = Scale( state.Position - state.PreviousPosition, Reciprocal( input.DeltaTime ) );
		// History reset follows velocity sampling; it does not discard the current velocity.
		if ( input.ResetHistory )
			state.PreviousPosition = state.Position;

		var errors = readback.AnimationErrors;
		var largest = errors[ 17 ];
		foreach ( var bone in MagnitudeOrder )
			largest = Larger( largest, errors[ bone ] );
		state.MaximumError = SquareRoot( Dot( largest, largest ) );
		if ( input.DirectFeedback )
		{
			state.FirstFeedback = readback.FirstFeedbackError;
			state.SecondFeedback = readback.SecondFeedbackError;
			state.Correction = state.FirstFeedback;
			return;
		}

		state.FirstFeedback = Vector3.Zero;
		state.SecondFeedback = Vector3.Zero;
		var selected = Filter( errors[ 23 ], input.ContactNormal, contacts );
		foreach ( var bone in FilterOrder )
			selected = Larger( selected, Filter( errors[ bone ], input.ContactNormal, contacts ) );
		state.Correction = selected;
	}

	private static ReadOnlySpan<int> MagnitudeOrder => [23, 18, 16, 21, 22, 20, 1];
	private static ReadOnlySpan<int> FilterOrder => [17, 18, 16, 21, 22, 20, 1];

	private static Vector3 Larger( Vector3 current, Vector3 candidate ) =>
		Dot( candidate, candidate ) > Dot( current, current ) ? candidate : current;

	internal Vector3 Filter( Vector3 error, Vector3 normal, ReadOnlySpan<Vector3> contacts )
	{
		var projected = error - Scale( normal, Dot( error, normal ) );
		var direction = Direction( projected );
		if ( Dot( direction, direction ) < .1f )
			return projected;

		var upper = RightAngle;
		var lower = -RightAngle;
		var accepted = false;
		foreach ( var contact in contacts )
		{
			var tangent = Direction( contact - Scale( normal, Dot( contact, normal ) ) );
			if ( Dot( tangent, tangent ) < .1f )
				continue;
			var angle = _angles.WrappedFraction( direction, tangent, normal );
			if ( angle <= -RightAngle || angle >= RightAngle )
				continue;
			upper = MathF.Min( upper, angle );
			lower = MathF.Max( lower, angle );
			accepted = true;
		}
		var middle = (upper + lower) * .5f;
		var scale = accepted ? _math.SinCos( middle ).Cosine : 0f;
		var rotation = _math.AxisRotation( normal, middle );
		return Scale( rotation.Transform( projected, Vector3.Zero ), scale );
	}

	private static Vector3 Direction( Vector3 value )
	{
		var squared = Dot( value, value );
		return SquareRoot( squared ) > MinimumLength ? Scale( value, InverseSquareRoot( squared ) ) : Vector3.Zero;
	}
}
