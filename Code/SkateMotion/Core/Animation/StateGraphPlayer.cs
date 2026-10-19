namespace SkateMotion.Core.Animation;

using Quaternion = System.Numerics.Quaternion;
using Vector3 = System.Numerics.Vector3;

/// <summary>
/// Plays what a <see cref="StateGraph"/> asks for: each new clip or blend fades in over the state's blend time
/// (never shorter than <see cref="MinimumBlend"/>) from the pose it replaces, then loops until the graph asks for
/// something else. A clip asked for again restarts with the same fade from the pose shown. A name the clip set
/// lacks keeps the current clip playing.
/// </summary>
internal sealed class StateGraphPlayer
{
	private readonly ClipSet _clips;
	private readonly SkeletonPose _from;
	private readonly Vector3[] _offsetPositions;
	private readonly Quaternion[] _offsetRotations;
	private readonly SkeletonPose _scratch;
	private AnimationClip _clip;
	private float _time;

	// The playing clip's length when last updated, seconds.
	private float _length;
	private float _fade = 1f;
	private float _fadeTime;
	private int _played = -1;

	/// <summary>Shortest fade between clips, in seconds, so no change of clip shows as a one-frame snap.</summary>
	internal const float MinimumBlend = 0.1f;

	internal StateGraphPlayer( ClipSet clips, StateGraphDefinition definition )
	{
		_clips = clips;
		Graph = new StateGraph( definition );
		_from = new SkeletonPose( clips.Skeleton.Count );
		_offsetPositions = new Vector3[ clips.Skeleton.Count ];
		_offsetRotations = new Quaternion[ clips.Skeleton.Count ];
		_scratch = new SkeletonPose( clips.Skeleton.Count );
		_from.SetRest( clips.Skeleton );
	}

	internal StateGraph Graph { get; }

	/// <summary>Name of the clip or blend playing.</summary>
	internal string ClipName => _clip?.Name;

	/// <summary>Clip or blend playing, or null before the first.</summary>
	internal AnimationClip Clip => _clip;

	/// <summary>Multiplies the playing state's speed (1 = as authored), e.g. to match a pushing foot to the ground.</summary>
	internal float RateScale { get; set; } = 1f;

	// Transitions are inertialized rather than crossfaded from a frozen pose: when a clip starts, each bone's
	// difference from the pose shown last (position, and rotation along the shortest arc) is recorded and then
	// eased out while the new clip plays its own motion. A fast-moving bone (a flipping board) therefore keeps
	// moving through the change, and no blend ever passes between two nearly opposite rotations, where blending
	// flips to the other way round in a single frame.
	private void CaptureOffsets()
	{
		for ( var bone = 0; bone < _offsetRotations.Length; bone++ )
		{
			_offsetPositions[ bone ] = _from.Positions[ bone ] - _scratch.Positions[ bone ];
			var offset = Quaternion.Normalize( _from.Rotations[ bone ] * Quaternion.Conjugate( Quaternion.Normalize( _scratch.Rotations[ bone ] ) ) );
			_offsetRotations[ bone ] = offset.W < 0f ? Quaternion.Negate( offset ) : offset;
		}
	}

	private void Inertialize( SkeletonPose output )
	{
		// The remaining share of the offset falls along a smooth curve, so the change has no sudden start or end.
		var remaining = 1f - _fade * _fade * (3f - 2f * _fade);
		output.CopyFrom( _from );
		output.BlendToward( _scratch, _fade );
		for ( var bone = 0; bone < _offsetRotations.Length; bone++ )
		{
			output.Positions[ bone ] = _scratch.Positions[ bone ] + _offsetPositions[ bone ] * remaining;
			output.Rotations[ bone ] = Quaternion.Normalize(
				Quaternion.Slerp( Quaternion.Identity, _offsetRotations[ bone ], remaining ) * _scratch.Rotations[ bone ] );
		}
	}


	internal void Update( StateFacts facts, AnimationParameters parameters, float delta, SkeletonPose output )
	{
		// A clip shorter than its fade in (a single held pose has no length at all) lasts as long as the fade, so a state
		// waiting for it to end shows it first rather than ending, and starting again, every frame.
		Graph.ClipLength = Math.Max( _clip?.Length( parameters ) ?? float.PositiveInfinity, _fadeTime );
		// A blend's length follows its weights. Its clips play in phase (time over length), so when the length changes
		// the elapsed time is rescaled to keep the phase: otherwise a weight changing late in a long cycle would
		// jump every clip to another point in its motion.
		if ( _clip is not null && _length > 0f && float.IsFinite( Graph.ClipLength ) && Graph.ClipLength != _length )
			_time *= Graph.ClipLength / _length;
		var speed = (Graph.Play?.Speed ?? 1f) * RateScale;
		var nextTime = _time + delta * speed;
		var nextFade = _fadeTime > 0f ? Math.Min( 1f, _fade + delta / Math.Max( _fadeTime, 0.0001f ) ) : 1f;
		// Conditions observe the playback being produced this frame, including any retained phase.
		// A separately accumulated state timer can lag by one frame at a clip's wrap boundary.
		Graph.Update( facts, delta, new AnimationPlaybackStatus( Graph.ClipLength - nextTime, nextFade < 1f ) );
		_time = nextTime;
		_length = Graph.ClipLength;
		if ( Graph.PlayCount != _played )
		{
			_played = Graph.PlayCount;
			var next = Graph.Clip is { } name ? _clips.Find( name ) : null;
			if ( next is not null && !ReferenceEquals( next, _clip ) || next is not null && _clip is null )
			{
				// Fade from the pose shown last frame.
				_from.CopyFrom( output );
				var phase = _clip is null ? 0f : _time / Math.Max( _clip.Length( parameters ), 0.0001f );
				var first = _clip is null;
				_clip = next;
				_time = Graph.Play.KeepPhase && !first ? (phase - MathF.Floor( phase )) * _clip.Length( parameters ) : 0f;
				_length = _clip.Length( parameters );
				_fadeTime = first ? Math.Max( Graph.Play.BlendTime, 0f ) : Math.Max( Graph.Play.BlendTime, MinimumBlend );
				_fade = _fadeTime > 0f ? 0f : 1f;
			}
			else if ( next is not null )
			{
				// The same clip asked for again restarts it, fading from the pose shown rather than jumping.
				_from.CopyFrom( output );
				_time = 0f;
				_fadeTime = Math.Max( Graph.Play.BlendTime, MinimumBlend );
				_fade = 0f;
			}
		}
		if ( _clip is null )
		{
			output.SetRest( _clips.Skeleton );
			return;
		}
		_scratch.Sample( _clip, _clips.Skeleton, _time, true, parameters );
		if ( _fade < 1f )
		{
			if ( _fade <= 0f )
				CaptureOffsets();
			_fade = Math.Min( 1f, _fade + delta / Math.Max( _fadeTime, 0.0001f ) );
			Inertialize( output );
		}
		else
		{
			output.CopyFrom( _scratch );
		}
	}
}
