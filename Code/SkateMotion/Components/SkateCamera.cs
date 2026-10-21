using Sandbox;
using SkateMotion.Engine;

namespace SkateMotion.Components;

/// <summary>
/// Optional skate follow camera; it never changes board motion. It settles behind the way the board travels, holds
/// its height through ollies, looks down into drops, frames grinds, manuals and bails, and cranes over or pulls in
/// for anything in the way so it never ends up inside the world. The mouse orbits it one to one (not while the
/// mouse drives a flick); while riding it eases back behind the board once the mouse rests, and on foot it stays
/// where it was turned.
/// </summary>
public sealed class SkateCamera : Component
{
	/// <summary>Rider or board object to follow.</summary>
	[Property] public GameObject Target { get; set; }

	/// <summary>Follow distance in engine inches.</summary>
	[Property, Range( 30f, 300f )] public float Distance { get; set; } = 102f;

	/// <summary>Camera height above the focus point in engine inches.</summary>
	[Property, Range( 10f, 160f )] public float Height { get; set; } = 26f;

	/// <summary>Height of the focus point above the board or the rider's feet in engine inches; 0 takes it from the
	/// rider's height.</summary>
	[Property, Range( 0f, 100f )] public float FocusHeight { get; set; }

	/// <summary>Let the mouse or a free right stick orbit the camera.</summary>
	[Property] public bool MouseOrbit { get; set; } = true;

	/// <summary>Seconds the mouse must rest before a riding camera eases back behind the board.</summary>
	[Property, Range( 0.2f, 5f )] public float RecentreDelay { get; set; } = 1.2f;

	/// <summary>Camera receiving the follow pose.</summary>
	[RequireComponent] public CameraComponent Camera { get; set; }

	private GameObject _cachedTarget;
	private SkateRider _rider;
	private Skateboard _board;
	private readonly GameObject[] _ignored = new GameObject[ 4 ];
	private readonly SkateCameraRig _rig = new();

	protected override void OnUpdate()
	{
		if ( !Target.IsValid() || !Camera.IsValid() ) return;
		if ( _cachedTarget != Target )
			Resolve();
		var riding = !_rider.IsValid() || _rider.Mode == SkateRiderMode.Riding;
		// On the board a gamepad's right stick flicks tricks, so only the mouse orbits there.
		var look = MouseOrbit && !LocalSkateInput.Flicking ? CameraLook.Read( riding, Time.Delta ) : default;
		_rig.Update( Camera, Read(), Distance, Height, FocusHeight, RecentreDelay, look, Time.Delta );
	}

	private SkateCameraTarget Read()
	{
		var board = _board.IsValid() ? _board.RenderTransform : Target.WorldTransform;
		var rider = _rider.IsValid() ? _rider.ShownTransform : board;
		var mode = _rider.IsValid() ? _rider.Mode : SkateRiderMode.Riding;
		var velocity = mode == SkateRiderMode.Riding
			? _board.IsValid() ? _board.Body.Velocity : Vector3.Zero
			: _rider.Walker.Velocity;
		_ignored[ 0 ] = Target;
		_ignored[ 1 ] = _board.IsValid() ? _board.GameObject : null;
		_ignored[ 2 ] = _rider.IsValid() ? _rider.GameObject : null;
		_ignored[ 3 ] = _rider.IsValid() ? _rider.VisualObject : null;
		return new SkateCameraTarget( mode, board, rider, velocity, _board.IsValid() ? _board.State : default,
			_rider.IsValid() && !_rider.Walker.IsOnGround, _rider.IsValid() ? _rider.Walker.Height : 0f, _ignored );
	}

	// A rider target brings its board; a board target finds the rider standing on it, if there is one.
	private void Resolve()
	{
		_cachedTarget = Target;
		_rider = Target.Components.Get<SkateRider>();
		_board = _rider.IsValid() ? _rider.Board : Target.Components.Get<Skateboard>();
		if ( _rider.IsValid() || !_board.IsValid() )
			return;
		foreach ( var rider in Scene.GetAllComponents<SkateRider>() )
		{
			if ( rider.Board != _board ) continue;
			_rider = rider;
			break;
		}
	}
}
