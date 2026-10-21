using Sandbox;

namespace SkateMotion.Engine;

/// <summary>
/// The look input that orbits the skate camera: the engine's, which carries the mouse (at the player's sensitivity)
/// and the first controller's right stick. On the board the right stick drives tricks, so while riding its share is
/// taken back out (at the rates the engine adds it), whatever device the engine counts as last: the mouse always
/// orbits and the stick never does. On foot both orbit.
/// </summary>
internal static class CameraLook
{
	internal static Angles Read( bool riding, float delta )
	{
		var look = Input.AnalogLook;
		if ( !riding )
			return look;
		var stick = LocalControllerInput.Read();
		// The reading's Y is up-positive; the engine adds the raw (down-positive) axis.
		return new Angles( look.pitch + stick.RightY * delta * Preferences.ControllerLookPitchSpeed,
			look.yaw + stick.RightX * delta * Preferences.ControllerLookYawSpeed, look.roll );
	}
}
