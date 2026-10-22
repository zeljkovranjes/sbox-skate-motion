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

	// Each bone's shown speed when a change was made (per second; rotations as axis times radians, in the parent's
	// frame), the new clip's own speed then, sampled a moment ahead, and the speed each bone is held back by while it
	// takes the new speed up.
	private readonly Vector3[] _shownPositionRates;
	private readonly Vector3[] _shownTurnRates;
	private readonly SkeletonPose _ahead;
	private readonly Vector3[] _lagPositions;
	private readonly Vector3[] _lagTurns;
	private bool _shownRates;
	private float _lagTime = float.PositiveInfinity;
	private float _lagLength;

	// The poses shown the last two frames, the time between them, and whether that step was smooth (no clip change and
	// no wrap): a change carries the shown motion on from them.
	private SkeletonPose _shown;
	private SkeletonPose _before;
	private float _shownStep;
	private bool _steady;
	private AnimationClip _clip;
	private float _time;

	// The playing clip's length when last updated, seconds.
	private float _length;
	private float _fade = 1f;
	private float _fadeTime;
	private int _played = -1;

	/// <summary>Shortest fade between clips, in seconds, so no change of clip shows as a one-frame snap.</summary>
	internal const float MinimumBlend = 0.1f;

	// Longest clip (seconds) treated as a single held pose: one frame at 30 frames a second.
	private const float HeldPose = 1f / 30f;

	// How close (seconds) to the end of its pass a clip left counts as having run it, for a state keeping its phase:
	// one frame at 30 frames a second, within which an ending test can pass.
	private const float PassEnd = 1f / 30f;

	// Where this frame's pass of the playing clip ended when the frame ran past it (seconds of clip time; NaN otherwise), and
	// how far short of that end (seconds) the frame is shown, inside the pass.
	private float _passEnd = float.NaN;
	private const float EndHeld = 1e-4f;

	// Seconds of playback over which a new clip's starting speed is measured.
	private const float SpeedSample = 1f / 120f;

	// The playing clip's time and blend weights as its flags are read this frame.
	private float _flagTime;
	private AnimationParameters _flagParameters;

	// A timed clip flag of the playing clip, at the time it is being played to this frame.
	private bool? TimedFlag( string flag ) =>
		_flagParameters is null ? null : ClipFlags.Active( _clip, _flagParameters, _flagTime, flag );

	internal StateGraphPlayer( ClipSet clips, StateGraphDefinition definition )
	{
		_clips = clips;
		Graph = new StateGraph( definition ) { ClipFlag = TimedFlag };
		_from = new SkeletonPose( clips.Skeleton.Count );
		_offsetPositions = new Vector3[ clips.Skeleton.Count ];
		_offsetRotations = new Quaternion[ clips.Skeleton.Count ];
		_scratch = new SkeletonPose( clips.Skeleton.Count );
		_shownPositionRates = new Vector3[ clips.Skeleton.Count ];
		_shownTurnRates = new Vector3[ clips.Skeleton.Count ];
		_ahead = new SkeletonPose( clips.Skeleton.Count );
		_lagPositions = new Vector3[ clips.Skeleton.Count ];
		_lagTurns = new Vector3[ clips.Skeleton.Count ];
		_shown = new SkeletonPose( clips.Skeleton.Count );
		_before = new SkeletonPose( clips.Skeleton.Count );
		_from.SetRest( clips.Skeleton );
	}

	internal StateGraph Graph { get; }

	/// <summary>Name of the clip or blend playing.</summary>
	internal string ClipName => _clip?.Name;

	/// <summary>Clip or blend playing, or null before the first.</summary>
	internal AnimationClip Clip => _clip;

	/// <summary>Seconds into <see cref="Clip"/> its pose was last sampled at.</summary>
	internal float Time => _time;

	/// <summary>Multiplies the playing state's speed (1 = as authored), e.g. to match a pushing foot to the ground.</summary>
	internal float RateScale { get; set; } = 1f;

	/// <summary>
	/// The board's bone (its up along its own Z), or -1 for none. A board is the same end for end, so a change that finds
	/// the new clip's board nearly the other way round from the board shown turns the board shown over to its other end
	/// rather than back (see <see cref="CaptureOffsets"/>).
	/// </summary>
	internal int Board { get; set; } = -1;

	// A half turn about a bone's own up (Z), and how near (radians) the board shown turned so must come to the new clip's
	// board for it to be taken that way round: a third of a half turn, so a board held across the rider (a shifty, a board
	// adjust, at 90-120 degrees) still goes back the way it came.
	private static readonly Quaternion HalfTurn = new( 0f, 0f, 1f, 0f );
	private const float OtherEnd = MathF.PI / 3f;

	// Transitions are inertialized rather than crossfaded from a frozen pose: when a clip starts, each bone's
	// difference from the pose shown last (position, and rotation along the shortest arc) is recorded and then
	// eased out while the new clip plays its own motion. A fast-moving bone (a flipping board) therefore keeps
	// moving through the change, and no blend ever passes between two nearly opposite rotations, where blending
	// flips to the other way round in a single frame.
	// A shove-it's clip ends with its board turned half round under the rider, and the clip after it has its board the way
	// it started: the offset between them, eased out the shorter way, spun the board back across the whole spin (173
	// degrees at up to 900 degrees a second after a pop shove-it, landing with it still 30-60 degrees round, and a varial
	// kickflip's tipped 40 degrees over as it went). A board is the same end for end, so one the new clip has within
	// OtherEnd of its other end is taken as turned on to that end, and only what is left of the spin eases out.
	private void CaptureOffsets( float speed, AnimationParameters parameters )
	{
		var widest = 0f;
		for ( var bone = 0; bone < _offsetRotations.Length; bone++ )
		{
			if ( bone == Board && Between( _from.Rotations[ bone ] * HalfTurn, _scratch.Rotations[ bone ] ) < OtherEnd )
				_from.Rotations[ bone ] = Quaternion.Normalize( _from.Rotations[ bone ] * HalfTurn );
			_offsetPositions[ bone ] = _from.Positions[ bone ] - _scratch.Positions[ bone ];
			var offset = Quaternion.Normalize( _from.Rotations[ bone ] * Quaternion.Conjugate( Quaternion.Normalize( _scratch.Rotations[ bone ] ) ) );
			_offsetRotations[ bone ] = offset.W < 0f ? Quaternion.Negate( offset ) : offset;
			// A pose shown before any was real (all zero) has no turn to measure.
			var turn = 2f * MathF.Acos( Math.Min( 1f, _offsetRotations[ bone ].W ) );
			if ( float.IsFinite( turn ) )
				widest = MathF.Max( widest, turn );
		}
		// A change far from the pose shown takes longer: the offset falls off along a smooth curve whose fastest turn is
		// half again its average, so the fade lasts long enough that no bone turns faster than FastestTurn on the way (a
		// slide started from a spin's pose whipped the head round in a few frames).
		_fadeTime = MathF.Max( _fadeTime, 1.5f * widest / FastestTurn );
		CaptureLag( speed, parameters );
	}

	// Fastest a fade turns any bone, radians per second: 900 degrees, a quick swing of an arm.
	private const float FastestTurn = 15.7f;

	// A state leaving as its clip ends does so a few hundredths of a second before the end (its test's Within), and the
	// new clip took over from there: a flip's clip catches its board in its last 0.05-0.1 s, so left 0.05 s early the board
	// stopped 25-65 degrees short of the catch and drifted the rest of the way over the fade (a varial's still tipped 31
	// degrees as it landed). The board plays the outgoing clip out to its end under the change: what the outgoing clip still
	// turns and moves it is added to its offset from the new clip, frame by frame, as the offset eases out. Only a clip left
	// within PlayOut seconds of the end of its pass is played out.
	private AnimationClip _outgoing;
	private float _outgoingTime;
	private float _outgoingEnd;
	private float _outgoingSpeed;
	private SkeletonPose _outgoingPose;
	private SkeletonPose _outgoingNext;
	private const float PlayOut = 0.1f;

	private void PlayOutFrom( AnimationClip clip, float time, float length, float speed, AnimationParameters parameters )
	{
		_outgoing = clip;
		_outgoingTime = time;
		_outgoingEnd = length - EndHeld;
		_outgoingSpeed = speed;
		_outgoingPose ??= new SkeletonPose( _clips.Skeleton.Count );
		_outgoingNext ??= new SkeletonPose( _clips.Skeleton.Count );
		_outgoingPose.Sample( clip, _clips.Skeleton, MathF.Min( time, _outgoingEnd ), true, parameters );
	}

	private void PlayOutBoard( float delta, AnimationParameters parameters )
	{
		if ( _outgoing is null || Board < 0 || Board >= _offsetRotations.Length )
			return;
		var time = MathF.Min( _outgoingTime + delta * _outgoingSpeed, _outgoingEnd );
		_outgoingNext.Sample( _outgoing, _clips.Skeleton, time, true, parameters );
		_offsetPositions[ Board ] += _outgoingNext.Positions[ Board ] - _outgoingPose.Positions[ Board ];
		var step = Quaternion.Normalize( Quaternion.Normalize( _outgoingNext.Rotations[ Board ] )
			* Quaternion.Conjugate( Quaternion.Normalize( _outgoingPose.Rotations[ Board ] ) ) );
		_offsetRotations[ Board ] = Quaternion.Normalize( step * _offsetRotations[ Board ] );
		(_outgoingPose, _outgoingNext) = (_outgoingNext, _outgoingPose);
		_outgoingTime = time;
		if ( time >= _outgoingEnd )
			_outgoing = null;
	}

	// The offsets keep the pose through a change but not the speed: the new clip's own motion shows at full speed from
	// the frame it starts, so a bone it moves faster than the bone was moving (a push cycle that opens with a quick
	// lead-in, which a crossfade would have hidden under a low weight) jumped to that speed in one frame, and one it
	// moves slower stopped dead. Each bone instead starts at the speed it was shown moving, and the difference dies
	// away over the fade. The difference carried is never more than the new clip's own speed, so a bone that clip
	// holds still is never carried past it, and no bone turns back. It is taken in the new clip's own frame, under the
	// change's offset, where the clip's motion is: carried over the offset instead, a turn the clip makes about the
	// bone (a knee twisting about the shin as a push begins) would come out as a swing of the bone, as far off as the
	// offset turns it.
	private void CaptureLag( float speed, AnimationParameters parameters )
	{
		_lagTime = float.PositiveInfinity;
		if ( !_shownRates || !(_fadeTime > 0f) || !(MathF.Abs( speed ) > 0f) )
			return;
		_ahead.Sample( _clip, _clips.Skeleton, _time + SpeedSample * speed, true, parameters );
		for ( var bone = 0; bone < _lagTurns.Length; bone++ )
		{
			_lagPositions[ bone ] = Lag( (_ahead.Positions[ bone ] - _scratch.Positions[ bone ]) / SpeedSample, _shownPositionRates[ bone ] );
			var shown = Vector3.Transform( _shownTurnRates[ bone ], Quaternion.Conjugate( _offsetRotations[ bone ] ) );
			_lagTurns[ bone ] = Lag( Turn( _ahead.Rotations[ bone ], _scratch.Rotations[ bone ] ) / SpeedSample, shown );
		}
		_lagTime = 0f;
		_lagLength = Math.Min( _fadeTime, MinimumBlend );
	}

	// The shown speed less the new clip's, cut down to the length of the new clip's speed.
	private static Vector3 Lag( Vector3 clip, Vector3 shown )
	{
		var difference = shown - clip;
		var most = clip.Length();
		var length = difference.Length();
		return length > most ? difference * (most / length) : difference;
	}

	// The difference dies away over T, the shortest fade (or the fade, if shorter), as lag * T * (s - 6s^3 + 8s^4 - 3s^5)
	// with s = t / T: the bone starts at its shown speed and acceleration, drifts a little off the new clip, comes back,
	// and is on the clip with both matched by then. Its speed back is at most about half the difference, so the bone
	// never moves against the new clip's motion, and a short clip (a push's contact, an intro) is on its own motion well
	// before it ends, so the change after it carries that motion on rather than the catching up.
	private void ApplyLag( SkeletonPose clip, float delta )
	{
		if ( !(_lagTime < _lagLength) )
			return;
		var s = _lagTime / _lagLength;
		var share = _lagLength * s * (1f + s * s * (-6f + s * (8f - 3f * s)));
		for ( var bone = 0; bone < _lagTurns.Length; bone++ )
		{
			clip.Positions[ bone ] += _lagPositions[ bone ] * share;
			clip.Rotations[ bone ] = Quaternion.Normalize( Rotation( _lagTurns[ bone ] * share ) * clip.Rotations[ bone ] );
		}
		_lagTime += delta;
	}

	// The rotation vector (axis times angle, radians) turning one rotation into another, the shorter way round.
	private static Vector3 Turn( Quaternion to, Quaternion from )
	{
		var turn = Quaternion.Normalize( Quaternion.Normalize( to ) * Quaternion.Conjugate( Quaternion.Normalize( from ) ) );
		if ( turn.W < 0f )
			turn = Quaternion.Negate( turn );
		var axis = new Vector3( turn.X, turn.Y, turn.Z );
		var sine = axis.Length();
		return sine < 1e-7f ? axis * 2f : axis * (2f * MathF.Atan2( sine, turn.W ) / sine);
	}

	// The angle (radians) between two rotations, the shorter way round.
	private static float Between( Quaternion a, Quaternion b ) =>
		2f * MathF.Acos( Math.Min( 1f, MathF.Abs( Quaternion.Dot( Quaternion.Normalize( a ), Quaternion.Normalize( b ) ) ) ) );

	private static Quaternion Rotation( Vector3 turn )
	{
		var angle = turn.Length();
		return angle < 1e-7f ? Quaternion.Identity : Quaternion.CreateFromAxisAngle( turn / angle, angle );
	}

	// Fades start from where the shown motion would be this frame, carried on from the last two poses shown, not from the
	// pose shown last frame: from that, the frame of a change would show the same pose twice and the motion would stop for
	// a frame at every change. Sampling the outgoing clip again instead would see this frame's parameters, which a change
	// often comes with (a landing's impact), and jump to that clip's pose at the new weights.
	private void Continue( bool steady, float delta, float previousStep )
	{
		_from.CopyFrom( _shown );
		_lagTime = float.PositiveInfinity;
		_shownRates = steady && previousStep > 0f;
		if ( !_shownRates )
			return;
		var share = Math.Clamp( delta / previousStep, 0f, MaximumCarry );
		for ( var bone = 0; bone < _from.Positions.Length; bone++ )
		{
			_shownPositionRates[ bone ] = (_shown.Positions[ bone ] - _before.Positions[ bone ]) / previousStep;
			_shownTurnRates[ bone ] = Turn( _shown.Rotations[ bone ], _before.Rotations[ bone ] ) / previousStep;
			_from.Positions[ bone ] += (_shown.Positions[ bone ] - _before.Positions[ bone ]) * share;
			var turn = Quaternion.Normalize( _shown.Rotations[ bone ] * Quaternion.Conjugate( Quaternion.Normalize( _before.Rotations[ bone ] ) ) );
			if ( turn.W < 0f )
				turn = Quaternion.Negate( turn );
			_from.Rotations[ bone ] = Quaternion.Normalize( Quaternion.Slerp( Quaternion.Identity, turn, share ) * _shown.Rotations[ bone ] );
		}
	}

	// Most frames of the last step's motion a change carries on over (a long frame after a short one).
	private const float MaximumCarry = 2f;

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
		(_before, _shown) = (_shown, _before);
		_shown.CopyFrom( output );
		var steady = _steady;
		var previousStep = _shownStep;
		_shownStep = delta;
		_steady = _clip is not null;
		// A single held pose (a frame long) lasts as long as its fade in, so a state waiting for it to end shows it first
		// rather than ending, and starting again, every frame. A clip with motion keeps its own length even when it is
		// shorter than its fade (a push's contact): stretched, it would loop back to its start until the fade was over.
		var length = _clip?.Length( parameters ) ?? float.PositiveInfinity;
		Graph.ClipLength = length <= HeldPose ? Math.Max( length, _fadeTime ) : length;
		// A blend's length follows its weights. Its clips play in phase (time over length), so when the length changes
		// the elapsed time is rescaled to keep the phase: otherwise a weight changing late in a long cycle would
		// jump every clip to another point in its motion.
		if ( _clip is not null && _length > 0f && float.IsFinite( Graph.ClipLength ) && Graph.ClipLength != _length )
			_time *= Graph.ClipLength / _length;
		var speed = (Graph.Play?.Speed ?? 1f) * RateScale;
		var nextTime = _time + delta * speed;
		_passEnd = float.NaN;
		if ( float.IsFinite( Graph.ClipLength ) && Graph.ClipLength > 0f
			&& MathF.Floor( nextTime / Graph.ClipLength ) != MathF.Floor( _time / Graph.ClipLength ) )
		{
			_steady = false;
			if ( nextTime > _time )
				_passEnd = MathF.Floor( nextTime / Graph.ClipLength ) * Graph.ClipLength;
		}
		var nextFade = _fadeTime > 0f ? Math.Min( 1f, _fade + delta / Math.Max( _fadeTime, 0.0001f ) ) : 1f;
		// Conditions observe the playback being produced this frame, including any retained phase.
		// A separately accumulated state timer can lag by one frame at a clip's wrap boundary.
		_flagTime = nextTime;
		_flagParameters = parameters;
		Graph.Update( facts, delta, new AnimationPlaybackStatus( Graph.ClipLength - nextTime, nextFade < 1f ) );
		_time = nextTime;
		_length = Graph.ClipLength;
		if ( Graph.PlayCount != _played )
		{
			_played = Graph.PlayCount;
			_passEnd = float.NaN;
			var next = Graph.Clip is { } name ? _clips.Find( name ) : null;
			_outgoing = null;
			if ( next is not null && !ReferenceEquals( next, _clip ) || next is not null && _clip is null )
			{
				Continue( steady, delta, previousStep );
				_steady = false;
				var outgoing = _clip is null ? 0f : Math.Max( _clip.Length( parameters ), 0.0001f );
				var phase = _clip is null ? 0f : _time / outgoing;
				var first = _clip is null;
				phase -= MathF.Floor( phase );
				if ( !first && Board >= 0 && speed > 0f && (1f - phase) * outgoing <= PlayOut )
					PlayOutFrom( _clip, phase * outgoing, outgoing, speed, parameters );
				// A clip left within a frame of the end of its pass has run it: the next carries on from the start of its
				// own, not from just short of its end (where it would wrap round to its start a frame later).
				if ( (1f - phase) * outgoing <= PassEnd )
					phase = 0f;
				_clip = next;
				_time = Graph.Play.KeepPhase && !first ? phase * _clip.Length( parameters ) : 0f;
				_length = _clip.Length( parameters );
				_fadeTime = first ? Math.Max( Graph.Play.BlendTime, 0f ) : Math.Max( Graph.Play.BlendTime, MinimumBlend );
				_fade = _fadeTime > 0f ? 0f : 1f;
			}
			else if ( next is not null )
			{
				// The same clip asked for again restarts it, fading from where it had got to rather than jumping.
				Continue( steady, delta, previousStep );
				_steady = false;
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
		// The frame a clip runs past the end of its pass, in a state that leaves as its clip ends, it is shown at that end:
		// the state may only leave on the next frame (a guard waiting on more than the ending), and wrapped round the clip
		// showed its start for that frame (stepping back from a drop-in, the body and board jumped to the clip's start and
		// back). A clip that simply loops goes on round.
		var ending = float.IsFinite( _passEnd ) && Graph.AwaitsClipEnd;
		_scratch.Sample( _clip, _clips.Skeleton, ending ? _passEnd - EndHeld : _time, true, parameters );
		if ( _fade < 1f )
		{
			if ( _fade <= 0f )
				CaptureOffsets( (Graph.Play?.Speed ?? 1f) * RateScale, parameters );
			else
				PlayOutBoard( delta, parameters );
			_fade = Math.Min( 1f, _fade + delta / Math.Max( _fadeTime, 0.0001f ) );
			ApplyLag( _scratch, delta );
			Inertialize( output );
		}
		else
		{
			ApplyLag( _scratch, delta );
			output.CopyFrom( _scratch );
		}
	}
}
