using System.Collections.Generic;
using System.Linq;
using Sandbox;
using SkateMotion.Core.Animation;

namespace SkateMotion.Engine;

/// <summary>
/// Plays clip-set animation on a skinned model: the director picks motions from gameplay, the graph samples
/// them, and the retargeter writes final bone transforms as overrides. Any humanoid model works: its bones are
/// found by name and hierarchy, and its facing and size are measured from the rest pose. The model is placed at
/// the clip root, which is the board frame while riding and the rider's feet otherwise.
/// </summary>
internal sealed class ClipRiderAnimation
{
	private readonly SkinnedModelRenderer _renderer;
	private readonly GameObject _rider;
	private readonly GameObject _board;
	private readonly MotionGraph _graph;
	private readonly StateGraphPlayer _states;
	private readonly BoundStateGraphTreePlayer _treeStates;
	private readonly StateFacts _facts = new();
	private readonly RiderStateFacts _riderFacts = new();
	private readonly RiderAnimationDirector _director;
	private readonly AnimationParameters _parameters = new();
	private readonly SkeletonPose _pose;
	private readonly BoardHandlingAnimationLayer _handling;
	private readonly CarriedBoardAttachment _carryAttachment;
	private RiderActivity _previousActivity;
	private readonly PoseRetargeter _retargeter;
	private readonly int _anchor;
	private readonly int _carryHand;
	private readonly int _mirroredCarryHand;
	private readonly System.Numerics.Vector3[] _positions;
	private readonly System.Numerics.Quaternion[] _rotations;
	private readonly (TwoBoneIk Leg, int Ankle, int Ball, float AnkleHeight, float BallHeight, System.Numerics.Vector3[] Sole, float ShinRadius)[] _feet;
	private readonly float[] _footWeight;

	// Current push of each foot out of the deck (inches along the deck's up axis).
	private readonly float[] _deckPush;

	// Current ground-plant shift of each foot (inches, up positive).
	private readonly float[] _plant;

	// Ground lock of each foot: whether it is locked, how fully (eased), and where it touched down (world).
	private readonly bool[] _locked;
	private readonly System.Numerics.Vector3[] _kneeHint;
	private readonly System.Numerics.Vector3[] _ball;
	private readonly bool[] _ballSeen;
	private readonly float[] _lock;
	private readonly Vector3[] _lockPoint;
	private readonly System.Numerics.Vector3[] _footCorrection;

	// Size of an in-air foot correction (inches) that is trusted fully, and where it is ignored; and how fast a
	// correction follows its target (per second).
	private const float TrustedCorrection = 2f;
	private const float IgnoredCorrection = 6f;
	private const float CorrectionRate = 30f;

	private readonly IBoardDisplay _skateboard;
	private float _boardShown;
	private Transform _animatedBoard;
	private readonly HashSet<string> _stanceSwapClips;
	private bool _shownSwitch;
	private int _plays;
	private readonly System.Numerics.Vector3[] _shownPositions;
	private readonly System.Numerics.Quaternion[] _shownRotations;
	private readonly System.Numerics.Vector3[] _flipPositions;
	private readonly System.Numerics.Quaternion[] _flipRotations;
	private Transform _shownModel;
	private bool _shownMirror;
	private bool _shownValid;
	private float _flipTime = FlipEase;

	// How long (seconds) showing the other stance eases in, and how long turning the body upright on a wall takes.
	private const float FlipEase = 0.3f;
	private const float WallTurn = 0.2f;
	private float _wallWeight;
	private float _settle;

	// Furthest (inches) the shown board gives way to the feet, and how fast it eases back (per second).
	private const float SettleLimit = 3f;
	private const float SettleRelease = 10f;

	// Points along each shin kept out of the shown board in the air.
	private const int ShinSamples = 4;
	private float _anchored;
	private readonly BodyClearance[] _clearance = new BodyClearance[ 2 ];
	private readonly Dictionary<int, TwoBoneIk> _arms = new();

	// Inches the held board is moved out to clear the body.
	private float _clearancePush;

	// Rate at which a clearance turn the body no longer needs eases back out.
	private const float ClearanceRelease = 8f;

	// The rider's hands measured from its mesh, by hand bone: direction to the fingertips in the hand bone's frame,
	// length and thickness (inches).
	private readonly Dictionary<int, (System.Numerics.Vector3 Axis, float Length, float Radius)> _hands = new();

	// How far (deck units, along its up axis) the held deck is moved to keep the holding hand out of it: positive puts
	// the deck above the hand.
	private float _handPush;

	// Rate at which a hand clearance no longer needed eases back out (per second), and how much shorter (in hand
	// thicknesses) the other side of the deck must be before the deck changes sides of the hand.
	private const float HandRelease = 8f;
	private const float HandSideBias = 1f;

	// Points along the holding hand, wrist to fingertips, tested against the deck.
	private const int HandSamples = 6;

	// The give of a hand catching the flying board: the board's arrival speed carries hand and board on, easing back
	// over this time constant (seconds), at most this share of the forearm's length.
	private const float CatchTime = 0.06f;
	private const float CatchReach = 0.3f;

	// Board position and velocity in the clip frame while it flies in, and the catch under way (seconds since the
	// board arrived, its velocity then; a negative time means none).
	private System.Numerics.Vector3 _flightPosition;
	private System.Numerics.Vector3 _flightVelocity;
	private bool _flightSeen;
	private float _catchElapsed = -1f;
	private System.Numerics.Vector3 _catchVelocity;
	private Transform _model;
	private bool _lastMirror;

	// Seconds over which the clip's board bone eases onto the real deck when getting on, and off when getting off.
	private const float AnchorBlend = 0.25f;

	// Seconds for the shown board to follow the animation's board after take-off, and to return to physics on landing.
	private const float BoardTakeOver = 0.04f;
	private const float BoardHandBack = 0.15f;

	private const float MaximumLift = 6f;

	// Inches past the deck's edge over which its push on a foot fades out.
	private const float EdgeFade = 1.5f;

	// Inches kept between a sole and the deck surface.
	private const float SoleMargin = 0.05f;

	// Sole gap above the ground (inches) within which a foot beside the deck is set on the ground, and the distance
	// over which that hold then fades as the clip lifts the foot.
	private const float PlantSnap = 2f;
	private const float PlantFade = 1.5f;

	// How fast (per second) a foot settles onto the ground or lets go of it.
	private const float PlantRate = 12f;

	// How fast (per second) a foot's ground lock lets go, and the furthest (inches) the body may carry the leg from
	// a locked foot before the held spot is dragged along.
	private const float LockRelease = 8f;

	// Soft reach: past this share of the leg's length a planted foot's distance from the hip is eased toward full
	// length instead of reaching it, so the knee never snaps straight (a two-bone leg's knee angle changes without
	// bound as it nears full stretch).
	private const float SoftReach = 0.9f;
	private const float LockReach = 16f;

	// Gap above the ground (inches) at which a descending foot is pinned, so it does not skid the last of the way.
	private const float LockGap = 3f;

	// Push playback-rate matching: slowest sweep worth matching (inches per second), the rate limits, and how fast
	// the rate follows (per second).
	private const float MinimumSweep = 5f;
	private const float MinimumRate = 0.6f;
	private const float MaximumRate = 3f;
	private const float RateSmoothing = 15f;

	// How fast (per second) a foot's push out of the deck eases off once it is no longer needed.
	private const float DeckPushRelease = 12f;

	// How far above and below the foot (inches) the ground is looked for.
	private const float PlantReach = 24f;

