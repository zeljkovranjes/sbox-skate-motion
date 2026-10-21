using Sandbox;

namespace SkateMotion.Engine;

internal sealed partial class RiderInteraction
{
	// Getting on with the board in hand: whether this mount is one, whether the board is down under the rider yet, and
	// the way the rider carries on across the ground meanwhile (inches per second).
	private bool _fromHand;
	private bool _airMount;
	private bool _placed;
	private Vector3 _glide;

	// Getting on from the hand, the board is put down as the mount's clip has the rider on it, and in any case after
	// HandMountLongest seconds; in the air it goes under the feet as the air mount's quarter second ends.
	private const float HandMountLongest = 1f;
	private const float AirMountTime = 0.25f;

	// Getting on from a stand the rider still steps forward onto the board (about two thirds of a metre in the clips), so
	// it carries on at no less than this (metres per second) along the way it faces.
	private const float SlowestMountStep = 0.75f;

	/// <summary>True for a mount with the board in the rider's hand (put down under the feet as the rider gets on).</summary>
	internal bool MountsFromHand => Action == RiderAction.Mount && _fromHand;

	/// <summary>Seconds since the interaction began.</summary>
	internal float Elapsed => _elapsed;

	/// <summary>True for a mount from the hand made in the air (the board goes under the feet before landing).</summary>
	internal bool MountsInAir => MountsFromHand && _airMount;

	/// <summary>True once a mount from the hand has gone on long enough that the board goes down now.</summary>
	internal bool MountDue => MountsFromHand && !_placed && _elapsed >= _duration;

	// The rider keeps its way across the ground while getting on: walking, running or sprinting at its own pace, from a
	// stand at least a step's.
	private Vector3 MountGlide()
	{
		var planar = _motor.Velocity.WithZ( 0f );
		var slowest = SlowestMountStep * SkateUnits.InchesPerMetre;
		if ( planar.Length >= slowest )
			return planar;
		return _rider.WorldRotation.Forward.WithZ( 0f ).Normal * slowest;
	}

	/// <summary>Puts the held board down at <paramref name="pose"/> under the rider and gets on it, moving as the rider moves.</summary>
	internal bool PlaceAndMount( Transform pose, float rideHeight )
	{
		if ( !MountsFromHand || _placed )
			return false;
		_placed = _motor.MountHeld( pose, _glide.WithZ( _motor.Velocity.z ), rideHeight );
		return _placed;
	}
}
