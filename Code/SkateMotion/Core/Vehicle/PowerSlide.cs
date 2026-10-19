namespace SkateMotion.Core.Vehicle;

using Vector3 = System.Numerics.Vector3;

/// <summary>
/// Powerslides and reverts. Holding slide while rolling turns the board across its travel (to the steering side)
/// and lets it slide; letting go turns it back in line with the travel. A short tap instead spins it half way round
/// (a revert), so the board rolls on backwards. While any of this is under way the wheels slide (see
/// <see cref="Sliding"/>).
/// </summary>
internal sealed class PowerSlide
{
	private enum Phase { None, Sliding, Returning, Reverting }

	private Phase _phase;
	private bool _held;
	private float _pressed;
	private Vector3 _revertTarget;
	private bool _backwards;

	/// <summary>Side the board slides to: -1 left, 1 right, 0 when not sliding.</summary>
	internal int Side { get; private set; }

	/// <summary>True while the board is turned across its travel, turning back, or reverting: the wheels slide.</summary>
	internal bool Sliding => _phase != Phase.None;

	/// <summary>True while a revert spins the board half way round.</summary>
	internal bool Reverting => _phase == Phase.Reverting;

	/// <summary>Slowest rolling speed (m/s) a powerslide starts or keeps going at.</summary>
	internal const float MinimumSpeed = 1f;

	/// <summary>Longest press (seconds) that counts as a tap, making a revert.</summary>
	internal const float TapTime = 0.25f;

	// How fast the board turns toward its heading per radian off it, its fastest turn (radians per second), how
	// quickly (seconds) its spin follows, and how close (radians) counts as lined up.
	private const float Turn = 8f;
	private const float MaximumTurn = 12f;
	private const float Response = 0.05f;
	private const float LinedUp = 0.05f;

	/// <summary>Change of the deck's angular velocity (world) this step; zero when nothing is under way.</summary>
	internal Vector3 Step( in BoardBody body, Vector3 normal, float steer, bool slide, bool supported, float step )
	{
		var pressed = slide && !_held;
		var released = !slide && _held;
		_held = slide;
		_pressed = pressed ? 0f : _pressed + step;
		var travel = body.Velocity - normal * Vector3.Dot( body.Velocity, normal );
		var speed = travel.Length();
		var forward = body.Forward - normal * Vector3.Dot( body.Forward, normal );
		if ( !supported || speed < 0.01f || forward.LengthSquared() < 0.0001f )
		{
			Reset();
			return Vector3.Zero;
		}
		forward = Vector3.Normalize( forward );
		// The line of travel the deck rides along, forwards or backwards as it was riding when the slide began (half
		// way round, which way it faces says nothing).
		if ( _phase == Phase.None )
			_backwards = Vector3.Dot( forward, travel ) < 0f;
		var line = travel / speed * (_backwards ? -1f : 1f);

		if ( _phase == Phase.None && pressed && speed >= MinimumSpeed )
		{
			_phase = Phase.Sliding;
			Side = steer < 0f ? -1 : 1;
		}
		else if ( _phase == Phase.Sliding && released )
		{
			_phase = _pressed < TapTime ? Phase.Reverting : Phase.Returning;
			_revertTarget = -line;
		}
		else if ( _phase == Phase.Sliding && speed < MinimumSpeed )
			_phase = Phase.Returning;
		if ( _phase == Phase.None )
			return Vector3.Zero;

		var target = _phase switch
		{
			// Across the travel, to the slide's side.
			Phase.Sliding => Vector3.Normalize( Vector3.Cross( normal, line ) ) * -Side,
			Phase.Reverting => _revertTarget,
			_ => line
		};
		var error = SignedAngle( forward, target, normal );
		// A revert spins on the slide's side, the long way round if need be, rather than whichever way is shorter.
		if ( _phase == Phase.Reverting && MathF.Abs( error ) > MathF.PI * 0.5f )
			error = MathF.Abs( error ) * -Side;
		var spin = Vector3.Dot( body.AngularVelocity, normal );
		// Lined up again: done, and the turn stops there rather than carrying the board past.
		if ( _phase != Phase.Sliding && MathF.Abs( error ) < LinedUp )
		{
			Reset();
			return normal * -spin;
		}
		var wanted = Math.Clamp( error * Turn, -MaximumTurn, MaximumTurn );
		return normal * ((wanted - spin) * Math.Min( 1f, step / Response ));
	}

	internal void Reset()
	{
		_phase = Phase.None;
		Side = 0;
	}

	// Angle (radians) turning from about up onto to, positive anticlockwise seen from above.
	private static float SignedAngle( Vector3 from, Vector3 to, Vector3 up ) =>
		MathF.Atan2( Vector3.Dot( Vector3.Cross( from, to ), up ), Vector3.Dot( from, to ) );
}