	// Seconds for a foot's pull to the board to fade fully in or out.
	private const float FootBlend = 0.15f;

	// State action that releases the feet from the board (grab tweaks, handplants).
	private const string ReleaseFeet = "attribute:disable-feet-target-ik";

	internal ClipRiderAnimation( SkinnedModelRenderer renderer, RiderAnimationAssets assets, GameObject rider, GameObject board )
	{
		_renderer = renderer;
		_rider = rider;
		_board = board;
		_graph = new MotionGraph( assets.Clips, assets.Graph );
		if ( assets.Graph.BasePlayback is not null )
			_treeStates = new BoundStateGraphTreePlayer( assets.Clips, assets.Graph );
		else if ( assets.Graph.StateMachine is not null )
			_states = new StateGraphPlayer( assets.Clips, assets.Graph.StateMachine );
		_director = new RiderAnimationDirector( string.Equals( assets.Graph.Stance, "goofy", StringComparison.OrdinalIgnoreCase ) );
		_pose = new SkeletonPose( assets.Clips.Skeleton.Count );
		if ( assets.Graph.BoardHandling is not null )
			_handling = new BoardHandlingAnimationLayer( assets.Clips.RawPoses, assets.Graph );
		_anchor = string.IsNullOrEmpty( assets.Graph.AnchorBone ) ? -1 : assets.Clips.Skeleton.IndexOf( assets.Graph.AnchorBone );
		var bones = renderer.Model.Bones.AllBones.OrderBy( bone => bone.Index ).ToArray();
		var names = bones.Select( bone => bone.Name ).ToArray();
		var parents = bones.Select( bone => bone.Parent?.Index ?? -1 ).ToArray();
		var bindPositions = bones.Select( bone => SkateUnits.ToCore( bone.LocalTransform.Position ) ).ToArray();
		var bindRotations = bones.Select( bone => SkateUnits.ToCore( bone.LocalTransform.Rotation ) ).ToArray();
		Rig = new HumanoidBones( names, parents, bindPositions );
		if ( !Rig.Complete )
			throw new InvalidOperationException( $"{renderer.Model.Name} has no recognisable humanoid legs, hips and head." );
		_retargeter = new PoseRetargeter( assets.Clips.Skeleton, names, parents, bindPositions, bindRotations, Rig.Map(),
			Rig.Facing, Sole( renderer.Model.Bounds, Rig.Facing ) );
		_carryHand = assets.Graph.CarryHand switch
		{
			"left_hand" => Rig[ "left_hand" ],
			"right_hand" => Rig[ "right_hand" ],
			_ => -1
		};
		_mirroredCarryHand = assets.Graph.CarryHand switch
		{
			"left_hand" => Rig[ "right_hand" ],
			"right_hand" => Rig[ "left_hand" ],
			_ => -1
		};
		var hasCarryTarget = _anchor >= 0 && _carryHand >= 0 && _mirroredCarryHand >= 0
			&& _retargeter.SourceBone( _carryHand, false ) >= 0
			&& _retargeter.SourceBone( _mirroredCarryHand, true ) >= 0;
		if ( assets.RetrievalProfile is not null && hasCarryTarget && assets.Rig is not null
			&& assets.Rig.SolverToClip[ 0 ] == _anchor )
			_carryAttachment = new( assets.Rig, assets.Graph.CarryHand == "left_hand" );
		RetrievalProfile = _handling is not null && _carryAttachment is not null ? assets.RetrievalProfile : null;
		_positions = new System.Numerics.Vector3[ bones.Length ];
		_rotations = new System.Numerics.Quaternion[ bones.Length ];
		_shownPositions = new System.Numerics.Vector3[ bones.Length ];
		_shownRotations = new System.Numerics.Quaternion[ bones.Length ];
		_flipPositions = new System.Numerics.Vector3[ bones.Length ];
		_flipRotations = new System.Numerics.Quaternion[ bones.Length ];
		_stanceSwapClips = new HashSet<string>( assets.Graph.StanceSwapClips ?? new() );
		_feet = new[] { "left", "right" }.Select( side => Foot( parents, side ) ).Where( foot => foot.Leg is not null ).ToArray();
		_footWeight = new float[ _feet.Length ];
		_plant = new float[ _feet.Length ];
		_locked = new bool[ _feet.Length ];
		_kneeHint = new System.Numerics.Vector3[ _feet.Length ];
		_ball = new System.Numerics.Vector3[ _feet.Length ];
		_ballSeen = new bool[ _feet.Length ];
		_lock = new float[ _feet.Length ];
		_lockPoint = new Vector3[ _feet.Length ];
		_deckPush = new float[ _feet.Length ];
		_footCorrection = new System.Numerics.Vector3[ _feet.Length ];
		foreach ( var side in new[] { "left", "right" } )
		{
			var (upper, lower, end) = (Rig[ $"{side}_arm" ], Rig[ $"{side}_forearm" ], Rig[ $"{side}_hand" ]);
			if ( upper >= 0 && lower >= 0 && end >= 0 )
				_arms[ end ] = new TwoBoneIk( parents, upper, lower, end );
			if ( end >= 0 && BodyClearance.Hand( renderer.Model, Rig, side ) is { } hand )
				_hands[ end ] = (SkateUnits.ToCore( hand.Axis ), hand.Length, hand.Radius);
		}
		// Interfaces are not component types to the lookup, so the board's components are filtered instead.
		_skateboard = board.Components.GetAll<Component>( FindMode.EverythingInSelf ).OfType<IBoardDisplay>().FirstOrDefault();
	}

	/// <summary>The humanoid bones found on the model.</summary>
	internal HumanoidBones Rig { get; }

	/// <summary>The model's bone name for a humanoid role (hips, head, left_shoulder...), or null.</summary>
	internal string BoneName( string role )
	{
		var bone = Rig[ role ];
		return bone < 0 ? null : _renderer.Model.Bones.AllBones.FirstOrDefault( b => b.Index == bone )?.Name;
	}

	// The soles are the bottom of the model's bounds, measured along its up axis.
	private static float Sole( BBox bounds, System.Numerics.Quaternion facing )
	{
		var inverse = System.Numerics.Quaternion.Conjugate( facing );
		return bounds.Corners.Min( corner => System.Numerics.Vector3.Transform( SkateUnits.ToCore( corner ), inverse ).Z );
	}

	private (TwoBoneIk Leg, int Ankle, int Ball, float AnkleHeight, float BallHeight, System.Numerics.Vector3[] Sole, float ShinRadius) Foot( int[] parents, string side )
	{
		var upper = Rig[ $"{side}_upleg" ];
		var lower = Rig[ $"{side}_leg" ];
		var ankle = Rig[ $"{side}_foot" ];
		var ball = Rig[ $"{side}_toe" ];
		if ( upper < 0 || lower < 0 || ankle < 0 )
			return (null, -1, -1, 0f, 0f, [], 0f);
		if ( ball < 0 )
			ball = ankle;
		var bones = _renderer.Model.Bones.AllBones;
		var shin = BodyClearance.Radius( _renderer.Model, bones.First( bone => bone.Index == lower ).LocalTransform.Position,
			bones.First( bone => bone.Index == ankle ).LocalTransform.Position );
		return (new TwoBoneIk( parents, upper, lower, ankle ), ankle, ball, _retargeter.BindPosition( ankle ).Z,
			_retargeter.BindPosition( ball ).Z, FootSole.Measure( _renderer.Model, _retargeter, ankle, ball ), shin);
	}

	/// <summary>Name of the graph state currently playing.</summary>
	internal string State => _treeStates?.Player.Graph.State ?? _states?.Graph.State ?? _graph.State;

