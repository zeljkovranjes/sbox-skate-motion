using System.Collections.Generic;

namespace SkateMotion.Core.Animation;

/// <summary>Connects handling phases, retained motion trees and a masked overlay to an already sampled locomotion pose.</summary>
internal sealed class BoardHandlingAnimationLayer : IAnimationChannelTreeFactory
{
	private readonly BoardHandlingController _controller = new();
	private readonly BoardHandlingAnimationTree[,] _trees = new BoardHandlingAnimationTree[ 9, 3 ];
	private readonly AnimationChannelBlend _blend = new( default );
	private readonly AnimationChannelPoseTreeOwner _owner;
	private readonly AnimationOwnerPoseContext _context;
	private float _yaw;
	private readonly bool _streams;

	// A request made while the previous one is still queued behind the motion playing (a reach whose cycle has not
	// begun) waits for it to take over: replacing it then would cut the rest of the reach short and jump the pose
	// straight to the cycle in one frame. Meanwhile the controller sees the waiting motion as just begun.
	private BoardHandlingResult? _waiting;

	// Where the reach clips hold the board against their blend yaw, measured from the clips; null without a board bone.
	private readonly BoardReachMap _reach;

	// The edge of the uncovered side a pickup turns toward (positive: left), kept for the whole pickup.
	private int _reachSide;

	// Where in each release motion (a fraction of it) the hand lets go of the board.
	private readonly float[] _release = new float[ 9 ];

	// A release motion without an authored release lets go of the board here, a fraction of the way through.
	private const float DefaultRelease = 0.45f;

	// The reach asks for the board as it starts into its hold, but the clips' own board is still flying in from metres
	// away until the hold has blended in, and the rider's board is called to where the clips hold theirs: the call is
	// passed on once the hold has taken over (the blend into it has run), with the clips' board in the reaching hand.
	// Seconds left until then; negative with no call waiting.
	private float _callIn = -1f;

	// Once the board is called the reach holds where it is, waiting for the board to fly in, rather than following the
	// flying board round: the yaw it reached at as the call went out (radians, as given to the controller), and the yaw it
	// is being given now.
	private float? _calledYaw;
	private float _givenYaw;

	internal BoardHandlingAnimationLayer( RawAnimationClipSet clips, AnimationGraphDefinition graph,
		AnimationOwnerPoseContext context = null )
	{
		ArgumentNullException.ThrowIfNull( clips );
		ArgumentNullException.ThrowIfNull( graph.BoardHandling );
		_context = context ?? new( new AnimationPoseSamplingContext( 1, false ), 1, true, 0f );
		var key = AnimationAttributeKey.Encode( graph.BoardHandling.YawAttribute );
		BoardHandlingAnimationTree first = null;
		_streams = true;
		var cycles = new List<RawAnimationClip[]>();
		foreach ( var pair in graph.BoardHandling.Motions )
		{
			if ( !Enum.TryParse<BoardHandlingMotion>( pair.Key, out var motion ) || motion == BoardHandlingMotion.None
				|| (int)motion >= _trees.GetLength( 0 ) )
				throw new ArgumentException( "Unknown board handling motion." );
			var leaves = ResolveLeaves( clips, graph.Blends, pair.Value );
			_streams &= Array.TrueForAll( leaves, leaf => leaf.HasPoseStreams );
			if ( motion is BoardHandlingMotion.FrontCycle or BoardHandlingMotion.BackCycle )
				cycles.Add( leaves );
			_release[ (int)motion ] = Release( leaves[ 0 ] );
			var reference = clips.Reference( leaves[ 0 ].ReferenceIndex ).ToArray();
			for ( var slot = 0; slot < _trees.GetLength( 1 ); slot++ )
			{
				var tree = new BoardHandlingAnimationTree( leaves, reference, key, _context );
				_trees[ (int)motion, slot ] = tree;
				first ??= tree;
			}
		}
		if ( first is null )
			throw new ArgumentException( "Board handling needs motion definitions." );
		for ( var motion = BoardHandlingMotion.FrontInto; motion <= BoardHandlingMotion.BackOut; motion++ )
			if ( _trees[ (int)motion, 0 ] is null )
				throw new ArgumentException( "Retrieval needs all front and back phases." );
		first.InUse = true;
		_owner = new AnimationChannelPoseTreeOwner( first, this, _blend );
		_reach = BoardReachMeasurement.Measure( clips, graph.AnchorBone, graph.BoardHandling.YawAttribute, cycles.ToArray() );
	}

	// The fraction of a clip where its hand lets go of the board: the start of its authored release or throw window.
	private static float Release( RawAnimationClip clip )
	{
		if ( clip.Attributes is { } attributes )
		{
			foreach ( var name in new[] { "obreleaseboard", "obthrowboard" } )
			{
				if ( attributes.TryWindow( name, out var start, out _ ) && start is >= 0f and <= 1f )
					return start;
			}
		}
		return DefaultRelease;
	}

