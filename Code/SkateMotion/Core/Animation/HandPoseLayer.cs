using System.Collections.Generic;

namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;
using Quaternion = System.Numerics.Quaternion;

/// <summary>
/// Finger poses from hand-pose banks. A bank holds one hand's finger bones and a set of poses for them; each clip
/// carries a stream per hand that names the bank pose to show along its length, and blends between poses where the
/// stream changes. The layer adds every bank's bones to the clip skeleton below its hand bone, then each frame turns
/// the streams of what plays (the clip or blend under way, and the board-handling overlay where it covers that
/// hand) into finger poses. The fingers follow those as a critically damped spring, so a change of clip moves them
/// with a gentle start and finish instead of a jump.
/// </summary>
internal sealed class HandPoseLayer
{
	private readonly int _bodyCount;
	private readonly int[] _hand;
	private readonly int[] _first;
	private readonly AnimationPoseStreamKey[] _stream;
	private readonly AnimationIndexedPosePlayback[] _playback;
	private readonly SkeletonPose[] _target;
	private readonly SkeletonPose[] _shown;
	private readonly Vector3[][] _turning;
	private readonly AnimationPoseStreamContext _base = new( Commands );
	private readonly AnimationPoseStreamContext _overlay = new( Commands );
	private readonly Dictionary<RawAnimationClip, (AnimationClipClock Clock, AnimationPoseStreamProducer Producer)> _producers = new();
	private bool _started;

	// Stream commands per hand kept for one frame: a blend of five clips needs about thirty.
	private const int Commands = 64;

	// Pose operands a stream may stack before it reduces (a five-way blend of interpolated keys needs ten).
	private const int Operands = 16;

	/// <summary>Natural frequency (radians per second) of the spring the shown fingers follow the streams' pose with.</summary>
	internal const float Stiffness = 30f;

	/// <param name="body">The clip skeleton the banks' hands belong to.</param>
	/// <param name="banks">One bank per hand, each with a single pose clip.</param>
	internal HandPoseLayer( ClipSkeleton body, IReadOnlyList<HandPoseBank> banks )
	{
		ArgumentNullException.ThrowIfNull( body );
		ArgumentNullException.ThrowIfNull( banks );
		_bodyCount = body.Count;
		_hand = new int[ banks.Count ];
		_first = new int[ banks.Count ];
		_stream = new AnimationPoseStreamKey[ banks.Count ];
		_playback = new AnimationIndexedPosePlayback[ banks.Count ];
		_target = new SkeletonPose[ banks.Count ];
		_shown = new SkeletonPose[ banks.Count ];
		_turning = new Vector3[ banks.Count ][];
		var names = new List<string>( body.Names );
		var parents = new List<int>( body.Parents );
		var positions = new List<Vector3>( body.RestPositions );
		var rotations = new List<Quaternion>( body.RestRotations );
		for ( var i = 0; i < banks.Count; i++ )
		{
			var bank = banks[ i ];
			var clip = bank.Bones.Find( bank.Clip ) ?? throw new ArgumentException( $"Hand pose bank has no clip {bank.Clip}." );
			_hand[ i ] = body.IndexOf( bank.Hand );
			if ( _hand[ i ] < 0 )
				throw new ArgumentException( $"Hand pose bank names a missing hand bone {bank.Hand}." );
			_first[ i ] = names.Count;
			_stream[ i ] = bank.Stream;
			var reference = bank.Bones.Reference( clip.ReferenceIndex ).ToArray();
			_playback[ i ] = new AnimationIndexedPosePlayback( new AnimationIndexedPoseBank( clip ), reference, Operands );
			_target[ i ] = new SkeletonPose( clip.BoneCount );
			_shown[ i ] = new SkeletonPose( clip.BoneCount );
			_turning[ i ] = new Vector3[ clip.BoneCount ];
			for ( var bone = 0; bone < bank.Bones.BoneCount; bone++ )
			{
				var name = bank.Bones.Names[ bone ];
				if ( names.Contains( name ) )
					throw new ArgumentException( $"Hand pose bone {name} is already in the skeleton." );
				names.Add( name );
				var parent = bank.Bones.Parents[ bone ];
				parents.Add( parent < 0 ? _hand[ i ] : _first[ i ] + parent );
				var rest = reference[ bone ];
				positions.Add( new Vector3( rest.Position.X, rest.Position.Y, rest.Position.Z ) );
				rotations.Add( Quaternion.Normalize( rest.Rotation ) );
			}
		}
		Skeleton = new ClipSkeleton( names.ToArray(), parents.ToArray(), positions.ToArray(), rotations.ToArray() );
		Pose = new SkeletonPose( Skeleton.Count );
		Pose.SetRest( Skeleton );
	}

	/// <summary>The clip skeleton with every bank's finger bones added after the body's.</summary>
	internal ClipSkeleton Skeleton { get; }

	/// <summary>The body pose with this frame's fingers, in <see cref="Skeleton"/> order.</summary>
	internal SkeletonPose Pose { get; }