	/// <summary>Name of the clip playing now.</summary>
	internal string Motion => _treeStates?.Player.ClipName ?? _states?.ClipName ?? _graph.ClipName;

	/// <summary>
	/// Lowest gap (inches) between any part of either foot or shin and the shown deck, on the side of the deck the foot is on,
	/// after this frame's foot placement; negative means a foot is inside the deck, null when no foot is over it.
	/// </summary>
	internal float? FootDeckGap { get; private set; }

	/// <summary>True while a foot stands on the ground beside the board (held where it touched down).</summary>
	internal bool FootOnGround => System.Array.IndexOf( _locked, true ) >= 0;

	/// <summary>Authored board pose aligned to the carrying hand after retargeting, when configured.</summary>
	internal Transform? CarriedBoardPose { get; private set; }
	internal BoardRetrievalProfile RetrievalProfile { get; }
	internal bool RetrieveRequested => _handling?.Result.RetrieveRequested ?? false;

	internal void Request( IReadOnlyDictionary<string, string> tags, float seconds ) => _director.Request( tags, seconds );

	internal IReadOnlyList<string> Actions( string family, string style ) => _graph.Actions( family, style );

	internal void Update( in RiderAnimationInput input, float delta, Transform root )
	{
		if ( input.Activity != RiderActivity.Riding )
		{
			Switch = false;
			_shownSwitch = false;
		}
		// Riding switch the rider faces the board's tail: rolling forwards for that stance is the board rolling backwards.
		// The clips show the stance they started in (a turn-round clip ends in the new stance), so their mirroring and
		// facing change only when the next clip starts.
		var stance = Switch ? input with { Speed = -input.Speed, Switch = true } : input;
		_director.Update( _shownSwitch ? stance with { Goofy = !input.Goofy } : stance, delta, _parameters );
		var mirror = _parameters.Number( "mirror" ) > 0.5f;
		var stanceMirror = mirror != (_shownSwitch != Switch);
		if ( _treeStates is not null && !_director.Requesting )
		{
			_riderFacts.Fill( _facts, stance, stanceMirror, delta );
			_treeStates.Update( _facts, _parameters, delta, _pose );
		}
		else if ( _states is not null && !_director.Requesting )
		{
			_riderFacts.Fill( _facts, stance, stanceMirror, delta );
			_states.Update( _facts, _parameters, delta, _pose );
		}
		else
		{
			_graph.Update( _parameters, delta, _pose );
		}
		if ( TrackStance() )
		{
			mirror = !mirror;
			_parameters.Set( "mirror", mirror );
		}
		if ( _shownSwitch )
			root = new Transform( root.Position, root.Rotation * Rotation.FromYaw( 180f ) );
		if ( _handling is not null )
		{
			var offset = root.Rotation.Inverse * (_board.WorldPosition - root.Position);
			var yaw = MathF.Atan2( offset.y, offset.x );
			var pitch = MathF.Atan2( offset.z, MathF.Sqrt( offset.x * offset.x + offset.y * offset.y ) );
			var changed = input.Activity != _previousActivity;
			var handlingInput = new BoardHandlingInput( false, input.HoldingBoard, false,
				input.Activity == RiderActivity.PickingUp || input.HoldingBoard,
				changed && input.Activity == RiderActivity.Dropping, false,
				changed && input.Activity == RiderActivity.PickingUp, yaw, pitch, mirror );
			_handling.Update( handlingInput, delta, _pose );
		}
		_previousActivity = input.Activity;
		_retargeter.Apply( _pose, mirror, _positions, _rotations );
		var riding = input.Activity == RiderActivity.Riding;
		MatchPushToGround( riding, input.Speed, delta );
		for ( var i = 0; i < _feet.Length; i++ )
			_kneeHint[ i ] = KneeDirection( _feet[ i ], _kneeHint[ i ], _positions );
		// While riding, the clip's board bone sits on the real deck rather than the clip's travel root; getting on
		// or off eases between the two so the body never jumps.
		var anchorStep = delta / AnchorBlend;
		_anchored = Math.Clamp( _anchored + (riding ? anchorStep : -anchorStep), 0f, 1f );
		var deck = _anchor >= 0 ? _retargeter.SourceOffset( _anchor, mirror ) * _anchored : System.Numerics.Vector3.Zero;
		// Clip-frame space: the root transform with the deck offset; the renderer adds the rig's own facing.
		// Riding a wall the clips hold the board up the wall with the body upright: the model turns so the clip's board
		// lies on the real deck, rather than the body lying along the wall with it.
		var wallStep = delta / WallTurn;
		_wallWeight = Math.Clamp( _wallWeight + (riding && input.WallRiding ? wallStep : -wallStep), 0f, 1f );
		var frame = root.Rotation;
		if ( _wallWeight > 0f && _anchor >= 0 )
		{
			var clipBoard = SkateUnits.ToEngine( _retargeter.SourceRotation( _anchor, mirror ) );
			frame = Rotation.Slerp( root.Rotation, root.Rotation * clipBoard.Inverse, Smoothstep( _wallWeight ) );
		}
		var model = new Transform( root.Position - frame * SkateUnits.ToEngine( deck ), frame );
		EaseFlip( model, _shownValid && mirror != _shownMirror, delta );
		var facing = SkateUnits.ToEngine( _retargeter.Facing );
		_renderer.WorldTransform = new Transform( model.Position - model.Rotation * (Vector3.Up * _retargeter.Ground),
			model.Rotation * facing.Inverse );
		var boardShown = ShowAnimatedBoard( model, mirror, riding && input.Airborne, delta );
		// Off the board in a hippy jump the feet leave the deck too.
		var released = input.OffBoard || ((_treeStates?.Player.Graph ?? _states?.Graph)?.HasAction( ReleaseFeet ) ?? false);
		for ( var i = 0; i < _feet.Length; i++ )
		{
			if ( riding )
			{
				OnBoard( i, model, mirror, released, delta, boardShown );
				continue;
			}
			_footWeight[ i ] = 0f;
			if ( !input.Airborne )
				Lift( _feet[ i ], 0f );
		}
		// The first frames after starting can carry a pose or board that is not real yet; the corrections wait for one.
		if ( riding && Finite( model ) && Finite( boardShown ) && Finite( _positions[ 0 ] ) )
		{
			boardShown = SettleBoard( model, boardShown, delta );
			for ( var i = 0; i < _feet.Length; i++ )
			{
				OutOfDeck( i, model, boardShown, delta );
				PlantOffDeck( i, model, boardShown, !input.Airborne && !input.OffBoard, delta );
			}
		}
		else
		{
			_settle = 0f;
		}
		FootDeckGap = riding ? LowestFootGap( model, boardShown ) : null;
		_model = model;
		Catch( input, model, delta );
		CarriedBoardPose = !riding && (input.HoldingBoard
			|| (RetrievalProfile is not null && input.Activity == RiderActivity.PickingUp))
			? CarryPose( model, mirror, delta ) : null;
		_shownValid = Finite( model ) && Finite( _positions[ 0 ] );
		if ( _shownValid )
		{
			System.Array.Copy( _positions, _shownPositions, _positions.Length );
			System.Array.Copy( _rotations, _shownRotations, _rotations.Length );
			_shownModel = model;
			_shownMirror = mirror;
		}
		var scene = _renderer.SceneModel;
		if ( scene is null )
			return;
		for ( var bone = 0; bone < _positions.Length; bone++ )
		{
			var transform = new Transform( SkateUnits.ToEngine( _retargeter.ToModel( _positions[ bone ] ) ),
				SkateUnits.ToEngine( _retargeter.ToModel( _rotations[ bone ] ) ) );
			scene.SetBoneOverride( bone, in transform );
		}
	}