	internal bool Active { get; private set; }
	internal float Weight => Active ? _blend.State.Weight : 0f;
	internal BoardHandlingPhase Phase => _controller.Phase;
	internal BoardHandlingMotion Motion { get; private set; }

	/// <summary>
	/// The controller's result for this update, its retrieve request (the call for the board) passed on only once the reach
	/// holds out its hand: the hold it starts has blended in, so the clips' board is in that hand. From then on it stays
	/// raised until the pickup ends, so a reader updated on another schedule cannot miss it.
	/// </summary>
	internal BoardHandlingResult Result { get; private set; }

	/// <summary>
	/// How far the rider should turn (degrees, left positive) for a reach to cover the board being picked up: zero while
	/// one does. The reach clips leave one side behind the rider uncovered.
	/// </summary>
	internal float ReachTurn { get; private set; }

	/// <summary>True once a drop or throw playing has reached the moment its hand lets go of the board.</summary>
	internal bool ReleasePassed => Active && (Motion is BoardHandlingMotion.Drop or BoardHandlingMotion.Throw)
		&& _owner.Elapsed >= _release[ (int)Motion ] * _owner.Duration;

	/// <summary>The overlay pose last prepared, with its per-bone mask.</summary>
	internal SkeletonPose Pose => _owner.Pose;

	/// <summary>Whether every handling clip carries authored pose streams (hands, face).</summary>
	internal bool HasPoseStreams => _streams;

	/// <summary>Adds the playing overlay's pose streams to <paramref name="context"/>, as last prepared.</summary>
	internal void SamplePoseStreams( AnimationPoseStreamContext context ) => _owner.SamplePoseStreams( context );

	internal void Update( in BoardHandlingInput input, float delta, SkeletonPose basePose )
	{
		if ( _waiting is { } waiting && !_owner.HasPending )
		{
			_waiting = null;
			Play( waiting );
		}
		var playback = _waiting is null ? new BoardHandlingPlayback( Active, _owner.Duration - _owner.Elapsed, _owner.Elapsed )
			: new BoardHandlingPlayback( true, 1f, 0f );
		var availableInput = Aimed( input ) with
		{
			DropRequested = input.DropRequested && _trees[ (int)BoardHandlingMotion.Drop, 0 ] is not null,
			ThrowRequested = input.ThrowRequested && _trees[ (int)BoardHandlingMotion.Throw, 0 ] is not null
		};
		Result = HoldCall( _controller.Update( availableInput, playback ), delta );
		if ( Result.Command == BoardHandlingCommand.Stop )
		{
			_waiting = null;
			_blend.Stop( Result.BlendSeconds, false );
		}
		else if ( Result.Command is BoardHandlingCommand.Transition or BoardHandlingCommand.Sequence )
		{
			if ( Active && _owner.HasPending )
				_waiting = Result;
			else
				Play( Result );
		}
		if ( Result.PublishOrientation )
		{
			_yaw = Result.YawDegrees;
			foreach ( var tree in _trees )
				if ( tree is not null && tree.InUse )
					tree.UpdateYaw( _yaw );
		}
		if ( !Active )
			return;
		if ( _blend.Update( delta, _owner ) )
		{
			var completion = default(AnimationPlaybackCompletion);
			_owner.Advance( delta, 0f, ref completion );
			_blend.CompleteOverlayUpdate( completion.Finished );
		}
		foreach ( var tree in _trees )
			if ( tree is not null && tree.InUse )
				tree.UpdateSamplingContext();
		if ( _owner.Prepare( _blend.State.Weight > 0f, _context.ConsumeHistory, _context.SamplingFlags )
			&& _blend.State.Weight > 0f )
			basePose.BlendWeightedToward( _owner.Pose, _blend.State.Weight, false );
		_owner.CleanupCompleted();
		if ( _blend.Finished )
			Active = false;
	}

	// The board is called once the reach's hold has blended in; a pickup given up meanwhile calls nothing.
	private BoardHandlingResult HoldCall( BoardHandlingResult result, float delta )
	{
		var holding = Phase == BoardHandlingPhase.RetrieveCycle;
		if ( _calledYaw is not null && Phase is BoardHandlingPhase.RetrieveCycle or BoardHandlingPhase.RetrieveOut )
			return result with { RetrieveRequested = true };
		if ( result.RetrieveRequested )
		{
			if ( _callIn < 0f )
				_callIn = result.BlendSeconds;
			return result with { RetrieveRequested = false };
		}
		if ( _callIn < 0f )
			return result;
		if ( !holding )
		{
			_callIn = -1f;
			return result;
		}
		_callIn -= delta;
		if ( _callIn > 0f )
			return result;
		_callIn = -1f;
		_calledYaw = _givenYaw;
		return result with { RetrieveRequested = true };
	}

