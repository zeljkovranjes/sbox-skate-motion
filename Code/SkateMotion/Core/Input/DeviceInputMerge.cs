namespace SkateMotion.Core.Input;

using Vector3 = System.Numerics.Vector3;

/// <summary>
/// Keyboard and mouse and a controller both drive the rider at all times: each is read every frame and the two are
/// merged control by control, so no last-device flag decides and neither can lock the other out. A held key, the mouse
/// or a mouse flick drives its control; otherwise a stick or trigger pushed past drift drives it; buttons add up.
/// Readings are first settled: stick deflection and trigger travel within <see cref="StickDrift"/> and
/// <see cref="TriggerDrift"/> (a drifting stick, a resting trigger, a virtual pad) count as nothing.
/// </summary>
internal static class DeviceInputMerge
{
	/// <summary>Stick deflection (0 to 1) below which a stick counts as at rest.</summary>
	internal const float StickDrift = 0.2f;

	/// <summary>Trigger travel (0 to 1) below which a trigger counts as at rest.</summary>
	internal const float TriggerDrift = 0.25f;

	/// <summary>The reading with drift taken out: sticks and triggers inside their drift zones read zero, the rest
	/// as they are (so the controller's own thresholds still see the same deflections).</summary>
	internal static ControllerReading Settle( ControllerReading reading )
	{
		var (leftX, leftY) = Stick( reading.LeftX, reading.LeftY );
		var (rightX, rightY) = Stick( reading.RightX, reading.RightY );
		return reading with
		{
			LeftX = leftX,
			LeftY = leftY,
			RightX = rightX,
			RightY = rightY,
			LeftTrigger = reading.LeftTrigger < TriggerDrift ? 0f : reading.LeftTrigger,
			RightTrigger = reading.RightTrigger < TriggerDrift ? 0f : reading.RightTrigger
		};
	}

	/// <summary>Which parts of a settled reading are in use.</summary>
	internal static DeviceActivity Activity( ControllerReading settled ) => new(
		settled.LeftX != 0f || settled.LeftY != 0f,
		settled.RightX != 0f || settled.RightY != 0f,
		settled.LeftTrigger != 0f || settled.RightTrigger != 0f );

	/// <summary>
	/// One frame of board controls from both devices. steeringKey is whether a steering key is held: then the keys
	/// steer (their ramp also eases the trucks back to centre once let go); otherwise a pushed left stick does.
	/// </summary>
	internal static BoardControls Board( BoardControls keys, bool steeringKey, BoardControls pad, DeviceActivity active )
	{
		var keyFlick = keys.FlickX != 0f || keys.FlickY != 0f;
		var (flickX, flickY) = keyFlick ? (keys.FlickX, keys.FlickY)
			: active.RightStick ? (pad.FlickX, pad.FlickY)
			: (0f, 0f);
		return new BoardControls(
			steeringKey || !active.LeftStick ? keys.Steer : pad.Steer,
			MathF.Max( keys.Push, pad.Push ),
			MathF.Max( keys.Brake, pad.Brake ),
			flickX,
			flickY,
			keys.Lean != 0f ? keys.Lean : active.LeftStick ? pad.Lean : 0f,
			keys.Slide || pad.Slide,
			// The controller crouches with the left stick forward or a trigger. The stick is also held forward to roll
			// straight, so its crouch gives way to a push from either device, and the rider pushes rather than crouching
			// mid-stroke.
			keys.Pump || pad.Pump && (active.Triggers || active.LeftStick && MathF.Max( keys.Push, pad.Push ) <= 0f),
			// The triggers are the controller's hands.
			keys.Grab != 0 ? keys.Grab : active.Triggers ? pad.Grab : 0,
			keys.Gesture || pad.Gesture,
			keys.HippyJump || pad.HippyJump,
			keys.Catch || pad.Catch,
			keys.FeetOff | pad.FeetOff,
			pad.PushFeet != 0 ? pad.PushFeet : keys.PushFeet,
			keys.NewPush || pad.NewPush ) { HandPlant = keys.HandPlant || pad.HandPlant };
	}

	/// <summary>Walking: held movement keys walk, otherwise a pushed left stick does.</summary>
	internal static Vector3 Walk( Vector3 keys, bool keysHeld, Vector3 pad, DeviceActivity active ) =>
		keysHeld || !active.LeftStick ? keys : pad;

	private static (float X, float Y) Stick( float x, float y ) =>
		x * x + y * y < StickDrift * StickDrift ? (0f, 0f) : (x, y);
}
