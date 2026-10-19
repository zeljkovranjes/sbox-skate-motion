namespace SkateMotion.Core.Animation;

using Matrix4x4 = System.Numerics.Matrix4x4;
using Vector3 = System.Numerics.Vector3;
using Vector4 = System.Numerics.Vector4;
using A = PhysicalCadenceArithmetic;
using O = PhysicalLocomotionOwnerArithmetic;

internal static partial class PhysicalLocomotionOwner
{
	/// <summary>Prepares the ordinary ground phase; unsupported target/rejection paths leave state unchanged.</summary>
	internal static bool TryPrepare( PhysicalLocomotionOwnerState state, in PhysicalLocomotionOwnerInput input )
	{
		ArgumentNullException.ThrowIfNull( state );
		if ( input.TargetMode || (input.ContactFlags & 2u) != 0 || state.RejectionActive )
			return false;
		var hasContact = (input.ContactFlags & 1u) != 0;
		if ( hasContact )
			UpdateContact( state, input );
		var frame = state.Frame;
		var position = Position( frame );
		var oldUp = Row1( frame );
		var forward = Row2( frame );
		var tick = A.Value( 0x3c888888 );
		var displacement = O.Scale( state.Movement, tick );
		state.Timers = new( -1f, state.Timers.Y, state.Timers.Z, state.Timers.W );
		if ( input.ReplaceTarget )
		{
			var target = input.TargetFrame;
			var targetPosition = O.AddScaled( Row0( target ), A.Value( 0x3e4ccccc ), Position( target ) );
			target.Translation = targetPosition;
			state.TargetFrame = target;
		}
		var correction = hasContact
			? O.Scale( oldUp, A.Multiply( A.Dot( A.Subtract( input.Position, position ), oldUp ), A.Value( 0x3d4ccccc ) ) )
			: Vector3.Zero;
		displacement = O.Add( displacement, correction );
		displacement = O.Add( displacement, state.AdditionalMovement );
		displacement = O.AddScaled( state.AccelerationMovement, tick, displacement );
		displacement = O.AddScaled( state.Contact.Reference, tick, displacement );
		var angle = A.Multiply( A.Add( state.Heading.X, state.AngularHistory.Z ), tick );
		var rotated = Rotate( forward, oldUp, angle );
		var first = O.Normalize( O.Cross( state.SmoothedUp, rotated ) );
		var third = O.Normalize( O.Cross( first, state.SmoothedUp ) );
		third = O.Normalize( third );
		var second = O.Normalize( A.Reject( state.SmoothedUp, third ) );
		var corrected = A.Reject( first, third );
		var projection = A.Dot( second, first );
		first = O.Normalize( new( A.NegativeMultiplySubtract( second.X, projection, corrected.X ),
			A.NegativeMultiplySubtract( second.Y, projection, corrected.Y ),
			A.NegativeMultiplySubtract( second.Z, projection, corrected.Z ) ) );
		state.Frame = Frame( first, second, third, O.Add( position, displacement ) );
		UpdateSecondary( state, displacement );
		return true;
	}

	/// <summary>Connects prepared owner placement to the solved motion producer with explicit physical axes.</summary>
	internal static bool TryPrepareAndSolve( PhysicalLocomotionOwnerState state,
		in PhysicalLocomotionOwnerInput input, in PhysicalLocomotionMotionInput physical )
	{
		if ( !TryPrepare( state, input ) )
			return false;
		var prepared = physical with { CurrentPosition = Position( state.Frame ), Forward = Row2( state.Frame ) };
		state.Solved = PhysicalLocomotionMotionPreparation.Update( state.Solved, prepared );
		return true;
	}

	internal static bool TryUpdateCadence( PhysicalLocomotionOwnerState state,
		ref PhysicalLocomotionCadenceState cadence, in PhysicalLocomotionCadenceInput input,
		Vector4 thresholds, PhysicalAnimationAttributeState attributes, PhysicalLocomotionCadenceContactInput? contact )
	{
		ArgumentNullException.ThrowIfNull( state );
		var prepared = input with { FrameRow0 = Row0( state.Frame ), FrameRow1 = Row1( state.Frame ), FrameRow2 = Row2( state.Frame ) };
		return PhysicalLocomotionContactReference.TryUpdateCadence( state.Contact, state.Solved, ref cadence,
			prepared, thresholds, attributes, contact );
	}

	private static Vector3 Row0( Matrix4x4 frame ) => new( frame.M11, frame.M12, frame.M13 );
	private static Vector3 Row1( Matrix4x4 frame ) => new( frame.M21, frame.M22, frame.M23 );
	private static Vector3 Row2( Matrix4x4 frame ) => new( frame.M31, frame.M32, frame.M33 );
	private static Vector3 Position( Matrix4x4 frame ) => new( frame.M41, frame.M42, frame.M43 );

	private static Matrix4x4 Frame( Vector3 first, Vector3 second, Vector3 third, Vector3 position ) => new(
		first.X, first.Y, first.Z, 0f, second.X, second.Y, second.Z, 0f,
		third.X, third.Y, third.Z, 0f, position.X, position.Y, position.Z, 0f );

	private static Vector3 Rotate( Vector3 value, Vector3 axis, float angle )
	{
		var (sine, cosine) = O.SinCos( angle );
		var scaled = O.Scale( axis, sine );
		var offset = O.Scale( axis, A.Subtract( 1f, cosine ) );
		var first = new Vector3( A.MultiplyAdd( offset.X, axis.X, cosine ),
			A.MultiplyAdd( offset.X, axis.Y, scaled.Z ),
			A.MultiplyAdd( offset.X, axis.Z, -scaled.Y ) );
		var second = new Vector3( A.MultiplyAdd( offset.Y, axis.X, -scaled.Z ),
			A.MultiplyAdd( offset.Y, axis.Y, cosine ), A.MultiplyAdd( offset.Y, axis.Z, scaled.X ) );
		var third = new Vector3( A.MultiplyAdd( offset.Z, axis.X, scaled.Y ),
			A.MultiplyAdd( offset.Z, axis.Y, -scaled.X ), A.MultiplyAdd( offset.Z, axis.Z, cosine ) );
		return O.AddScaled( third, value.Z, O.AddScaled( second, value.Y, O.Scale( first, value.X ) ) );
	}
}