	private Transform? CarryPose( Transform model, bool mirror, float delta )
	{
		var hand = mirror ? _mirroredCarryHand : _carryHand;
		_lastMirror = mirror;
		if ( _anchor < 0 || hand < 0 )
			return null;
		var sourceHand = _retargeter.SourceBone( hand, mirror );
		if ( sourceHand < 0 )
			return null;
		var boardPosition = _retargeter.SourcePosition( _anchor );
		var boardRotation = _retargeter.SourceRotation( _anchor );
		if ( _carryAttachment is not null )
		{
			var corrected = _carryAttachment.Update( _retargeter, _anchor, sourceHand );
			boardPosition = corrected.Translation;
			var rotation = PhysicalPoseOutput.Rotation( corrected.Rotation );
			boardRotation = new( rotation.X, rotation.Y, rotation.Z, rotation.W );
		}
		var pose = RetargetedAttachment.Place( boardPosition,
			boardRotation, _retargeter.SourcePosition( sourceHand ),
			_positions[ hand ], _retargeter.Scale, mirror );
		var held = new Transform( model.PointToWorld( SkateUnits.ToEngine( pose.Position ) ),
			model.Rotation * SkateUnits.ToEngine( pose.Rotation ), _board.WorldScale );
		held = OutOfHand( held, model, hand, delta );
		held = CatchGive( held, model, hand );
		return ClearOfBody( held, model, hand, mirror, delta );
	}

	// The clip's skater curls its fingers round the rail; this rider's fingers keep whatever pose its model has, so at
	// the clip's grip they would pass through the deck. The deck moves along its own up axis, the shorter way, until
	// the holding hand measured from the rider's mesh (wrist to fingertips, with its thickness) lies against the deck
	// instead of in it. A larger move applies at once so the hand never shows inside the deck, a smaller one eases
	// in, and the deck stays on its side of the hand unless the other side is clearly shorter.
	private Transform OutOfHand( Transform held, Transform model, int hand, float delta )
	{
		// Nothing pushes any more: the push eases out, but never back into the hand.
		var push = MathX.Lerp( _handPush, 0f, 1f - MathF.Exp( -HandRelease * delta ) );
		if ( HandOverlap( held, model, hand ) is { } overlap )
		{
			// The hand is inside the deck at pushes between minus the sink and the rise; inside that range the deck
			// moves out the shorter way, keeping its side unless the other side is shorter by more than the bias.
			var (rise, sink, bias) = overlap;
			if ( push > -sink && push < rise )
			{
				var up = rise - push;
				var down = push + sink;
				var raise = _handPush > 0f ? up <= down + bias : _handPush < 0f ? up + bias < down : up <= down;
				push = raise ? rise : -sink;
			}
		}
		_handPush = push;
		return Lifted( held );
	}

	private Transform Lifted( Transform held ) =>
		MathF.Abs( _handPush ) < 0.001f ? held : held.WithPosition( held.PointToWorld( new Vector3( 0f, 0f, _handPush ) ) );

	// How far (deck units) the deck must rise for its underside to clear the holding hand and sink for its top to
	// clear it (both positive while the hand is inside the deck), and a hand thickness in deck units; null when no
	// part of the hand is over the deck.
	private (float Rise, float Sink, float Bias)? HandOverlap( Transform board, Transform model, int hand )
	{
		if ( _skateboard is null || !_hands.TryGetValue( hand, out var shape ) )
			return null;
		var wrist = model.PointToWorld( SkateUnits.ToEngine( _positions[ hand ] ) );
		var axis = model.Rotation * SkateUnits.ToEngine( System.Numerics.Vector3.Transform( shape.Axis, _rotations[ hand ] ) );
		var normal = board.Rotation.Up;
		var across = Vector3.Cross( normal, axis );
		across = across.Length > 0.001f ? across.Normal : board.Rotation.Left;
		var offsets = new[] { Vector3.Zero, normal, -normal, across, -across };
		var rise = float.MinValue;
		var sink = float.MinValue;
		for ( var i = 0; i <= HandSamples; i++ )
		{
			var centre = wrist + axis * (shape.Length * i / HandSamples);
			foreach ( var offset in offsets )
			{
				var local = board.PointToLocal( centre + offset * shape.Radius );
				if ( _skateboard.DeckSurface( local.x, local.y ) is not { } surface )
					continue;
				rise = MathF.Max( rise, local.z - surface.Bottom );
				sink = MathF.Max( sink, surface.Top - local.z );
			}
		}
		if ( rise == float.MinValue )
			return null;
		var bias = (board.PointToLocal( wrist + normal * shape.Radius ) - board.PointToLocal( wrist )).Length * HandSideBias;
		return (rise, sink, bias);
	}

	/// <summary>
	/// Gap (inches) between the holding hand and <paramref name="board"/> after this frame's pose: negative when the
	/// hand is inside the deck, null when no board is held or no part of the hand is over the deck.
	/// </summary>
	internal float? HandDeckGap( Transform board )
	{
		if ( CarriedBoardPose is null || _anchor < 0 )
			return null;
		var hand = _lastMirror ? _mirroredCarryHand : _carryHand;
		if ( hand < 0 || HandOverlap( board, _model, hand ) is not { } overlap )
			return null;
		return overlap.Rise > 0f && overlap.Sink > 0f ? -MathF.Min( overlap.Rise, overlap.Sink )
			: MathF.Max( -overlap.Rise, -overlap.Sink );
	}

	// Follows the flying board while it is picked up; when it arrives in the hand the hand gives with it.
	private void Catch( in RiderAnimationInput input, Transform model, float delta )
	{
		if ( input.Activity == RiderActivity.PickingUp && !input.HoldingBoard )
		{
			var position = SkateUnits.ToCore( model.PointToLocal( _board.WorldPosition ) );
			_flightVelocity = _flightSeen && delta > 0f ? (position - _flightPosition) / delta : System.Numerics.Vector3.Zero;
			_flightPosition = position;
			_flightSeen = true;
			_catchElapsed = -1f;
			return;
		}
		if ( _flightSeen && input.HoldingBoard )
		{
			_catchVelocity = _flightVelocity;
			_catchElapsed = 0f;
		}
		_flightSeen = false;
		if ( _catchElapsed >= 0f )
			_catchElapsed = _catchElapsed + delta > CatchTime * 8f ? -1f : _catchElapsed + delta;
	}

	// The catch's give: the arrival velocity carried on and eased back, t * exp( -t / CatchTime ), its furthest reach
	// limited to a share of this rider's forearm. The holding arm reaches the moved hand.
	private Transform CatchGive( Transform held, Transform model, int hand )
	{
		if ( _catchElapsed < 0f || !_arms.TryGetValue( hand, out var arm ) )
			return held;
		var forearm = System.Numerics.Vector3.Distance( _positions[ arm.Lower ], _positions[ hand ] ) * CatchReach;
		// The give peaks at speed * CatchTime / e; a faster arrival is scaled down to reach no further than the limit.
		var speed = _catchVelocity.Length();
		var peak = speed * CatchTime / MathF.E;
		var velocity = peak > forearm && peak > 0f ? _catchVelocity * (forearm / peak) : _catchVelocity;
		var give = velocity * (_catchElapsed * MathF.Exp( -_catchElapsed / CatchTime ));
		if ( give.LengthSquared() < 1e-6f )
			return held;
		arm.Solve( _positions, _rotations, _positions[ hand ] + give );
		return held.WithPosition( held.Position + model.Rotation * SkateUnits.ToEngine( give ) );
	}

