namespace SkateMotion.Core.Animation;

using Matrix4x4 = System.Numerics.Matrix4x4;
using Vector3 = System.Numerics.Vector3;
using Vector4 = System.Numerics.Vector4;
using A = PhysicalCadenceArithmetic;

internal static class PhysicalLocomotionContactReference
{
	/// <summary>Updates linear contact history from actual consecutive rigid contact frames.</summary>
	internal static PhysicalLocomotionContactReferenceState Update(
		in PhysicalLocomotionContactReferenceState state, Matrix4x4 contactFrame,
		uint contactIdentity, bool hasContact, Vector3 physicalPosition )
	{
		if ( !hasContact )
			return state;
		if ( contactIdentity != state.Identity )
			return state with { Frame = contactFrame, Identity = contactIdentity,
				Velocity = Vector3.Zero, Reference = Vector3.Zero, Acceleration = Vector3.Zero };

		var old = state.Frame;
		var inverse0 = new Vector3( old.M11, old.M21, old.M31 );
		var inverse1 = new Vector3( old.M12, old.M22, old.M32 );
		var inverse2 = new Vector3( old.M13, old.M23, old.M33 );
		var current0 = new Vector3( contactFrame.M11, contactFrame.M12, contactFrame.M13 );
		var current1 = new Vector3( contactFrame.M21, contactFrame.M22, contactFrame.M23 );
		var current2 = new Vector3( contactFrame.M31, contactFrame.M32, contactFrame.M33 );
		var delta0 = Combine( current0, current1, current2, inverse0 );
		var delta1 = Combine( current0, current1, current2, inverse1 );
		var delta2 = Combine( current0, current1, current2, inverse2 );
		var inversePosition = AddScaled( inverse0, A.Subtract( 0f, old.M41 ),
			AddScaled( inverse1, A.Subtract( 0f, old.M42 ), Scale( inverse2, A.Subtract( 0f, old.M43 ) ) ) );
		var offset = AddScaled( current0, inversePosition.X, new( contactFrame.M41, contactFrame.M42, contactFrame.M43 ) );
		offset = AddScaled( current1, inversePosition.Y, offset );
		offset = AddScaled( current2, inversePosition.Z, offset );
		var carried = AddScaled( delta0, physicalPosition.X, offset );
		carried = AddScaled( delta1, physicalPosition.Y, carried );
		carried = AddScaled( delta2, physicalPosition.Z, carried );
		var inverseTick = A.Value( 0x42700002 );
		var velocity = Scale( A.Subtract( carried, physicalPosition ), inverseTick );
		var acceleration = Scale( AddScaled( A.Subtract( velocity, state.Velocity ), inverseTick,
			state.Acceleration ), A.Value( 0x3f733333 ) );
		var reference = Add( velocity, A.Subtract( velocity, state.PreviousVelocity ) );
		return new( contactFrame, state.Identity, velocity, reference, velocity, acceleration );
	}

	/// <summary>Uses retained contact motion with the solved walking output and authored animation attributes.</summary>
	internal static bool TryUpdateCadence( in PhysicalLocomotionContactReferenceState contact,
		in PhysicalLocomotionMotionState motion, ref PhysicalLocomotionCadenceState cadence,
		in PhysicalLocomotionCadenceInput physicalInput, Vector4 thresholds,
		PhysicalAnimationAttributeState attributes, PhysicalLocomotionCadenceContactInput? observation )
	{
		var input = physicalInput with { MotionReference = contact.Reference };
		return PhysicalLocomotionMotionPreparation.TryUpdateCadence( motion, ref cadence, input,
			thresholds, attributes, observation );
	}

	private static Vector3 Combine( Vector3 first, Vector3 second, Vector3 third, Vector3 factors )
		=> AddScaled( third, factors.Z, AddScaled( second, factors.Y, Scale( first, factors.X ) ) );

	private static Vector3 Scale( Vector3 value, float factor )
		=> new( A.Multiply( value.X, factor ), A.Multiply( value.Y, factor ), A.Multiply( value.Z, factor ) );

	private static Vector3 Add( Vector3 first, Vector3 second )
		=> new( A.Add( first.X, second.X ), A.Add( first.Y, second.Y ), A.Add( first.Z, second.Z ) );

	private static Vector3 AddScaled( Vector3 value, float factor, Vector3 add )
		=> new( A.MultiplyAdd( value.X, factor, add.X ), A.MultiplyAdd( value.Y, factor, add.Y ),
			A.MultiplyAdd( value.Z, factor, add.Z ) );
}
