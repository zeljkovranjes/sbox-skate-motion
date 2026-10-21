namespace SkateMotion.Core.Camera;

using Vector3 = System.Numerics.Vector3;

/// <summary>Angle and easing helpers for the follow camera. Angles are radians; +X forward, +Y left, +Z up.</summary>
internal static class CameraMath
{
	internal const float Degrees = MathF.PI / 180f;

	/// <summary>The angle wrapped into [-pi, pi).</summary>
	internal static float Wrap( float angle )
	{
		if ( angle >= -MathF.PI && angle < MathF.PI )
			return angle;
		var wrapped = angle - MathF.Floor( (angle + MathF.PI) / MathF.Tau ) * MathF.Tau;
		return wrapped >= MathF.PI ? wrapped - MathF.Tau : wrapped;
	}

	/// <summary>Moves an angle part of the way toward another along the shorter way round.</summary>
	internal static float Blend( float from, float to, float amount ) => Wrap( from + Wrap( to - from ) * amount );

	/// <summary>The heading of whichever end of the board (its nose or tail, from its facing) is nearer a heading.</summary>
	internal static float NearerEnd( float facing, float heading )
	{
		var nose = Wrap( facing );
		var tail = Wrap( facing + MathF.PI );
		return MathF.Abs( Wrap( nose - heading ) ) <= MathF.Abs( Wrap( tail - heading ) ) ? nose : tail;
	}

	/// <summary>Heading (yaw) of a direction's horizontal part.</summary>
	internal static float Heading( Vector3 direction ) => MathF.Atan2( direction.Y, direction.X );

	/// <summary>Sine ease from 0 to 1 with zero slope at both ends; the input is clamped to [0, 1].</summary>
	internal static float Ease( float fraction )
	{
		var t = Math.Clamp( fraction, 0f, 1f );
		return 0.5f + 0.5f * MathF.Sin( MathF.PI * (t - 0.5f) );
	}

	/// <summary>
	/// The value kept between two limits without a corner: it passes unchanged until within <paramref name="knee"/> of a
	/// limit and then bends smoothly toward it, so a value running into a limit slows down instead of stopping dead.
	/// </summary>
	internal static float SoftClamp( float value, float lowest, float highest, float knee )
	{
		if ( value > highest - knee )
			return highest - knee * MathF.Exp( -(value - (highest - knee)) / knee );
		if ( value < lowest + knee )
			return lowest + knee * MathF.Exp( -((lowest + knee) - value) / knee );
		return value;
	}

	/// <summary>0 below the low edge, 1 above the high edge and a smooth step between.</summary>
	internal static float Step( float low, float high, float value )
	{
		var t = Math.Clamp( (value - low) / (high - low), 0f, 1f );
		return t * t * (3f - 2f * t);
	}

	/// <summary>
	/// Unit direction from the focus out to the camera for a heading the camera looks along and an elevation above
	/// the focus: the camera sits behind the heading and above for positive elevation.
	/// </summary>
	internal static Vector3 Boom( float yaw, float elevation )
	{
		var level = MathF.Cos( elevation );
		return new Vector3( -MathF.Cos( yaw ) * level, -MathF.Sin( yaw ) * level, MathF.Sin( elevation ) );
	}
}