	// The board is placed for the clip's skater; a rider with other proportions may have legs or a torso where the
	// board hangs. The board moves out from the body just enough to clear this rider, measured from its own mesh,
	// and the holding arm reaches out with it so the grip stays; it eases back in once the body moves away.
	private Transform ClearOfBody( Transform held, Transform model, int hand, bool mirror, float delta )
	{
		if ( _skateboard?.BoardBounds is not { } box || !_arms.TryGetValue( hand, out var arm ) || Rig[ "hips" ] < 0 )
			return held;
		var side = mirror ? 1 : 0;
		_clearance[ side ] ??= new BodyClearance( _renderer.Model, Rig, hand );
		System.Func<int, Vector3> world = bone => model.PointToWorld( SkateUnits.ToEngine( _positions[ bone ] ) );
		var outward = held.PointToWorld( box.Center ) - world( Rig[ "hips" ] );
		var away = (outward - model.Rotation.Up * Vector3.Dot( outward, model.Rotation.Up )).Normal;
		var needed = _clearance[ side ].Push( held, box, away, world );
		// A larger push applies at once so the board never shows inside the body; a smaller one eases in.
		_clearancePush = needed >= _clearancePush ? needed
			: MathX.Lerp( _clearancePush, needed, 1f - MathF.Exp( -ClearanceRelease * delta ) );
		if ( _clearancePush < 0.01f )
			return held;
		var shift = away * _clearancePush;
		arm.Solve( _positions, _rotations, _positions[ hand ] + SkateUnits.ToCore( model.Rotation.Inverse * shift ) );
		return held.WithPosition( held.Position + shift );
	}

	// While riding, each leg reaches for where the clip has its toes relative to the clip's board, carried by the
	// real board, so feet stay on the deck through bumps, tilts and flips and a pushing foot reaches the ground
	// beside it. The pull fades in and out over a short time and lets go while a state releases the feet.
	// In the air the clip's board (flipping, spinning, grabbed) is shown instead of the physical one, so the feet
	// and hands meet the board the animation was authored around; on the ground the physical board shows again.
	// The pose comes from the clip's board bone at the rider's measured scale, so any rider model fits.
	private Transform ShowAnimatedBoard( Transform model, bool mirror, bool airborne, float delta )
	{
		if ( _skateboard is null || _anchor < 0 )
			return _board.WorldTransform;
		var rate = delta / (airborne ? BoardTakeOver : BoardHandBack);
		_boardShown = Math.Clamp( _boardShown + (airborne ? rate : -rate), 0f, 1f );
		var animated = new Transform( model.PointToWorld( SkateUnits.ToEngine( _retargeter.SourceOffset( _anchor, mirror ) ) ),
			model.Rotation * SkateUnits.ToEngine( _retargeter.SourceRotation( _anchor, mirror ) ) * SwitchTurn, _board.WorldScale );
		_animatedBoard = animated;
		_skateboard.SetDisplayPose( animated, _boardShown );
		return _skateboard.DisplayedTransform;
	}



	private void OnBoard( int index, Transform model, bool mirror, bool released, float delta, Transform board )
	{
		var foot = _feet[ index ];
		var toeSource = _retargeter.SourceEffector( foot.Ball, mirror );
		if ( toeSource < 0 || _anchor < 0 )
			return;
		var step = delta / FootBlend;
		_footWeight[ index ] = Math.Clamp( _footWeight[ index ] + (released ? -step : step), 0f, 1f );
		var weight = _footWeight[ index ];
		if ( weight <= 0f )
		{
			_footCorrection[ index ] = System.Numerics.Vector3.Zero;
			return;
		}
		var onBoard = _retargeter.SourceRelative( toeSource, _anchor, mirror );
		var toeWorld = board.PointToWorld( SwitchTurn * SkateUnits.ToEngine( onBoard ) );
		var toeModel = SkateUnits.ToCore( model.PointToLocal( toeWorld ) );
		var ankle = _positions[ foot.Ankle ];
		var toe = _positions[ foot.Ball ];
		var correction = toeModel - toe;
		// The pull keeps feet on the deck; a foot whose target is off the deck (down on the ground, pushing) belongs
		// to the ground and is left to the plant and lock, fading out past the deck's edge.
		if ( _skateboard is not null )
			correction *= SurfaceNear( board.PointToLocal( toeWorld ), _skateboard.DeckBounds )?.Reach ?? 0f;
		// In the air the clip's foot target only means something while its own foot IK is engaged, and elsewhere it
		// can jump inches in a frame; a large correction there is ignored rather than followed.
		if ( _boardShown > 0f )
		{
			var size = correction.Length();
			correction *= 1f - _boardShown * Smoothstep( (size - TrustedCorrection) / (IgnoredCorrection - TrustedCorrection) );
		}
		// The correction itself changes smoothly, so no frame snaps a leg.
		var blend = 1f - MathF.Exp( -CorrectionRate * delta );
		_footCorrection[ index ] = System.Numerics.Vector3.Lerp( _footCorrection[ index ], correction, blend );
		var target = ankle + _footCorrection[ index ] * weight;
		foot.Leg.Solve( _positions, _rotations, target, _kneeHint[ index ] );
	}

	// No foot may pass into the deck: where a foot's ball or ankle is over the shown deck, padded by that foot's
	// own sole (on top) or instep (underneath) it must stay outside the deck model's actual surface there, else
	// the leg is moved out along the deck normal. Surfaces come from the deck model's triangles and the paddings
	// from the rider's rest pose, so any board and rider fit.
	// How far a foot must move along the deck's up axis to be clear of the deck (and, beneath it, the trucks and
	// wheels): positive lifts a foot on top, negative lowers one underneath.
	private float RequiredPush( (TwoBoneIk Leg, int Ankle, int Ball, float AnkleHeight, float BallHeight, System.Numerics.Vector3[] Sole, float ShinRadius) foot,
		Transform model, Transform board )
	{
		if ( _skateboard is null )
			return 0f;
		// The whole foot is either on top of the deck or under it (a flick passing beneath a flipping board); deciding
		// per point would let a deep heel count as "under" and stay inside.
		var onTop = OnTop( foot, model, board );
		var push = 0f;
		// Every measured point of the shoe (or the ankle and ball with their rest heights when the mesh could not be
		// measured) must clear the deck on the foot's side.
		var bounds = _skateboard.DeckBounds;
		foreach ( var (position, padding) in SolePoints( foot ) )
		{
			var local = board.PointToLocal( model.PointToWorld( SkateUnits.ToEngine( position ) ) );
			if ( SurfaceNear( local, bounds ) is not { } near )
				continue;
			var (surface, reach) = near;
			// Past the edge the deck only reaches a foot coming over it, not one down beside it on the ground.
			if ( reach < 1f && (onTop ? local.z < surface.Bottom : local.z > surface.Top) )
				continue;
			var needed = onTop
				? surface.Top + (padding > 0f ? padding : SoleMargin) - local.z
				: surface.Bottom - (padding > 0f ? foot.BallHeight : SoleMargin) - local.z;
			needed *= reach;
			push = onTop ? MathF.Max( push, needed ) : MathF.Min( push, needed );
		}
		// A foot on the underside (a flipping board turned toward it) must also clear the trucks and wheels.
		if ( !onTop )
		{
			var parts = _skateboard.PartBounds;
			foreach ( var (position, padding) in SolePoints( foot ) )
			{
				var local = board.PointToLocal( model.PointToWorld( SkateUnits.ToEngine( position ) ) );
				foreach ( var box in parts )
				{
					if ( local.x < box.Mins.x || local.x > box.Maxs.x || local.y < box.Mins.y || local.y > box.Maxs.y )
						continue;
					push = MathF.Min( push, box.Mins.z - (padding > 0f ? foot.BallHeight : SoleMargin) - local.z );
				}
			}
		}
		return push;
	}

