namespace SkateMotion.Core.Vehicle;

using Matrix4x4 = System.Numerics.Matrix4x4;
using Quaternion = System.Numerics.Quaternion;
using Vector3 = System.Numerics.Vector3;

/// <summary>
/// A hand plant: off the lip of a steep ramp, the rider plants a hand on the lip and stalls upside down over it, the
/// board held up, for as long as the plant is held (at most <see cref="MaximumHold"/>), then drops back in facing down
/// the ramp. The lip is where the board left the ramp; only a ramp at least as steep as <see cref="SteepRamp"/> there
/// has one to plant on, and the plant must start soon after take-off (<see cref="Window"/>) and near the lip
/// (<see cref="Reach"/>). One plant a flight.
/// </summary>
internal sealed class HandPlant
{
	/// <summary>Height of the take-off surface's normal at or below which the ramp is steep enough (60 degrees).</summary>
	internal const float SteepRamp = 0.5f;

	/// <summary>Seconds after take-off within which a plant can start.</summary>
	internal const float Window = 1.2f;

	/// <summary>Farthest the board may be from the lip (m) when the hand plants.</summary>
	internal const float Reach = 1.5f;

	/// <summary>Longest a plant is held, in seconds.</summary>
	internal const float MaximumHold = 2.5f;

	/// <summary>Where the board is held: this far above the lip (m), up at the planted arm's reach.</summary>
	internal const float HoldHeight = 1.2f;

	/// <summary>How far out over the ramp (m) the board is held from the lip.</summary>
	internal const float HoldOut = 0.25f;

	/// <summary>Seconds the board takes to come to its stall over the lip.</summary>
	internal const float Settle = 0.12f;

	/// <summary>Speed (m/s) the board drops back into the ramp at.</summary>
	internal const float DropSpeed = 1f;

	private Vector3 _lip;
	private Vector3 _outward = Vector3.UnitX;
	private float _rampUp = 1f;
	private float _airTime;
	private bool _spent;

	/// <summary>True while the hand is planted.</summary>
	internal bool Planted { get; private set; }

	/// <summary>Seconds since the hand planted.</summary>
	internal float Time { get; private set; }

	/// <summary>The lip: the surface point the board last left from.</summary>
	internal Vector3 Lip => _lip;

	/// <summary>Level direction out of the wall the lip tops (from the wall toward the air the board rose into).</summary>
	internal Vector3 Outward => _outward;

	/// <summary>Where the planted board is held.</summary>
	internal Vector3 Hold => _lip + Vector3.UnitZ * HoldHeight + _outward * HoldOut;

	/// <summary>The board is on the ground (or a rail) at <paramref name="position"/>, on a surface facing
	/// <paramref name="normal"/>: if it leaves from here, here is the lip.</summary>
	internal void Ground( Vector3 position, Vector3 normal )
	{
		_lip = position;
		_rampUp = normal.Z;
		var flat = new Vector3( normal.X, normal.Y, 0f );
		_outward = flat.LengthSquared() > 0.0001f ? Vector3.Normalize( flat ) : Vector3.UnitX;
		_airTime = 0f;
		_spent = false;
		Planted = false;
	}

	/// <summary>
	/// One step in the air with the plant button <paramref name="held"/> and the board at <paramref name="position"/>;
	/// true while the hand stays planted, and <paramref name="released"/> on the step it lets go.
	/// </summary>
	internal bool Step( bool held, Vector3 position, float step, out bool released )
	{
		released = false;
		_airTime += step;
		if ( Planted )
		{
			Time += step;
			if ( held && Time < MaximumHold )
				return true;
			Planted = false;
			released = true;
			return false;
		}
		// Held as the board rises past the lip (or pressed soon after), the hand plants; a bounce at the lip does not use it
		// up. The lip is where the wheels last touched highest, so a wheel lifting part way up the wall, the deck still
		// below that, is not a plant.
		if ( held && !_spent && _rampUp <= SteepRamp && _airTime <= Window && position.Z > _lip.Z
			&& Vector3.Distance( position, _lip ) <= Reach )
		{
			Planted = true;
			_spent = true;
			Time = 0f;
			return true;
		}
		return false;
	}

	/// <summary>The velocity that carries the board toward its stall over the lip.</summary>
	internal Vector3 HoldVelocity( Vector3 position ) => (Hold - position) / Settle;

	/// <summary>The board's turn as it drops back in: nose down the ramp, deck facing out of it.</summary>
	internal Quaternion DropRotation()
	{
		var forward = -Vector3.UnitZ;
		var up = _outward;
		var left = Vector3.Cross( up, forward );
		// Rows are where the deck's forward, left and up axes end up.
		var basis = new Matrix4x4(
			forward.X, forward.Y, forward.Z, 0f,
			left.X, left.Y, left.Z, 0f,
			up.X, up.Y, up.Z, 0f,
			0f, 0f, 0f, 1f );
		return Quaternion.Normalize( Quaternion.CreateFromRotationMatrix( basis ) );
	}

	/// <summary>The board's velocity as it drops back in: down the ramp, a little out from it.</summary>
	internal Vector3 DropVelocity() => -Vector3.UnitZ * DropSpeed + _outward * 0.2f;
}
