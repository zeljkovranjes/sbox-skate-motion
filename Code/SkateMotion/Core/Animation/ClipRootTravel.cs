namespace SkateMotion.Core.Animation;

using Quaternion = System.Numerics.Quaternion;
using Vector3 = System.Numerics.Vector3;

/// <summary>
/// The travel of the clip a state graph plays, read from its root bone: where the root is now, where it is at the clip's
/// start and end, and how far along that way it has gone. The pose the rider is shown in leaves the root out (it is
/// placed from the rider), so a clip's board resting on the ground while the clip's skater steps toward it or away from
/// it is found from this travel, against the end where the skater stands still by the board: the start when the board is
/// already out of the clip's hand there, else the end.
/// </summary>
internal sealed class ClipRootTravel
{
	private readonly ClipSkeleton _skeleton;
	private readonly SkeletonPose _scratch;
	private readonly int[] _chain;
	private AnimationClip _clip;
	private float _length;

	// Travel shorter than this (source units, metres) is no travel: the progress along it says nothing.
	internal const float LeastTravel = 0.05f;

	// Source distance (metres) from the hand to the board beyond which the board is out of the clip's hand.
	internal const float OutOfHand = 0.3f;

	internal ClipRootTravel( ClipSkeleton skeleton )
	{
		_skeleton = skeleton;
		_scratch = new SkeletonPose( skeleton.Count );
		_chain = new int[ skeleton.Count ];
	}

	/// <summary>Root position at the clip's start (source units).</summary>
	internal Vector3 Start { get; private set; }

	/// <summary>Root position at the clip's end (source units).</summary>
	internal Vector3 End { get; private set; }

	/// <summary>Root position now (source units).</summary>
	internal Vector3 Now { get; private set; }

	/// <summary>True when the board is out of the clip's hand at its start: the skater stands still by it there.</summary>
	internal bool StandsAtStart { get; private set; }

	/// <summary>
	/// How fast the root goes now (source units per second of the clip's own time), over the next <see cref="Ahead"/> (or
	/// the frame's step, if longer): what follows the travel takes it up a frame or two late (a walker moved in fixed steps,
	/// shown a step behind), so it is read a little ahead.
	/// </summary>
	internal Vector3 Velocity { get; private set; }

	// Seconds of the clip's time ahead over which its travel is read.
	internal const float Ahead = 1f / 30f;

	/// <summary>Where the root is now against where the skater stands still by the board (source units).</summary>
	internal Vector3 FromStand => Now - (StandsAtStart ? Start : End);

	/// <summary>How far along its way from start to end the root is (0 to 1), or null for a clip that does not travel.</summary>
	internal float? Progress
	{
		get
		{
			var way = End - Start;
			var length = way.LengthSquared();
			if ( _clip is null || length < LeastTravel * LeastTravel )
				return null;
			return Math.Clamp( Vector3.Dot( Now - Start, way ) / length, 0f, 1f );
		}
	}

	/// <summary>
	/// Reads <paramref name="clip"/> at <paramref name="time"/> (held at its end past it), played on by
	/// <paramref name="step"/> seconds of its own time a frame; <paramref name="hand"/> and <paramref name="board"/> are
	/// the source bones the clip carries the board with. False without a clip.
	/// </summary>
	internal bool Update( AnimationClip clip, float time, float step, AnimationParameters parameters, int hand, int board )
	{
		if ( clip is null )
		{
			_clip = null;
			return false;
		}
		if ( !ReferenceEquals( clip, _clip ) )
		{
			_clip = clip;
			_length = Math.Max( clip.Length( parameters ), 0f );
			Start = Root( 0f, parameters );
			StandsAtStart = hand >= 0 && board >= 0
				&& Vector3.Distance( Bone( hand ), Bone( board ) ) > OutOfHand;
			End = Root( _length, parameters );
		}
		var at = Math.Clamp( time, 0f, _length );
		var ahead = Math.Clamp( time + MathF.Max( step, Ahead ), 0f, _length );
		Now = Root( at, parameters );
		Velocity = ahead > at ? (Root( ahead, parameters ) - Now) / (ahead - at) : Vector3.Zero;
		return true;
	}

	private Vector3 Root( float time, AnimationParameters parameters )
	{
		_scratch.Sample( _clip, _skeleton, time, false, parameters );
		return _scratch.Positions[ 0 ];
	}

	// A bone's place in the last sampled pose, from the root (left at the origin, unturned) down.
	private Vector3 Bone( int bone )
	{
		var position = Vector3.Zero;
		var rotation = Quaternion.Identity;
		var chain = 0;
		for ( var at = bone; at > 0 && chain < _chain.Length; at = _skeleton.Parents[ at ] )
			_chain[ chain++ ] = at;
		for ( var i = chain - 1; i >= 0; i-- )
		{
			position += Vector3.Transform( _scratch.Positions[ _chain[ i ] ], rotation );
			rotation = Quaternion.Normalize( rotation * _scratch.Rotations[ _chain[ i ] ] );
		}
		return position;
	}
}
