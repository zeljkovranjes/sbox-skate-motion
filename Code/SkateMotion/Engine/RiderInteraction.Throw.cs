using Sandbox;
using SkateMotion.Core.Physics;

namespace SkateMotion.Engine;

internal sealed partial class RiderInteraction
{
	private bool _released;

	// Seconds into a drop or throw by which the hand lets go of the board when no animation says when (the clips' own
	// release comes earlier, a little under half a second in).
	private const float ReleaseAtLatest = BoardThrow.ReleaseSeconds;

	/// <summary>True once a drop or throw has gone on long enough that the board leaves the hand without the animation.</summary>
	internal bool ReleaseDue => Action is RiderAction.Drop or RiderAction.Throw && !_released && _elapsed >= _duration;

	/// <summary>
	/// Lets go of the held board where the hand holds it now, moving as the hand moves it (<paramref name="velocity"/> and
	/// <paramref name="spin"/>, world units per second); with no motion of the hand to go on, a throw sends the board
	/// forward and up and a drop lets it fall, both with the rider's own speed.
	/// </summary>
	internal bool Release( Transform pose, Vector3? velocity, Vector3? spin )
	{
		if ( Action is not (RiderAction.Drop or RiderAction.Throw) || _released )
			return false;
		var walking = _motor.Velocity;
		var thrown = velocity ?? SkateUnits.Inches( Action == RiderAction.Throw
			? BoardThrow.Toss( SkateUnits.ToCore( _rider.WorldRotation.Forward ), SkateUnits.Metres( walking ) )
			: SkateUnits.Metres( walking ) );
		_released = _motor.Release( pose, thrown, spin ?? Vector3.Zero );
		return _released;
	}
}