	// A pickup's reach is blended at the yaw whose clips hold the board where it lies, rather than at the board's bearing
	// itself (the clips' own reaches miss by up to a fifth of a right angle); a board on the side no reach covers is
	// reached for at the nearer covered edge while the rider turns toward it. Bearings are taken as the clips see them,
	// mirrored for the other stance.
	private BoardHandlingInput Aimed( in BoardHandlingInput input )
	{
		var picking = Phase is BoardHandlingPhase.RetrieveStart or BoardHandlingPhase.RetrieveInto
			or BoardHandlingPhase.RetrieveCycle or BoardHandlingPhase.RetrieveOut;
		if ( _reach is null || !(picking || input.RetrieveRequested) )
		{
			ReachTurn = 0f;
			_reachSide = 0;
			_calledYaw = null;
			return input;
		}
		if ( input.RetrieveRequested )
			_calledYaw = null;
		if ( _calledYaw is { } held )
		{
			ReachTurn = 0f;
			return input with { YawRadians = held };
		}
		var bearing = input.YawRadians * (180f / MathF.PI);
		if ( input.Mirrored )
			bearing = -bearing;
		var (yaw, turn) = _reach.Solve( bearing, _reachSide );
		if ( turn != 0f && _reachSide == 0 )
			_reachSide = turn > 0f ? 1 : -1;
		// Once the board is in hand there is nothing left to turn for.
		ReachTurn = input.HoldingBoard ? 0f : input.Mirrored ? -turn : turn;
		// The controller blends at minus the bearing it is given, the other way round when mirrored.
		var given = (input.Mirrored ? yaw : -yaw) * (MathF.PI / 180f);
		_givenYaw = given;
		return input with { YawRadians = given };
	}

	private void Play( BoardHandlingResult result )
	{
		var held = result.Motion is BoardHandlingMotion.FrontCycle or BoardHandlingMotion.BackCycle
			or BoardHandlingMotion.FrontOut or BoardHandlingMotion.BackOut;
		// The reach asks for its hold as the reach has its blend time left: queued behind the reach, the hold cut in once the
		// reach ended, 6 inches from where the reach left the hand. Into another motion it blends over the reach's last
		// moments instead; a hold queued behind itself (it loops seamlessly) follows on as before.
		var blends = result.Command == BoardHandlingCommand.Transition
			|| (result.Command == BoardHandlingCommand.Sequence && result.Motion != Motion);
		var request = new AnimationChannelReplacement( (int)result.Motion, 1f, true,
			blends, result.BlendSeconds,
			0.25f, 0.25f, held ? 0xc2000000u : 0x02000000u, true );
		if ( !Active )
		{
			var tree = Create( request );
			_owner.Restart( tree );
			_blend.StartEnvelope( result.BlendSeconds, request.ExitDuration, request.Flags );
			Active = true;
		}
		else if ( _owner.Replace( request ) != AnimationChannelReplaceResult.Success )
		{
			return;
		}
		Motion = result.Motion;
	}

	/// <summary>Leases prebuilt storage so gameplay transitions do not allocate new trees.</summary>
	public IAnimationPlaybackTree Create( AnimationChannelReplacement request )
	{
		for ( var slot = 0; slot < _trees.GetLength( 1 ); slot++ )
		{
			var tree = _trees[ request.Definition, slot ];
			if ( tree is null || tree.InUse )
				continue;
			tree.InUse = true;
			tree.Restart( request.Rate );
			return tree;
		}
		throw new InvalidOperationException( "No free handling tree for this motion." );
	}

	/// <summary>Tree storage is already reserved before publication.</summary>
	public void Commit( IAnimationPlaybackTree tree ) { }

	/// <summary>Releases storage only after its successor has been published.</summary>
	public void Release( IAnimationPlaybackTree tree ) => ((BoardHandlingAnimationTree)tree).InUse = false;

	private static RawAnimationClip[] ResolveLeaves( RawAnimationClipSet clips,
		Dictionary<string, BlendDefinition> blends, string name )
	{
		var direct = clips.Find( name );
		if ( direct is not null )
			return new[] { direct };
		if ( !blends.TryGetValue( name, out var definition ) || definition.Type != "blend"
			|| definition.Children.Count < 2 || definition.Children.Count > 5 )
			throw new ArgumentException( "Handling motions need keyed clips or direct-leaf directional blends." );
		var leaves = new RawAnimationClip[ definition.Children.Count ];
		for ( var child = 0; child < leaves.Length; child++ )
		{
			leaves[ child ] = clips.Find( definition.Children[ child ].Name )
				?? throw new ArgumentException( "Handling blend children must be keyed clips." );
			if ( leaves[ child ].Attributes is null || leaves[ child ].ReferenceIndex != leaves[ 0 ].ReferenceIndex )
				throw new ArgumentException( "Handling children need attributes and a shared reference pose." );
		}
		return leaves;
	}
}
