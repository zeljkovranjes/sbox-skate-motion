namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;
using Vector4 = System.Numerics.Vector4;
using A = PhysicalCadenceArithmetic;

/// <summary>Advances retained physical gait from relative motion and authored translation.</summary>
internal static partial class PhysicalLocomotionCadence
{
	/// <summary>Updates one fixed step; returns false without mutation for pending contact correction.</summary>
	internal static bool TryUpdate( ref PhysicalLocomotionCadenceState state,
		in PhysicalLocomotionCadenceInput input, Vector4 thresholds )
		=> TryUpdateCore( ref state, input, thresholds, null );

	private static bool TryUpdateCore( ref PhysicalLocomotionCadenceState state,
		in PhysicalLocomotionCadenceInput input, Vector4 thresholds, PhysicalLocomotionCadenceContactInput? contacts )
	{
		var motion = A.Subtract( input.Motion, input.MotionReference );
		if ( input.RejectMotion )
			motion = A.Reject( motion, input.RejectionAxis );
		var magnitude = A.Length( A.Reject( motion, input.Up ) );
		var next = state;
		for ( var index = 0; index < 4; index++ )
		{
			var threshold = index switch { 0 => thresholds.X, 1 => thresholds.Y, 2 => thresholds.Z, _ => thresholds.W };
			if ( !A.Greater( magnitude, threshold ) )
			{
				next = next with { LocomotionIndex = index };
				break;
			}
		}

		if ( !A.Greater( 0f, input.RequestedPhase ) )
			next = Request( next, input.RequestedPhase, input.RequestedDuration );
		else if ( next.LocomotionIndex == 0 )
		{
			if ( A.Greater( 0f, next.Target ) )
				next = Stop( next );
		}
		else
		{
			// Contact correction requires actual physical observations.
			var correctContacts = !input.RejectMotion && !input.SuppressContactAdjustment && (input.ContactFlags & 0x180u) != 0;
			if ( correctContacts && !contacts.HasValue )
				return false;
			var horizontal = new Vector3( input.AnimationMotion.X, 0f, input.AnimationMotion.Z );
			var horizontalLength = A.Length( horizontal );
			if ( A.Greater( horizontalLength, A.Value( 0x3a83126e ) ) )
			{
				var local = new Vector3( A.TransformRow( input.FrameRow0, motion ),
					A.TransformRow( input.FrameRow1, motion ), A.TransformRow( input.FrameRow2, motion ) );
				var projection = A.Multiply( A.Divide( 1f, horizontalLength ), A.Dot( local, horizontal ) );
				magnitude = A.Root( A.MultiplyAdd( projection, projection, A.Multiply( local.Y, local.Y ) ) );
			}
			var animationLength = A.Length( input.AnimationMotion );
			var rate = A.Greater( A.Value( 0x3a83126e ), animationLength ) ? 0f : A.Divide( magnitude, animationLength );
			next = next with { Rate = rate, Target = -1f };
			if ( correctContacts )
				next = CorrectContact( next, input.ContactFlags, magnitude, contacts.Value );
		}
		state = Advance( next );
		return true;
	}

	private static PhysicalLocomotionCadenceState Request( PhysicalLocomotionCadenceState state, float target, float duration )
	{
		var delay = A.Value( 0x3dcccccc );
		if ( A.Greater( duration, delay ) )
			duration = A.Subtract( duration, delay );
		if ( state.Target == target && state.Duration == duration )
			return state;
		var delta = A.Subtract( target, state.Phase );
		if ( A.Greater( 0f, delta ) )
			delta = A.Add( delta, 1f );
		return state with { Target = target, Duration = duration, ForwardTarget = true, Rate = A.Divide( delta, duration ) };
	}

	private static PhysicalLocomotionCadenceState Stop( PhysicalLocomotionCadenceState state )
	{
		var target = 0f;
		var best = A.Value( 0x7149f2c9 );
		for ( var index = 0; index < 2; index++ )
		{
			var candidate = index == 0 ? 0f : .5f;
			var delta = A.Subtract( candidate, state.Phase );
			var wrapped = A.Wrap( A.Add( delta, 1f ), 0f, 1f );
			var distance = A.Greater( A.Abs( wrapped ), A.Abs( delta ) ) ? A.Abs( delta ) : A.Abs( wrapped );
			if ( A.Greater( best, distance ) )
			{
				best = distance;
				target = candidate;
			}
		}
		var direct = A.Abs( A.Subtract( target, state.Phase ) );
		var around = A.Subtract( 1f, direct );
		var shortest = A.Negative( A.Subtract( direct, around ) ) ? direct : around;
		return state with { Target = target, Duration = A.Value( 0x3e4ccccc ), ForwardTarget = false, Rate = A.Multiply( shortest, 5f ) };
	}

	private static PhysicalLocomotionCadenceState Advance( PhysicalLocomotionCadenceState state )
	{
		var tick = A.Value( 0x3c888888 );
		if ( A.Greater( 0f, state.Target ) )
		{
			var next = A.MultiplyAdd( state.Rate, tick, state.Phase );
			return state with { Phase = A.Subtract( next, A.Floor( next ) ) };
		}
		if ( state.ForwardTarget )
		{
			if ( state.Phase == state.Target )
				return state;
			var goal = A.Greater( state.Phase, state.Target ) ? A.Add( state.Target, 1f ) : state.Target;
			var next = A.MultiplyAdd( state.Rate, tick, state.Phase );
			return state with { Phase = A.Greater( goal, state.Phase ) && !A.Greater( goal, next )
				? state.Target : A.Subtract( next, A.Floor( next ) ) };
		}
		var step = A.Multiply( state.Rate, tick );
		var delta = A.Wrap( A.Subtract( state.Target, state.Phase ), -.5f, .5f );
		var value = A.Add( state.Phase, delta );
		if ( A.Greater( state.Phase, A.Add( value, step ) ) )
			value = A.Subtract( state.Phase, step );
		else if ( A.Greater( A.Subtract( value, step ), state.Phase ) )
			value = A.Add( state.Phase, step );
		return state with { Phase = A.Wrap( value, 0f, 1f ) };
	}
}