	private void OutOfDeck( int index, Transform model, Transform board, float delta )
	{
		var foot = _feet[ index ];
		var needed = RequiredPush( foot, model, board );
		if ( !float.IsFinite( needed ) )
			needed = 0f;
		// Pushing further out applies at once so the foot never shows inside the deck; letting go (a foot stepping
		// off the deck's edge to push) eases so the leg does not drop in one frame.
		var current = float.IsFinite( _deckPush[ index ] ) ? _deckPush[ index ] : 0f;
		var push = MathF.Abs( needed ) >= MathF.Abs( current ) && (current == 0f || MathF.Sign( needed ) == MathF.Sign( current ))
			? needed : MathX.Lerp( current, needed, 1f - MathF.Exp( -DeckPushRelease * delta ) );
		if ( needed != 0f && MathF.Sign( needed ) == MathF.Sign( push ) && MathF.Abs( push ) < MathF.Abs( needed ) )
			push = needed;
		_deckPush[ index ] = push;
		if ( MathF.Abs( push ) < 0.01f )
			return;
		var normal = SkateUnits.ToCore( model.Rotation.Inverse * board.Rotation.Up );
		foot.Leg.Solve( _positions, _rotations, _positions[ foot.Ankle ] + normal * push, _kneeHint[ index ] );
	}

	// Points along a shin, ankle to knee, as (position, radius).
	private IEnumerable<(System.Numerics.Vector3 Position, float Radius)> ShinPoints( (TwoBoneIk Leg, int Ankle, int Ball, float AnkleHeight, float BallHeight, System.Numerics.Vector3[] Sole, float ShinRadius) foot )
	{
		var ankle = _positions[ foot.Ankle ];
		var knee = _positions[ foot.Leg.Lower ];
		for ( var i = 1; i <= ShinSamples; i++ )
			yield return (System.Numerics.Vector3.Lerp( ankle, knee, i / (float)ShinSamples ), foot.ShinRadius);
	}

	// How far a shin must move along the deck's up axis to clear the deck, on its foot's side.
	private float ShinPush( (TwoBoneIk Leg, int Ankle, int Ball, float AnkleHeight, float BallHeight, System.Numerics.Vector3[] Sole, float ShinRadius) foot, Transform model, Transform board )
	{
		if ( _skateboard is null )
			return 0f;
		var onTop = OnTop( foot, model, board );
		var push = 0f;
		foreach ( var (position, radius) in ShinPoints( foot ) )
		{
			var local = board.PointToLocal( model.PointToWorld( SkateUnits.ToEngine( position ) ) );
			if ( _skateboard.DeckSurface( local.x, local.y ) is not { } surface )
				continue;
			push = onTop ? MathF.Max( push, surface.Top + radius - local.z ) : MathF.Min( push, surface.Bottom - radius - local.z );
		}
		return push;
	}

	// Which way the animation bends a knee: the knee's offset from the hip-to-ankle line. The straighter the leg the
	// less that offset says, so the direction kept from earlier frames takes over and a nearly straight leg never
	// flips its knee. Every leg correction passes it on.
	private static System.Numerics.Vector3 KneeDirection( (TwoBoneIk Leg, int Ankle, int Ball, float AnkleHeight, float BallHeight, System.Numerics.Vector3[] Sole, float ShinRadius) foot,
		System.Numerics.Vector3 previous, System.Numerics.Vector3[] positions )
	{
		if ( previous.LengthSquared() < 0.5f )
			previous = System.Numerics.Vector3.UnitX;
		var hip = positions[ foot.Leg.Upper ];
		var knee = positions[ foot.Leg.Lower ];
		var line = positions[ foot.Ankle ] - hip;
		if ( line.LengthSquared() < 1e-6f )
			return previous;
		line = System.Numerics.Vector3.Normalize( line );
		var pole = (knee - hip) - line * System.Numerics.Vector3.Dot( knee - hip, line );
		var bend = pole.Length();
		if ( bend < 1e-4f )
			return previous;
		var trust = Math.Clamp( bend / KneeBendTrusted, 0f, 1f );
		var blended = System.Numerics.Vector3.Lerp( previous, pole / bend, trust );
		return blended.LengthSquared() > 1e-6f ? System.Numerics.Vector3.Normalize( blended ) : previous;
	}

	// Knee offset from the hip-to-ankle line (metres) at which the animation's bend direction is fully trusted.
	private const float KneeBendTrusted = 0.05f;

	private static bool Finite( Transform value ) => float.IsFinite( value.Position.x + value.Position.y + value.Position.z
		+ value.Rotation.x + value.Rotation.y + value.Rotation.z + value.Rotation.w );

	private static bool Finite( System.Numerics.Vector3 value ) => float.IsFinite( value.X + value.Y + value.Z );

	// Pulls a foot target in along the line from the hip so it approaches the leg's full length smoothly.
	private Vector3 SoftenReach( (TwoBoneIk Leg, int Ankle, int Ball, float AnkleHeight, float BallHeight, System.Numerics.Vector3[] Sole, float ShinRadius) foot,
		Transform model, Vector3 target )
	{
		var hip = model.PointToWorld( SkateUnits.ToEngine( _positions[ foot.Leg.Upper ] ) );
		var length = SkateUnits.ToEngine( _positions[ foot.Leg.Lower ] - _positions[ foot.Leg.Upper ] ).Length
			+ SkateUnits.ToEngine( _positions[ foot.Ankle ] - _positions[ foot.Leg.Lower ] ).Length;
		var soft = length * SoftReach;
		var span = length - soft;
		var offset = target - hip;
		var distance = offset.Length;
		if ( distance <= soft || span <= 0f )
			return target;
		return hip + offset / distance * (soft + span * (1f - MathF.Exp( -(distance - soft) / span )));
	}

	// Velocity matching: while a foot is planted on the ground pushing, the clip plays at the rate that sweeps that
	// foot back exactly as fast as the board moves, so the foot stays put on the ground instead of sliding. The rate
	// eases back to the authored one when no foot is planted.
	private void MatchPushToGround( bool riding, float boardSpeed, float delta )
	{
		if ( _states is null )
			return;
		var target = 1f;
		for ( var i = 0; i < _feet.Length; i++ )
		{
			var ball = _positions[ _feet[ i ].Ball ];
			if ( riding && _locked[ i ] && _ballSeen[ i ] && delta > 0f )
			{
				// Backward speed of the clip's foot relative to the body at the authored rate, inches per second.
				var authored = -(ball.X - _ball[ i ].X) / delta / MathF.Max( _states.RateScale, 0.01f );
				if ( authored > MinimumSweep )
					target = Math.Clamp( MathF.Abs( boardSpeed ) * SkateUnits.InchesPerMetre / authored, MinimumRate, MaximumRate );
			}
			_ball[ i ] = ball;
			_ballSeen[ i ] = true;
		}
		_states.RateScale = MathX.Lerp( _states.RateScale, riding ? target : 1f, 1f - MathF.Exp( -RateSmoothing * delta ) );
	}

