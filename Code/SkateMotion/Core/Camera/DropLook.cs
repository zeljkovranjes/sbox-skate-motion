namespace SkateMotion.Core.Camera;

/// <summary>
/// Raises the camera to look down into a drop ahead. Probes ahead of the subject along its travel report the ground
/// height there; the deepest drop below a reference ground (the ground the camera's followed height stands for, so
/// the ground left behind during an ollie) gives an angle, depth over distance, that the camera's elevation is raised
/// by. Small angles (a kerb, a gentle slope) are ignored, and the angle is capped: 70 degrees on the board, 40 on
/// foot. The result follows smoothly.
/// </summary>
internal sealed class DropLook
{
	// Angles (radians) below which a drop is ignored, faded in up to the second.
	private const float IgnoreBelow = 10f * CameraMath.Degrees;
	private const float FullFrom = 20f * CameraMath.Degrees;

	private const float BoardLimit = 70f * CameraMath.Degrees;
	private const float FootLimit = 40f * CameraMath.Degrees;

	// Speed (m/s) above which the look-down eases off a little (to 90% by 8 m/s).
	private const float EaseOffSpeed = 4f;

	private static readonly SpringTuning Response = SpringTuning.Critical( 4f ).Limited( 90f * CameraMath.Degrees );

	private readonly CameraSpring _elevation = new();

	/// <summary>Extra elevation in radians.</summary>
	internal float Value => _elevation.Value;

	internal void Reset() => _elevation.Reset( 0f );

	/// <param name="ground">The probes ahead.</param>
	/// <param name="reference">Height (m) of the ground drops are measured from.</param>
	internal float Update( CameraGround ground, float reference, bool onFoot, float speed, float delta )
	{
		var target = Angle( ground.Distances, ground.Grounds, ground.Count, reference );
		target = MathF.Min( target, onFoot ? FootLimit : BoardLimit ) * Math.Clamp( 1f - (speed - EaseOffSpeed) * 0.025f, 0.9f, 1f );
		return _elevation.Step( target, delta, Response );
	}

	private static float Angle( float[] distances, float[] grounds, int count, float reference )
	{
		var deepest = 0f;
		var distance = 0f;
		for ( var index = 0; index < count; index++ )
		{
			if ( float.IsNaN( grounds[ index ] ) )
				break;
			var depth = reference - grounds[ index ];
			if ( depth > deepest && distances[ index ] > 0f )
			{
				deepest = depth;
				distance = distances[ index ];
			}
		}
		if ( distance <= 0f )
			return 0f;
		var angle = MathF.Atan2( deepest, distance );
		return angle * CameraMath.Step( IgnoreBelow, FullFrom, angle );
	}
}
