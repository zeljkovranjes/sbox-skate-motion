namespace SkateMotion.Core.Physics;

using Quaternion = System.Numerics.Quaternion;
using Vector3 = System.Numerics.Vector3;

/// <summary>
/// How a board held in the hand is moving, from the poses it is shown at frame by frame: so a board let go carries on
/// as the hand was moving it (a throw's swing, a drop's fall), not from rest. Each frame's motion is blended in over a
/// short time, so one uneven frame does not decide where the board goes. Units are the caller's own, per second.
/// </summary>
internal sealed class HeldBoardMotion
{
	private Vector3 _position;
	private Quaternion _rotation;
	private bool _started;

	// How fast (per second) a new frame's motion takes over from the motion measured so far.
	private const float Response = 30f;

	/// <summary>Velocity of the held board.</summary>
	internal Vector3 Velocity { get; private set; }

	/// <summary>Angular velocity of the held board: its turn axis times radians per second.</summary>
	internal Vector3 Spin { get; private set; }

	/// <summary>Forgets the board's motion: the next pose starts it again from rest.</summary>
	internal void Reset()
	{
		_started = false;
		Velocity = Vector3.Zero;
		Spin = Vector3.Zero;
	}

	/// <summary>Takes the pose the board is shown at now, <paramref name="seconds"/> after the last one.</summary>
	internal void Add( Vector3 position, Quaternion rotation, float seconds )
	{
		if ( !float.IsFinite( position.X + position.Y + position.Z ) )
			return;
		rotation = Quaternion.Normalize( rotation );
		if ( !_started || !(seconds > 0f) )
		{
			_position = position;
			_rotation = rotation;
			_started = true;
			return;
		}
		var velocity = (position - _position) / seconds;
		var turn = Quaternion.Normalize( rotation * Quaternion.Conjugate( _rotation ) );
		if ( turn.W < 0f )
			turn = Quaternion.Negate( turn );
		var sine = MathF.Sqrt( turn.X * turn.X + turn.Y * turn.Y + turn.Z * turn.Z );
		var spin = sine > 1e-6f
			? new Vector3( turn.X, turn.Y, turn.Z ) / sine * (2f * MathF.Atan2( sine, turn.W ) / seconds)
			: Vector3.Zero;
		var take = 1f - MathF.Exp( -Response * seconds );
		Velocity += (velocity - Velocity) * take;
		Spin += (spin - Spin) * take;
		_position = position;
		_rotation = rotation;
	}
}