	// While the animated board is shown (in the air) it gives way to the feet along its own up axis instead of the
	// feet bending away from it: the animation was made for its own board and skater, and moving the legs there
	// kinks the knees. The board takes the whole move at once and eases back once the feet allow; when the feet ask
	// for opposite directions (one above, one beneath) they yield themselves.
	private Transform SettleBoard( Transform model, Transform board, float delta )
	{
		if ( _skateboard is null || _boardShown <= 0f )
		{
			_settle = 0f;
			return board;
		}
		var wanted = 0f;
		var agreed = true;
		foreach ( var foot in _feet )
		{
			var push = RequiredPush( foot, model, board );
			var shin = ShinPush( foot, model, board );
			var move = -(MathF.Abs( shin ) > MathF.Abs( push ) && (push == 0f || MathF.Sign( shin ) == MathF.Sign( push )) ? shin : push);
			if ( MathF.Abs( move ) < 0.01f )
				continue;
			if ( wanted != 0f && MathF.Sign( move ) != MathF.Sign( wanted ) )
				agreed = false;
			if ( MathF.Abs( move ) > MathF.Abs( wanted ) )
				wanted = move;
		}
		if ( !agreed )
			wanted = 0f;
		wanted = Math.Clamp( wanted, -SettleLimit, SettleLimit );
		_settle = MathF.Abs( wanted ) >= MathF.Abs( _settle ) || MathF.Sign( wanted ) != MathF.Sign( _settle ) && wanted != 0f
			? wanted : MathX.Lerp( _settle, wanted, 1f - MathF.Exp( -SettleRelease * delta ) );
		if ( MathF.Abs( _settle ) < 0.01f )
			return board;
		// The shown pose blends physics and animation by the shown weight, so the animated pose carries the move.
		var shift = board.Rotation.Up * (_settle / MathF.Max( _boardShown, 0.25f ));
		_skateboard.SetDisplayPose( _animatedBoard.WithPosition( _animatedBoard.Position + shift ), _boardShown );
		return _skateboard.DisplayedTransform;
	}

	// The deck surface under a point, and how fully it applies: fully over the deck, fading to nothing a short way
	// past its edge, so a foot swinging back onto the deck starts rising before it arrives instead of in one frame.
	private ((float Top, float Bottom) Surface, float Reach)? SurfaceNear( Vector3 local, BBox? bounds )
	{
		if ( _skateboard.DeckSurface( local.x, local.y ) is { } surface )
			return (surface, 1f);
		if ( bounds is not { } box || local.x < box.Mins.x - EdgeFade || local.x > box.Maxs.x + EdgeFade
			|| local.y < box.Mins.y - EdgeFade || local.y > box.Maxs.y + EdgeFade )
			return null;
		// Walk toward the deck's middle line to find the nearest point on its outline.
		var inside = new Vector2( Math.Clamp( local.x, box.Mins.x * 0.9f, box.Maxs.x * 0.9f ), 0f );
		if ( _skateboard.DeckSurface( inside.x, inside.y ) is null )
			return null;
		var outside = new Vector2( local.x, local.y );
		for ( var i = 0; i < 10; i++ )
		{
			var middle = (inside + outside) * 0.5f;
			if ( _skateboard.DeckSurface( middle.x, middle.y ) is null )
				outside = middle;
			else
				inside = middle;
		}
		var distance = (new Vector2( local.x, local.y ) - inside).Length;
		if ( distance >= EdgeFade || _skateboard.DeckSurface( inside.x, inside.y ) is not { } edge )
			return null;
		return (edge, 1f - Smoothstep( distance / EdgeFade ));
	}

	// Whether a foot is above the deck rather than beneath it, from its ankle against the deck's middle.
	private bool OnTop( (TwoBoneIk Leg, int Ankle, int Ball, float AnkleHeight, float BallHeight, System.Numerics.Vector3[] Sole, float ShinRadius) foot,
		Transform model, Transform board )
	{
		var ankle = board.PointToLocal( model.PointToWorld( SkateUnits.ToEngine( _positions[ foot.Ankle ] ) ) );
		var surface = _skateboard.DeckSurface( ankle.x, ankle.y ) ?? _skateboard.DeckSurface( 0f, 0f ) ?? (0f, 0f);
		return ankle.z >= (surface.Top + surface.Bottom) * 0.5f;
	}

	// A foot whose whole sole is off the deck and close to the ground (pushing) stands on the real ground under it.
	// The clip places it for its own board's height, so another rider, board or slope would leave the sole
	// hovering or sunk; within a short gap the sole is set on the ground, and the hold fades as the clip lifts it.
	private void PlantOffDeck( int index, Transform model, Transform board, bool grounded, float delta )
	{
		var foot = _feet[ index ];
		var gap = float.MaxValue;
		var wanted = grounded ? PlantShift( foot, model, board, out gap ) : 0f;
		// Lowering onto the ground eases in and out so the leg never snaps or flickers as the foot crosses the deck's
		// edge or the edge of the plant's range; lifting a sole out of the ground applies at once.
		_plant[ index ] = wanted > 0f && wanted >= _plant[ index ] ? wanted
			: MathX.Lerp( _plant[ index ], wanted, 1f - MathF.Exp( -PlantRate * delta ) );
		var ankle = model.PointToWorld( SkateUnits.ToEngine( _positions[ foot.Ankle ] ) );
		var target = ankle + Vector3.Up * _plant[ index ];
		// A foot standing on the ground (pushing) stays where it touched down while the body rides on: the clip
		// moves it relative to the body only, so left alone it would slide and shake over the ground.
		// It locks on touching down and lets go only once clearly lifted, so a gap hovering at the edge does not keep
		// re-locking it. It takes full hold at once, exactly where the foot is shown at that moment (which a lock still
		// letting go may hold partly), so taking hold stops the foot without moving it.
		if ( grounded && gap <= LockGap && !_locked[ index ] )
		{
			_locked[ index ] = true;
			_lockPoint[ index ] = Vector3.Lerp( target, _lockPoint[ index ].WithZ( target.z ), _lock[ index ] );
			_lock[ index ] = 1f;
		}
		if ( _locked[ index ] && (!grounded || gap > PlantSnap + PlantFade) )
			_locked[ index ] = false;
		// When the body carries the leg past its reach the held spot is dragged along at that reach, so the foot
		// slides only as much as it must and never jumps.
		var offset = (_lockPoint[ index ] - target).WithZ( 0f );
		if ( offset.Length > LockReach )
			_lockPoint[ index ] = target + offset.Normal * LockReach;
		var lockedAt = _lockPoint[ index ].WithZ( target.z );
		// Letting go eases, so what is left of the offset blends into the lifting foot's swing.
		if ( !_locked[ index ] )
			_lock[ index ] = MathX.Lerp( _lock[ index ], 0f, 1f - MathF.Exp( -LockRelease * delta ) );
		if ( _lock[ index ] > 0.001f )
			target = Vector3.Lerp( target, lockedAt, _lock[ index ] );
		target = SoftenReach( foot, model, target );
		if ( (target - ankle).Length < 0.01f )
			return;
		foot.Leg.Solve( _positions, _rotations, SkateUnits.ToCore( model.PointToLocal( target ) ), _kneeHint[ index ] );
	}

