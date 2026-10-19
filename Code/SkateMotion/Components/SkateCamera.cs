using Sandbox;
using SkateMotion.Engine;

namespace SkateMotion.Components;

/// <summary>
/// Optional collision-aware follow camera; it never changes board motion. The mouse orbits it (not while the mouse
/// drives a flick); while riding it swings back behind the rider when the mouse rests.
/// </summary>
public sealed class SkateCamera : Component
{
	/// <summary>Board object to follow.</summary>
	[Property] public GameObject Target { get; set; }

	/// <summary>Follow distance in engine inches.</summary>
	[Property, Range( 30f, 300f )] public float Distance { get; set; } = 110f;

	/// <summary>Camera height above the focus point in engine inches.</summary>
	[Property, Range( 10f, 160f )] public float Height { get; set; } = 45f;

	/// <summary>Let the mouse or a free right stick orbit the camera.</summary>
	[Property] public bool MouseOrbit { get; set; } = true;

	/// <summary>Camera receiving the follow pose.</summary>
	[RequireComponent] public CameraComponent Camera { get; set; }
	private GameObject _cachedTarget;
	private SkateRider _rider;
	private readonly SkateCameraRig _rig = new();

	protected override void OnUpdate()
	{
		if ( !Target.IsValid() || !Camera.IsValid() ) return;
		if ( _cachedTarget != Target )
		{
			_cachedTarget = Target;
			_rider = Target.Components.Get<SkateRider>();
		}
		var riding = !_rider.IsValid() || _rider.Mode == SkateRiderMode.Riding;
		// On the board a gamepad's right stick flicks tricks, so only the mouse orbits there.
		var look = MouseOrbit && !LocalSkateInput.Flicking && !(riding && Input.UsingController) ? Input.AnalogLook : default;
		_rig.Update( Camera, Target, Distance, Height, Time.Delta, _rider.IsValid() ? _rider.VisualObject : null, riding, look );
	}
}