	/// <summary>
	/// Writes <paramref name="body"/> and this frame's fingers into <see cref="Pose"/>. The fingers take the streams of
	/// <paramref name="clip"/> (a clip or blend, null for none) <paramref name="time"/> seconds in, then those of an
	/// active <paramref name="overlay"/> by its weight on each hand bone, and spring toward that over
	/// <paramref name="delta"/> seconds.
	/// </summary>
	internal SkeletonPose Update( SkeletonPose body, AnimationClip clip, float time, AnimationParameters parameters,
		BoardHandlingAnimationLayer overlay, float delta )
	{
		_base.Reset();
		if ( clip is not null )
			Add( clip, time, parameters );
		var covering = overlay is { Active: true, HasPoseStreams: true } && overlay.Weight > 0f;
		if ( covering )
		{
			_overlay.Reset();
			overlay.SamplePoseStreams( _overlay );
		}
		for ( var i = 0; i < _hand.Length; i++ )
		{
			// A stream with nothing in it shows the bank's reference pose.
			_playback[ i ].Prepare( _base, _stream[ i ], int.MaxValue );
			_target[ i ].CopyFrom( _playback[ i ].Pose );
			if ( covering && _playback[ i ].Prepare( _overlay, _stream[ i ], int.MaxValue ) )
			{
				var weight = overlay.Weight * Math.Clamp( overlay.Pose.BoneWeights[ _hand[ i ] ], 0f, 1f );
				if ( weight > 0f )
					_target[ i ].BlendToward( _playback[ i ].Pose, weight );
			}
			if ( _started )
				Spring( _shown[ i ], _turning[ i ], _target[ i ], Math.Max( delta, 0f ) );
			else
				_shown[ i ].CopyFrom( _target[ i ] );
		}
		_started = true;
		Array.Copy( body.Positions, Pose.Positions, _bodyCount );
		Array.Copy( body.Rotations, Pose.Rotations, _bodyCount );
		for ( var i = 0; i < _hand.Length; i++ )
		{
			Array.Copy( _shown[ i ].Positions, 0, Pose.Positions, _first[ i ], _shown[ i ].Positions.Length );
			Array.Copy( _shown[ i ].Rotations, 0, Pose.Rotations, _first[ i ], _shown[ i ].Rotations.Length );
		}
		return Pose;
	}

	// Each finger turns toward its target as a critically damped spring: its offset from the target (and the rate the
	// offset changes at) decays along x(t) = (x0 + (v0 + w x0) t) e^(-w t), exactly for any frame length.
	private static void Spring( SkeletonPose shown, Vector3[] turning, SkeletonPose target, float delta )
	{
		var decay = MathF.Exp( -Stiffness * delta );
		for ( var bone = 0; bone < turning.Length; bone++ )
		{
			var offset = Log( shown.Rotations[ bone ] * Quaternion.Conjugate( target.Rotations[ bone ] ) );
			var drive = turning[ bone ] + offset * Stiffness;
			turning[ bone ] = (turning[ bone ] - drive * (Stiffness * delta)) * decay;
			shown.Rotations[ bone ] = Quaternion.Normalize( Exp( (offset + drive * delta) * decay ) * target.Rotations[ bone ] );
			shown.Positions[ bone ] = target.Positions[ bone ];
		}
	}

	// Rotation vector (axis times angle, radians) of the shorter way round.
	private static Vector3 Log( Quaternion rotation )
	{
		rotation = Quaternion.Normalize( rotation );
		if ( rotation.W < 0f )
			rotation = Quaternion.Negate( rotation );
		var axis = new Vector3( rotation.X, rotation.Y, rotation.Z );
		var sine = axis.Length();
		return sine < 1e-7f ? axis * 2f : axis * (2f * MathF.Atan2( sine, rotation.W ) / sine);
	}

	private static Quaternion Exp( Vector3 turn )
	{
		var angle = turn.Length();
		if ( angle < 1e-7f )
			return Quaternion.Identity;
		var axis = turn / angle;
		return Quaternion.CreateFromAxisAngle( axis, angle );
	}

	// Adds a clip's streams to the base, or a blend's: its clips play in phase, and each later one blends in by its
	// share of the weight so far, as the blend mixes their poses.
	private void Add( AnimationClip clip, float time, AnimationParameters parameters )
	{
		if ( clip.Blend is not { } blend )
		{
			var end = Math.Max( clip.Duration, 0.0001f );
			Sample( clip, clip.RawSampleTime( time, true ) / end );
			return;
		}
		var phase = time / blend.Duration( parameters );
		var total = 0f;
		foreach ( var (leaf, weight) in blend.Weights( parameters ) )
		{
			var leafPhase = leaf.Looping ? phase - MathF.Floor( phase ) : Math.Clamp( phase, 0f, 1f );
			if ( weight <= 0f || !Sample( leaf, leafPhase ) )
				continue;
			total += weight;
			if ( total > weight )
				_base.QueueBlend( weight / total );
		}
	}

	private bool Sample( AnimationClip clip, float phase )
	{
		var raw = clip.RawPose?.Clip;
		if ( raw is null || !raw.HasPoseStreams )
			return false;
		if ( !_producers.TryGetValue( raw, out var stream ) )
		{
			var clock = new AnimationClipClock( raw.FrameCount, raw.FramesPerSecond, raw.RateScale, raw.Looping, raw.PhaseControlled );
			stream = (clock, raw.CreatePoseStreamProducer( clock ));
			_producers[ raw ] = stream;
		}
		stream.Clock.Seek( Math.Clamp( phase, 0f, 1f ) * stream.Clock.Duration );
		stream.Producer.Sample( _base );
		return true;
	}
}