	// How far (inches, up positive) a foot beside the deck should move to stand on the ground under it: zero while
	// any of it is over the deck or it is well above the ground.
	private float PlantShift( (TwoBoneIk Leg, int Ankle, int Ball, float AnkleHeight, float BallHeight, System.Numerics.Vector3[] Sole, float ShinRadius) foot,
		Transform model, Transform board, out float gap )
	{
		gap = float.MaxValue;
		if ( _skateboard is null )
			return 0f;
		var lowest = float.MaxValue;
		var under = Vector3.Zero;
		foreach ( var (position, padding) in SolePoints( foot ) )
		{
			var world = model.PointToWorld( SkateUnits.ToEngine( position ) );
			var local = board.PointToLocal( world );
			if ( _skateboard.DeckSurface( local.x, local.y ) is not null )
				return 0f;
			if ( world.z - padding < lowest )
			{
				lowest = world.z - padding;
				under = world;
			}
		}
		if ( lowest == float.MaxValue )
			return 0f;
		var ground = _rider.Scene.Trace.Ray( under + Vector3.Up * PlantReach, under - Vector3.Up * PlantReach )
			.IgnoreGameObjectHierarchy( _rider ).IgnoreGameObjectHierarchy( _board )
			.IgnoreGameObjectHierarchy( _renderer.GameObject ).Run();
		if ( !ground.Hit || ground.Normal.z < 0.5f )
			return 0f;
		gap = lowest - ground.HitPosition.z;
		return -gap * (1f - Smoothstep( (gap - PlantSnap) / PlantFade ));
	}

	// The measured sole points where the mesh gave them, else the ankle and ball with their rest heights as padding.
	private IEnumerable<(System.Numerics.Vector3 Position, float Padding)> SolePoints(
		(TwoBoneIk Leg, int Ankle, int Ball, float AnkleHeight, float BallHeight, System.Numerics.Vector3[] Sole, float ShinRadius) foot )
	{
		if ( foot.Sole.Length == 0 )
		{
			yield return (_positions[ foot.Ankle ], foot.AnkleHeight);
			yield return (_positions[ foot.Ball ], foot.BallHeight);
			yield break;
		}
		var rotation = _rotations[ foot.Ankle ];
		foreach ( var point in foot.Sole )
			yield return (_positions[ foot.Ankle ] + System.Numerics.Vector3.Transform( point, rotation ), 0f);
	}

	private float? LowestFootGap( Transform model, Transform board )
	{
		float? lowest = null;
		if ( _skateboard is null )
			return lowest;
		foreach ( var foot in _feet )
		{
			var onTop = OnTop( foot, model, board );
			foreach ( var (position, padding) in SolePoints( foot ) )
			{
				var local = board.PointToLocal( model.PointToWorld( SkateUnits.ToEngine( position ) ) );
				if ( _skateboard.DeckSurface( local.x, local.y ) is not { } surface )
					continue;
				var gap = onTop ? local.z - padding - surface.Top : surface.Bottom - (local.z + (padding > 0f ? foot.BallHeight : 0f));
				if ( lowest is null || gap < lowest )
					lowest = gap;
			}
			foreach ( var (position, radius) in ShinPoints( foot ) )
			{
				var local = board.PointToLocal( model.PointToWorld( SkateUnits.ToEngine( position ) ) );
				if ( _skateboard.DeckSurface( local.x, local.y ) is not { } surface )
					continue;
				var gap = onTop ? local.z - radius - surface.Top : surface.Bottom - (local.z + radius);
				if ( lowest is null || gap < lowest )
					lowest = gap;
			}
			if ( onTop )
				continue;
			foreach ( var (position, padding) in SolePoints( foot ) )
			{
				var local = board.PointToLocal( model.PointToWorld( SkateUnits.ToEngine( position ) ) );
				foreach ( var box in _skateboard.PartBounds )
				{
					if ( local.x < box.Mins.x || local.x > box.Maxs.x || local.y < box.Mins.y || local.y > box.Maxs.y
						|| local.z > box.Maxs.z )
						continue;
					var gap = box.Mins.z - (local.z + (padding > 0f ? foot.BallHeight : 0f));
					if ( lowest is null || gap < lowest )
						lowest = gap;
				}
			}
		}
		return lowest;
	}

	/// <summary>True while riding switch (see <see cref="AnimationGraphDefinition.StanceSwapClips"/>).</summary>
	internal bool Switch { get; private set; }

	// Shown switch, the clips face the board's tail, so the clip's board is the real one turned half way round.
	private Rotation SwitchTurn => _shownSwitch ? Rotation.FromYaw( 180f ) : Rotation.Identity;

	// Each start of a turn-round clip swaps the stance; the next other clip to start shows it. Returns true when the shown
	// stance changes this frame.
	private bool TrackStance()
	{
		var graph = _treeStates?.Player.Graph ?? _states?.Graph;
		if ( graph is null || _stanceSwapClips.Count == 0 || _director.Requesting || graph.PlayCount == _plays )
			return false;
		_plays = graph.PlayCount;
		if ( graph.Clip is { } clip && _stanceSwapClips.Contains( clip ) )
		{
			Switch = !Switch;
			return false;
		}
		if ( _shownSwitch == Switch )
			return false;
		_shownSwitch = Switch;
		return true;
	}

	// Showing the other stance turns and mirrors the whole pose in one frame; the difference from what was last shown is
	// eased out instead, so the body turns over smoothly.
	private void EaseFlip( Transform model, bool flipped, float delta )
	{
		if ( flipped )
		{
			for ( var bone = 0; bone < _positions.Length; bone++ )
			{
				var world = _shownModel.PointToWorld( SkateUnits.ToEngine( _shownPositions[ bone ] ) );
				_flipPositions[ bone ] = SkateUnits.ToCore( model.PointToLocal( world ) ) - _positions[ bone ];
				var shown = SkateUnits.ToCore( model.Rotation.Inverse * _shownModel.Rotation * SkateUnits.ToEngine( _shownRotations[ bone ] ) );
				var offset = System.Numerics.Quaternion.Normalize( shown * System.Numerics.Quaternion.Conjugate( _rotations[ bone ] ) );
				_flipRotations[ bone ] = offset.W < 0f ? System.Numerics.Quaternion.Negate( offset ) : offset;
			}
			_flipTime = 0f;
		}
		if ( _flipTime >= FlipEase )
			return;
		_flipTime += delta;
		var remaining = 1f - Smoothstep( _flipTime / FlipEase );
		for ( var bone = 0; bone < _positions.Length; bone++ )
		{
			_positions[ bone ] += _flipPositions[ bone ] * remaining;
			_rotations[ bone ] = System.Numerics.Quaternion.Normalize(
				System.Numerics.Quaternion.Slerp( System.Numerics.Quaternion.Identity, _flipRotations[ bone ], remaining ) * _rotations[ bone ] );
		}
	}

	private static float Smoothstep( float value )
	{
		value = Math.Clamp( value, 0f, 1f );
		return value * value * (3f - 2f * value);
	}

	// Lifts a sole that sank below the support surface.
	private void Lift( (TwoBoneIk Leg, int Ankle, int Ball, float AnkleHeight, float BallHeight, System.Numerics.Vector3[] Sole, float ShinRadius) foot, float surface,
		System.Numerics.Vector3 hint = default )
	{
		var sole = MathF.Min( _positions[ foot.Ankle ].Z - foot.AnkleHeight, _positions[ foot.Ball ].Z - foot.BallHeight );
		var lift = Math.Clamp( surface - sole, 0f, MaximumLift );
		if ( lift > 0.01f )
			foot.Leg.Solve( _positions, _rotations, _positions[ foot.Ankle ] + new System.Numerics.Vector3( 0f, 0f, lift ), hint );
	}

	internal void Clear()
	{
		_renderer.SceneModel?.ClearBoneOverrides();
	}
}
