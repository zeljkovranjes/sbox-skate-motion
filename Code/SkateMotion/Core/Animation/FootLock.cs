namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;

/// <summary>
/// Holds a planted foot where it was put down while the pose would slide it across the ground: from the moment it plants
/// it stays at that place across the ground (its height is the pose's own, which keeps it on the ground), and once it lifts
/// (or the pose has taken it further than <see cref="Reach"/> away) it is let go, easing back to the pose over
/// <see cref="Release"/> rather than in a frame. Positions are in metres, in any fixed frame (the world's).
/// </summary>
internal sealed class FootLock
{
	private Vector3 _locked;
	private Vector3 _offset;
	private bool _holding;

	/// <summary>Farthest (metres, across the ground) the pose may take the foot from where it is held before it is let go.</summary>
	internal const float Reach = 0.15f;

	/// <summary>Seconds a foot let go of takes to ease back to where the pose has it.</summary>
	internal const float Release = 0.15f;

	/// <summary>True while the foot is held where it was planted.</summary>
	internal bool Holding => _holding;

	/// <summary>Where the foot goes, given where the pose puts it now (<paramref name="free"/>) and whether it is planted.</summary>
	internal Vector3 Update( Vector3 free, bool planted, float delta )
	{
		if ( planted && !_holding )
		{
			// It plants where it is shown (still easing back from a hold before, it goes on from there).
			_holding = true;
			_locked = free + _offset;
		}
		if ( _holding )
		{
			var across = new Vector3( _locked.X - free.X, _locked.Y - free.Y, 0f );
			var length = across.Length();
			if ( planted && length <= Reach )
			{
				_offset = across;
				return free + across;
			}
			// The pose (or the rider, placed somewhere else) leaves the foot behind: it eases back from no further than Reach.
			_offset = length > Reach ? across * (Reach / length) : across;
			_holding = false;
		}
		if ( _offset == Vector3.Zero )
			return free;
		// Let go of, the hold eases out.
		_offset *= MathF.Exp( -3f * delta / Release );
		if ( _offset.LengthSquared() < 1e-8f )
			_offset = Vector3.Zero;
		return free + _offset;
	}

	/// <summary>Forgets the hold at once (the rider placed somewhere else, or riding).</summary>
	internal void Reset()
	{
		_holding = false;
		_offset = Vector3.Zero;
	}
}
