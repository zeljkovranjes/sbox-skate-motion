namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;
using Vector4 = System.Numerics.Vector4;
using A = PhysicalCadenceArithmetic;
using C = SkateMotion.Core.Collision.CollisionArithmetic;

internal static class PhysicalLocomotionMotionPreparation
{
	internal static PhysicalLocomotionMotionState Update( in PhysicalLocomotionMotionState state,
		in PhysicalLocomotionMotionInput input )
	{
		var first = Normalize( Cross( input.Up, input.Forward ) );
		var third = Normalize( Cross( first, input.OrientationAxis ) );
		third = Normalize( third );
		var second = Normalize( A.Reject( input.Up, third ) );
		var correctedFirst = A.Reject( first, third );
		var projection = A.Dot( second, first );
		first = Normalize( new(
			A.NegativeMultiplySubtract( second.X, projection, correctedFirst.X ),
			A.NegativeMultiplySubtract( second.Y, projection, correctedFirst.Y ),
			A.NegativeMultiplySubtract( second.Z, projection, correctedFirst.Z ) ) );

		var delta = A.Subtract( input.CurrentPosition, state.Position );
		var change = new Vector3( MultiplySubtract( delta.X, 60f, state.Motion.X ),
			MultiplySubtract( delta.Y, 60f, state.Motion.Y ), MultiplySubtract( delta.Z, 60f, state.Motion.Z ) );
		if ( A.Negative( A.Add( change.Y, 1f ) ) )
			change.Y = -1f;
		var motion = new Vector3( A.Add( state.Motion.X, change.X ),
			A.Add( state.Motion.Y, change.Y ), A.Add( state.Motion.Z, change.Z ) );
		var tick = A.Value( 0x3c888888 );
		var position = new Vector3( A.MultiplyAdd( motion.X, tick, state.Position.X ),
			A.MultiplyAdd( motion.Y, tick, state.Position.Y ), A.MultiplyAdd( motion.Z, tick, state.Position.Z ) );
		return new( first, second, third, position, motion );
	}

	/// <summary>Connects solved movement to retained attributes; contact and reference preparation stay explicit.</summary>
	internal static bool TryUpdateCadence( in PhysicalLocomotionMotionState motion,
		ref PhysicalLocomotionCadenceState cadence, in PhysicalLocomotionCadenceInput physicalInput,
		Vector4 thresholds, PhysicalAnimationAttributeState attributes, PhysicalLocomotionCadenceContactInput? contacts )
	{
		var input = physicalInput with { Motion = motion.Motion, Up = motion.FrameRow1 };
		return PhysicalLocomotionCadence.TryUpdateFromAttributes( ref cadence, input, thresholds, attributes, contacts );
	}

	private static Vector3 Cross( Vector3 left, Vector3 right ) => new(
		A.NegativeMultiplySubtract( right.Y, left.Z, A.Multiply( left.Y, right.Z ) ),
		A.NegativeMultiplySubtract( right.Z, left.X, A.Multiply( left.Z, right.X ) ),
		A.NegativeMultiplySubtract( right.X, left.Y, A.Multiply( left.X, right.Y ) ) );

	private static Vector3 Normalize( Vector3 value )
	{
		var lengthSquared = A.Dot( value, value );
		var inverse = C.Value( C.Interpolate( C.Word( lengthSquared ), C.RootEstimate( C.Word( lengthSquared ) ) ) );
		for ( var index = 0; index < 2; index++ )
		{
			var squared = A.Multiply( inverse, inverse );
			var half = A.Multiply( inverse, .5f );
			inverse = A.MultiplyAdd( half, A.NegativeMultiplySubtract( lengthSquared, squared, 1f ), inverse );
		}
		return new( A.Multiply( value.X, inverse ), A.Multiply( value.Y, inverse ), A.Multiply( value.Z, inverse ) );
	}

	private static float MultiplySubtract( float left, float right, float subtract )
		=> C.Value( C.MultiplyAdd( C.Word( left ), C.Word( right ), C.Word( subtract ) ^ 0x80000000u ) );
}
