namespace SkateMotion.Core.Vehicle;

using Vector3 = System.Numerics.Vector3;

/// <summary>
/// Hippy jump. While it is held the rider crouches on the board; letting go jumps the rider straight up off it, keeping
/// the speed it had, while the board rolls on beneath. A longer crouch, up to <see cref="FullCrouch"/>, jumps higher.
/// Coming down, the rider lands back on the deck when the board is still under its feet and on the ground, and otherwise
/// misses it. The rider is tracked relative to the board, so slopes and drops under the jump are followed. Metres, z up.
/// </summary>
internal sealed class HippyJump
{
	internal enum Outcome { None, Launched, Landed, Missed }

	private bool _held;
	private float _crouch;
	private Vector3 _velocity;
	private float _gravity = 9.8f;

	/// <summary>True while the rider is off the board in the air.</summary>
	internal bool Flying { get; private set; }

	/// <summary>True while the jump is held on the board, before it is let go.</summary>
	internal bool Crouching => _held && !Flying;

	/// <summary>Where the rider is relative to the deck it jumped from (z is its height above it).</summary>
	internal Vector3 Offset { get; private set; }

	/// <summary>The rider's speed toward or away from the deck: positive while still rising off it.</summary>
	internal float VerticalSpeed { get; private set; }

	/// <summary>
	/// How far the crouch has wound the jump up, 0 for a quick one to 1 for a full crouch: while it is held, and through
	/// the flight it gave. Zero otherwise.
	/// </summary>
	internal float Charge { get; private set; }

	/// <summary>True while the board is still close enough under the rider to land back on.</summary>
	internal bool OverBoard => new Vector3( Offset.X, Offset.Y, 0f ).Length() <= LandReach;

	/// <summary>Seconds until the rider comes back down to the deck's height, zero on the board.</summary>
	internal float TimeToLand => !Flying ? 0f
		: (VerticalSpeed + MathF.Sqrt( MathF.Max( 0f, VerticalSpeed * VerticalSpeed + 2f * _gravity * MathF.Max( Offset.Z, 0f ) ) )) / _gravity;

	// Height (m) of the rider's centre of mass above the board at the top of a quick and of a full crouch's jump, its
	// height standing on the deck, and how long (seconds) a crouch must be held to jump highest.
	internal const float LowApex = 1.35f;
	internal const float HighApex = 1.65f;
	internal const float Standing = 1f;
	internal const float FullCrouch = 0.5f;

	/// <summary>Farthest (m) the board can have moved out from under the rider for it to land back on.</summary>
	internal const float LandReach = 0.3f;

	/// <summary>
	/// Advances the jump. <paramref name="held"/> is the jump control; <paramref name="boardVelocity"/> and
	/// <paramref name="boardGrounded"/> describe the board this step.
	/// </summary>
	internal Outcome Step( bool held, Vector3 boardVelocity, bool boardGrounded, float gravity, float step )
	{
		_gravity = gravity > 0f ? gravity : 9.8f;
		if ( Flying )
		{
			_held = held;
			// The rider flies on unaided; the board keeps doing whatever it does beneath.
			_velocity.Z -= _gravity * step;
			Offset += (_velocity - boardVelocity) * step;
			VerticalSpeed = _velocity.Z - boardVelocity.Z;
			if ( Offset.Z > 0f || VerticalSpeed > 0f )
				return Outcome.None;
			var landed = OverBoard && boardGrounded;
			Reset();
			return landed ? Outcome.Landed : Outcome.Missed;
		}
		var released = _held && !held;
		_held = held;
		if ( held )
		{
			_crouch += step;
			Charge = Math.Clamp( _crouch / FullCrouch, 0f, 1f );
			return Outcome.None;
		}
		var crouch = _crouch;
		_crouch = 0f;
		if ( !released || !boardGrounded )
		{
			Charge = 0f;
			return Outcome.None;
		}
		// Rising from standing height to the apex takes this upward speed.
		Charge = Math.Clamp( crouch / FullCrouch, 0f, 1f );
		var apex = LowApex + (HighApex - LowApex) * Charge;
		var rise = MathF.Sqrt( MathF.Max( 0f, (apex - Standing) * 2f * _gravity ) );
		_velocity = new Vector3( boardVelocity.X, boardVelocity.Y, boardVelocity.Z + rise );
		Offset = Vector3.Zero;
		VerticalSpeed = rise;
		Flying = true;
		return Outcome.Launched;
	}

	internal void Reset()
	{
		Flying = false;
		_crouch = 0f;
		Charge = 0f;
		_velocity = Vector3.Zero;
		Offset = Vector3.Zero;
		VerticalSpeed = 0f;
	}

	/// <summary>
	/// The rider is off the board: forgets the jump and the control held for it, so a crouch held while stepping off
	/// does not go on crouching the rider on foot.
	/// </summary>
	internal void Clear()
	{
		Reset();
		_held = false;
	}
}
