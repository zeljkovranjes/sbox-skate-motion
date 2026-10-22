using System.Collections.Generic;
using System.Linq;
using Sandbox;
using SkateMotion.Core;
using SkateMotion.Core.Constraints;
using SkateMotion.Core.Grinding;
using SkateMotion.Core.BoardModel;
using SkateMotion.Core.Vehicle;

namespace SkateMotion.Engine;

internal sealed class BoardVisuals
{
	private readonly GameObject _root;
	private readonly GameObject[] _wheels = new GameObject[ 4 ];
	private readonly GameObject[] _trucks = new GameObject[ 2 ];
	private readonly BoardRig _rig;
	private readonly GameObject _board;
	private readonly GameObject _deck;
	private Transform _display;
	private float _displayWeight;
	private readonly Transform[] _wheelRest = new Transform[ 4 ];
	private readonly Transform[] _truckRest = new Transform[ 2 ];
	private bool _restCaptured;
	private readonly bool _generatedDeck;
	private readonly GameObject[] _baseplates = new GameObject[ 2 ];

	// A board model taken apart, and where it is drawn whole instead, the bone moving each part and that bone's place on
	// the part.
	private readonly BoardModelParts _parts;
	private readonly SkinnedModelRenderer _skinned;
	private readonly int[] _bones;
	private readonly Transform[] _boneOffsets = new Transform[ BoardPart.RearRightWheel - BoardPart.Deck + 1 ];

	private readonly DeckLean _lean = new();
	private readonly WheelSpin _spin = new();
	private readonly float _radius;

	internal BoardVisuals( GameObject board, SkateSettings settings, float radius, BoardRig rig, bool generate )
	{
		_rig = rig;
		_board = board;
		_radius = radius;
		_parts = rig.Parts;
		_bones = rig.PartBones;
		_root = new GameObject( board, true, "Generated Skateboard" );
		var skinned = _parts is not null && MovedByBones( _bones );
		if ( skinned )
			_skinned = AddSkinned( _root, "Deck", _parts );
		else if ( _parts is not null )
			_deck = AddModel( _root, "Deck", _parts.Deck );
		else if ( rig.DeckModel is not null || generate )
			_deck = AddModel( _root, "Deck", rig.DeckModel ?? BoardGeometry.Deck() );
		_generatedDeck = _parts is null && rig.DeckModel is null;
		var wheelModel = rig.WheelModel ?? (generate ? BoardGeometry.Wheel( radius ) : null);
		for ( var i = 0; i < 4; i++ )
		{
			var model = _parts is not null ? (skinned ? null : _parts.Wheels[ i ]) : wheelModel;
			_wheels[ i ] = rig.HasCustomWheels ? rig.Wheels[ i ] : model is not null ? AddModel( _root, $"Wheel {i + 1}", model ) : null;
		}
		if ( _parts is not null && !skinned )
		{
			for ( var i = 0; i < 2; i++ )
			{
				_trucks[ i ] = _parts.Trucks[ i ] is { } truck ? AddModel( _root, TruckName( i ), truck ) : null;
				_baseplates[ i ] = _parts.Baseplates[ i ] is { } plate && _deck.IsValid() ? AddModel( _deck, BaseplateName( i ), plate ) : null;
			}
		}
		else if ( _parts is null && generate && !rig.HasCustomWheels )
		{
			// The generated deck's underside sits 7 mm below its centre, which rides at RideHeight. Its baseplates are fixed to
			// the deck and lean with it, while the hangers below them stay level with the ground.
			var rise = settings.RideHeight - 0.007f - radius;
			var truckModel = BoardGeometry.Truck( settings.Track, rise, baseplate: false );
			for ( var i = 0; i < 2; i++ )
			{
				_trucks[ i ] = AddModel( _root, TruckName( i ), truckModel );
				if ( rise <= BoardGeometry.FullTruckRise )
					continue;
				_baseplates[ i ] = AddModel( _deck, BaseplateName( i ), BoardGeometry.Baseplate() );
				_baseplates[ i ].LocalPosition = new Vector3( (rig.Origins[ i * 2 ].x + rig.Origins[ i * 2 + 1 ].x) * 0.5f, 0f,
					-0.011f * SkateUnits.InchesPerMetre );
			}
		}
		if ( skinned )
			PlaceBones();
	}

	private static string TruckName( int truck ) => truck == 0 ? "Front Truck" : "Rear Truck";

	private static string BaseplateName( int truck ) => truck == 0 ? "Front Baseplate" : "Rear Baseplate";

	// A board model is drawn whole and moved by its bones when it has a bone for each of its wheels.
	private static bool MovedByBones( int[] bones ) => bones is not null && bones[ (int)BoardPart.FrontLeftWheel ] >= 0
		&& bones[ (int)BoardPart.FrontRightWheel ] >= 0 && bones[ (int)BoardPart.RearLeftWheel ] >= 0
		&& bones[ (int)BoardPart.RearRightWheel ] >= 0;

	// Each bone's bind pose on its part, about where the part turns (a wheel's centre, a truck's axle).
	private void PlaceBones()
	{
		for ( var part = BoardPart.FrontTruck; part <= BoardPart.RearRightWheel; part++ )
		{
			var bone = _bones[ (int)part ];
			if ( bone < 0 || bone >= _parts.Source.BoneCount )
				continue;
			_boneOffsets[ (int)part ] = PivotOf( part ).ToLocal( _parts.BoneRest( bone ) );
		}
	}

	private Transform PivotOf( BoardPart part ) => new( part >= BoardPart.FrontLeftWheel
		? _parts.WheelCentre( part - BoardPart.FrontLeftWheel ) : _parts.AxleCentre( part - BoardPart.FrontTruck ) );

	/// <summary>
	/// Moves on what the shown board does besides following its pose: the deck's lean into a turn and each wheel's spin,
	/// rolling with the ground under it (see <see cref="WheelSpin"/>).
	/// </summary>
	internal void Animate( ReadOnlySpan<DynamicBody> bodies, in BoardMotion motion, float delta )
	{
		_lean.Update( motion.Steer, motion.Speed, motion.Pushing, motion.Lean, delta );
		for ( var i = 0; i < 4; i++ )
		{
			var forward = System.Numerics.Vector3.Transform( System.Numerics.Vector3.UnitX, bodies[ i / 2 + 1 ].Rotation );
			var rolling = System.Numerics.Vector3.Dot( bodies[ i + 3 ].Velocity, forward );
			_spin.Update( i, rolling, _radius, motion.Touching( i ), delta );
		}
	}

	// How far past the room its wheels leave it (BoardRig.LeanLimit) a deck may lean, as a share of that room: it rises
	// off the wheels as it goes (Rise), as if on risers.
	private const float TiltPastRoom = 1.6f;

	/// <summary>
	/// How far the deck dips into the current turn (radians, positive: its left side down): on a board whose wheels sit
	/// close under the deck, no further than <see cref="TiltPastRoom"/> times the room they leave it, easing into that
	/// limit rather than stopping at it.
	/// </summary>
	internal float Roll
	{
		get
		{
			var roll = _lean.Roll;
			var limit = _rig.LeanLimit * TiltPastRoom;
			return limit >= DeckLean.MaximumRoll ? roll : limit > 0f ? limit * MathF.Tanh( roll / limit ) : 0f;
		}
	}

	// How far (inches) the deck rises off its wheels at a lean past the room they leave it: enough that its underside
	// clears the farthest of them under it.
	private float Rise( float roll ) => _rig.LeanLimit is var limit && float.IsFinite( limit )
		? MathF.Max( 0f, MathF.Abs( roll ) - limit ) * _rig.WheelReach : 0f;

	/// <summary>
	/// A deck pose dipped by the deck's lean, about the line through the trucks' pivots under it, and risen off its wheels
	/// as far as a lean past their room needs.
	/// </summary>
	internal Transform Leaned( Transform deck )
	{
		var roll = Roll;
		if ( roll == 0f )
			return deck;
		var turn = Rotation.FromAxis( Vector3.Forward, -roll * (180f / MathF.PI) );
		var pivot = Vector3.Down * _rig.PivotDepth;
		return deck.ToWorld( new Transform( pivot - turn * pivot + Vector3.Up * Rise( roll ), turn ) );
	}

	// A wheel's spin about its own axle (the board's left in its rest frame).
	private Rotation Spin( int wheel ) =>
		Rotation.FromAxis( _rig.WheelRotations[ wheel ].Inverse * Vector3.Left, _spin.Angle( wheel ) * (180f / MathF.PI) );

	/// <summary>
	/// Shows the whole board (deck, trucks, wheels) at <paramref name="pose"/> instead of where physics holds it,
	/// blended by <paramref name="weight"/> (0 shows the physical board). Visual only.
	/// </summary>
	/// <summary>Takes <paramref name="amount"/> off the display pose's weight (the physical board shows again as it nears 0).</summary>
	internal void FadeDisplay( float amount ) => _displayWeight = MathF.Max( 0f, _displayWeight - amount );

	internal void SetDisplay( Transform pose, float weight )
	{
		// A pose that is not yet a real one (an animation's first frames) is not shown.
		var rotation = pose.Rotation;
		var length = rotation.x * rotation.x + rotation.y * rotation.y + rotation.z * rotation.z + rotation.w * rotation.w;
		if ( !float.IsFinite( pose.Position.x + pose.Position.y + pose.Position.z ) || !float.IsFinite( length ) || length < 0.25f )
		{
			_displayWeight = 0f;
			return;
		}
		_display = pose;
		_displayWeight = Math.Clamp( weight, 0f, 1f );
	}

	/// <summary>The deck model's box in the deck's own frame (board units), or null without a deck model.</summary>
	internal BBox? DeckBounds
	{
		get
		{
			if ( _parts?.Shape is { } shape )
				return new BBox( SkateUnits.Inches( shape.Bounds.Min ), SkateUnits.Inches( shape.Bounds.Max ) );
			var model = _deck.IsValid() ? _deck.Components.Get<ModelRenderer>()?.Model : null;
			if ( model is null )
				return null;
			var bounds = model.Bounds;
			var local = _deck.LocalTransform;
			return BBox.FromPoints( bounds.Corners.Select( corner => local.PointToWorld( corner ) ) );
		}
	}

	/// <summary>
	/// The whole board (deck, trucks, wheels) in the deck's own frame, or null without a deck model. Trucks and wheels
	/// are counted once their rest place on the deck is known.
	/// </summary>
	internal BBox? BoardBounds
	{
		get
		{
			if ( DeckBounds is not { } bounds )
				return null;
			if ( !_restCaptured )
				return bounds;
			foreach ( var (model, rest) in PartsAtRest() )
				foreach ( var corner in model.Bounds.Corners )
					bounds = bounds.AddPoint( rest.PointToWorld( corner ) );
			return bounds;
		}
	}

	/// <summary>
	/// Boxes of the trucks and wheels in the deck's own frame at their rest places, empty until those are known.
	/// </summary>
	internal IReadOnlyList<BBox> PartBounds
	{
		get
		{
			if ( !_restCaptured )
				return [];
			if ( _partBounds is not null )
				return _partBounds;
			var boxes = new List<BBox>();
			foreach ( var (model, rest) in PartsAtRest() )
				boxes.Add( BBox.FromPoints( model.Bounds.Corners.Select( corner => rest.PointToWorld( corner ) ) ) );
			return _partBounds = boxes;
		}
	}

	private List<BBox> _partBounds;

	// Each truck, wheel and baseplate model at its rest place on the deck, in the deck's own frame.
	private IEnumerable<(Model Model, Transform Place)> PartsAtRest()
	{
		foreach ( var (part, rest) in _wheels.Zip( _wheelRest ).Concat( _trucks.Zip( _truckRest ) ) )
		{
			if ( (part.IsValid() ? part.Components.Get<ModelRenderer>()?.Model : null) is { } model )
				yield return (model, rest);
		}
		foreach ( var plate in _baseplates )
		{
			if ( (plate.IsValid() ? plate.Components.Get<ModelRenderer>()?.Model : null) is { } model )
				yield return (model, plate.LocalTransform);
		}
	}

	/// <summary>
	/// Top and underside of the deck model at a point of the deck's own frame (board units, X along the deck),
	/// found by tracing the model's triangles; null where the point is off the deck or there is no deck model.
	/// </summary>
	internal (float Top, float Bottom)? DeckSurface( float x, float y )
	{
		// A board model's deck answers from its own triangles, found when it was taken apart.
		if ( _parts is not null )
		{
			if ( _parts.Shape?.Surface( x / SkateUnits.InchesPerMetre, y / SkateUnits.InchesPerMetre ) is not { } surface )
				return null;
			return (surface.Top * SkateUnits.InchesPerMetre, surface.Bottom * SkateUnits.InchesPerMetre);
		}
		var model = _deck.IsValid() ? _deck.Components.Get<ModelRenderer>()?.Model : null;
		if ( model is null )
			return null;
		var local = _deck.LocalTransform;
		var inModel = local.PointToLocal( new Vector3( x, y, 0f ) );
		if ( _generatedDeck )
		{
			if ( BoardGeometry.DeckSurface( inModel.x / SkateUnits.InchesPerMetre, inModel.y / SkateUnits.InchesPerMetre ) is not { } shape )
				return null;
			return (local.PointToWorld( inModel.WithZ( shape.Top * SkateUnits.InchesPerMetre ) ).z,
				local.PointToWorld( inModel.WithZ( shape.Bottom * SkateUnits.InchesPerMetre ) ).z);
		}
		var reach = model.Bounds.Size.Length + 1f;
		var down = model.Trace.Ray( inModel.WithZ( model.Bounds.Maxs.z + 1f ), inModel.WithZ( model.Bounds.Mins.z - reach ) ).Run();
		var up = model.Trace.Ray( inModel.WithZ( model.Bounds.Mins.z - 1f ), inModel.WithZ( model.Bounds.Maxs.z + reach ) ).Run();
		if ( !down.Hit || !up.Hit )
			return null;
		return (local.PointToWorld( down.HitPosition ).z, local.PointToWorld( up.HitPosition ).z);
	}

	/// <summary>Where the deck is shown this frame.</summary>
	internal Transform DisplayedDeck => Lifted( Tipped( Blend( Leaned( _shown ) ) ) );

	/// <summary>
	/// Where the deck was last drawn, carried along with the board since: the shown deck of a board whose own updates have
	/// stopped (carried in a hand, the board moved by the rider) rather than where its last physical pose put it.
	/// </summary>
	internal Transform DrawnDeck => _root.IsValid()
		? new Transform( _root.WorldPosition, _root.WorldRotation, _board.WorldScale ) : _board.WorldTransform;

	// The physical deck as shown this frame (smoothed between physics steps).
	private Transform _shown;

	// How far (world inches) the shown board is raised out of the rails this frame.
	private Vector3 _lift;


	/// <summary>How far (world inches) the shown board is raised out of the rails this frame.</summary>
	internal Vector3 Lift => _lift + _tipShift;

	// The drawn parts and their poses this frame, measured against the rails.
	private readonly (Model Model, Transform Pose)[] _drawn = new (Model, Transform)[ 10 ];

	internal void Update( ReadOnlySpan<DynamicBody> bodies ) => Update( bodies, true, _board.WorldTransform );

	/// <param name="bodies">Physical bodies: deck, trucks, wheels.</param>
	/// <param name="simulated">False while the board is held or carried: its bodies are not moving with it, so
	/// trucks and wheels stay on the deck at their rest offsets.</param>
	/// <param name="shown">The physical deck as shown this frame (smoothed between physics steps).</param>
	/// <param name="rails">Solids of the rails near the board (metres). The shown board, wherever an animation holds it,
	/// is raised along a rail's up just enough that no drawn part of it is inside one: it rests on the rail, never in it.
	/// The raise follows the parts' depth in the rail continuously, so it comes and goes as smoothly as the board moves.
	/// </param>
	/// <param name="onRail">The board grinds or slides on a rail: whatever pose an animation holds it in, it is on top.</param>
	internal void Update( ReadOnlySpan<DynamicBody> bodies, bool simulated, Transform shown, IReadOnlyList<RailSolid> rails = null,
		bool onRail = false )
	{
		var deck = _board.WorldTransform;
		_shown = shown;
		BeginFrame();
		var wheels = new Transform[ 4 ];
		var trucks = new Transform[ 2 ];
		for ( var i = 0; i < 4; i++ )
			// A wheel turns with its truck; its spin about the axle is shown from how it rolls (see Animate).
			wheels[ i ] = new Transform( SkateUnits.Inches( bodies[ i + 3 ].Position ),
				SkateUnits.ToEngine( bodies[ i / 2 + 1 ].Rotation ) * _rig.WheelRotations[ i ], _wheels[ i ].IsValid() ? _wheels[ i ].WorldScale : Vector3.One );
		for ( var i = 0; i < 2; i++ )
			trucks[ i ] = new Transform( SkateUnits.Inches( bodies[ i + 1 ].Position ),
				SkateUnits.ToEngine( bodies[ i + 1 ].Rotation ), _trucks[ i ].IsValid() ? _trucks[ i ].WorldScale : Vector3.One );
		// The first physical pose (the board as built, at rest) is where trucks and wheels sit on the deck.
		if ( !_restCaptured )
		{
			for ( var i = 0; i < 4; i++ )
				_wheelRest[ i ] = deck.ToLocal( wheels[ i ] );
			for ( var i = 0; i < 2; i++ )
				_truckRest[ i ] = deck.ToLocal( trucks[ i ] );
			_restCaptured = true;
		}
		// The deck dips into a turn about its trucks' pivots; trucks and wheels stay level with the ground under it.
		var deckPose = Blend( Leaned( shown ) );
		// A truck bolted to the deck leans with it, and its wheels tilt with it, staying on the ground; the wheels never come
		// closer to the deck than the room its lean leaves them.
		var tilt = deckPose.Rotation * Blend( shown ).Rotation.Inverse;
		var rise = WheelRise();
		for ( var i = 0; i < 4; i++ )
		{
			wheels[ i ] = simulated ? Part( wheels[ i ], _wheelRest[ i ], i, true, rise ) : deckPose.ToWorld( _wheelRest[ i ] );
			if ( simulated && Bolted( i / 2 ) )
				wheels[ i ] = wheels[ i ].WithRotation( tilt * wheels[ i ].Rotation );
			wheels[ i ] = wheels[ i ].WithRotation( wheels[ i ].Rotation * Spin( i ) );
		}
		for ( var i = 0; i < 2; i++ )
			trucks[ i ] = !simulated ? deckPose.ToWorld( _truckRest[ i ] )
				: Bolted( i ) ? Part( trucks[ i ], _truckRest[ i ], 4 + i, true, 0f, true )
				: Part( trucks[ i ], _truckRest[ i ], 4 + i, true );
		// A nose or tail slide's board is shown pitched up off the rail toward its middle (see SlideTip and UpdateTip).
		UpdateTip( deckPose, rails );
		if ( _tip > 0f )
		{
			deckPose = SlideTip.About( deckPose, _tipPivot, _tipTurn );
			for ( var i = 0; i < 4; i++ )
				wheels[ i ] = SlideTip.About( wheels[ i ], _tipPivot, _tipTurn );
			for ( var i = 0; i < 2; i++ )
				trucks[ i ] = SlideTip.About( trucks[ i ], _tipPivot, _tipTurn );
		}
		// Once lifted onto a rail the board stays on top of it, however deep an animation then sinks it; so does a board
		// coming down onto one, its trucks just above the rail's top: an animated board held lower than the physical one
		// (a dark catch's) rises with it onto the rail as it lands, rather than sinking in and being thrown out at the grind.
		var resting = onRail || _liftBefore != Vector3.Zero || simulated && rails is { Count: > 0 } && AtRail( rails, bodies );
		// The raise comes at once, so nothing shown is ever in a rail, and goes at LiftRelease: passing a rail's end or a
		// truck dropping back onto the rail would otherwise drop the board a fraction of an inch in a frame.
		// It may rise as far as it sits below the physical board, which physics keeps on the rail: a long board's clip board
		// handed back at a slide's lock-on sat up to 6 in below it, deeper than MaximumLift.
		var most = MathF.Max( RailClearance.MaximumLift, Vector3.Dot( deck.Position - deckPose.Position, Vector3.Up ) + 1f );
		var needed = rails is { Count: > 0 } ? RailClearance.Lift( rails, Drawn( deckPose, wheels, trucks ), resting, most ) : Vector3.Zero;
		// A board shown on its back or side is kept out of the ground under it the same way: dark side up a deck rests on
		// its kicked nose and tail, which reach below its flat middle (FloorClearance).
		var floor = FloorClearance.Lift( _board, deckPose, Drawn( deckPose, wheels, trucks ) );
		if ( floor.LengthSquared > needed.LengthSquared )
			needed = floor;
		// Settling back down follows a critically damped spring from rest (LiftRelease its rate), so it starts and stops
		// gently: an exponential ease set off at full speed the frame less raise was needed, and a darkslide reaching the
		// rail's end dropped 0.06 in in that first frame.
		if ( needed.LengthSquared >= _liftBefore.LengthSquared )
		{
			_lift = needed;
			_liftSpeed = Vector3.Zero;
		}
		else
		{
			var step = MathF.Min( Time.Delta, 1f / 30f );
			_liftSpeed = _liftSpeedBefore
				- (LiftRelease * LiftRelease * (_liftBefore - needed) + 2f * LiftRelease * _liftSpeedBefore) * step;
			_lift = _liftBefore + _liftSpeed * step;
			if ( _lift.LengthSquared <= needed.LengthSquared )
			{
				_lift = needed;
				_liftSpeed = Vector3.Zero;
			}
		}
		if ( _lift.LengthSquared < 1e-6f )
			_lift = Vector3.Zero;
		_root.WorldTransform = Lifted( deckPose );
		for ( var i = 0; i < 4; i++ )
		{
			if ( _wheels[ i ].IsValid() )
				_wheels[ i ].WorldTransform = Lifted( wheels[ i ] );
		}
		for ( var i = 0; i < 2; i++ )
		{
			if ( _trucks[ i ].IsValid() )
				_trucks[ i ].WorldTransform = Lifted( trucks[ i ] );
		}
		if ( _skinned.IsValid() )
			MoveBones( wheels, trucks );
	}

	// A board model drawn whole: each part's bone is put where the part is shown.
	private void MoveBones( Transform[] wheels, Transform[] trucks )
	{
		var model = _skinned.WorldTransform;
		for ( var part = BoardPart.FrontTruck; part <= BoardPart.RearRightWheel; part++ )
		{
			var bone = _bones[ (int)part ];
			if ( bone < 0 || bone >= _parts.Source.BoneCount )
				continue;
			var shown = part >= BoardPart.FrontLeftWheel ? wheels[ part - BoardPart.FrontLeftWheel ] : trucks[ part - BoardPart.FrontTruck ];
			var place = Lifted( shown ).ToWorld( _boneOffsets[ (int)part ] );
			_skinned.SetBoneTransform( _parts.Source.Bones.AllBones[ bone ], model.ToLocal( place ) );
		}
	}

	// How fast (per second) the shown board settles back down when less raise is needed.
	private const float LiftRelease = 20f;

	// How far above a rail's top (metres) a physical truck counts as coming down onto it, and how far out from its sides.
	private const float RailApproach = 0.06f;

	private static bool AtRail( IReadOnlyList<RailSolid> rails, ReadOnlySpan<DynamicBody> bodies )
	{
		foreach ( var rail in rails )
		{
			for ( var truck = 1; truck <= 2; truck++ )
			{
				if ( rail.AtTop( bodies[ truck ].Position, RailApproach, RailContacts.GrooveHalfWidth ) )
					return true;
			}
		}
		return false;
	}

	/// <summary>The grind the board is in, set before each <see cref="Update(ReadOnlySpan{DynamicBody}, bool, Transform,
	/// IReadOnlyList{RailSolid}, bool)"/>: a nose or tail slide pitches the shown board (SlideTip).</summary>
	internal SkateGrind Grind { get; set; }

	/// <summary>The slide's deck lies over the top of a ledge: a blunt pitched to SlideTip.BackslashPitch.</summary>
	internal bool GrindOnLedgeTop { get; set; }

	/// <summary>The rider's animation is still in the slide (see IBoardDisplay.HoldSlidePitch): its pitch is held off the
	/// rail until the animation leaves it.</summary>
	internal bool HoldSlide { get; set; }

	// How far the shown board is pitched toward the slide's (0 to 1, eased), about what pivot and axis in the deck's frame,
	// by how many degrees at full, and this frame's turn, its pivot and how far it moves the deck's centre.
	private float _tip;
	private Vector3 _tipPivotLocal;
	private Vector3 _tipAxisLocal = Vector3.Left;
	private float _tipAngle = SlideTip.Pitch;
	private Rotation _tipTurn = Rotation.Identity;
	private Vector3 _tipPivot;
	private Vector3 _tipShift;

	// Seconds the shown board takes to pitch into a slide, and to level again once off the rail: nothing holds it
	// pitched there, and the air clip that follows stands the feet on a level board (still half pitched a tenth of a
	// second after a noseblunt, the leading ankle folded to 58 degrees as the air clip began).
	private const float TipEase = 0.25f;
	private const float TipLeave = 0.12f;

	// The slide's pitch this frame: found on the rail under the deck's end while sliding, eased in and, about the last
	// pivot found, eased out after.
	private void UpdateTip( Transform deck, IReadOnlyList<RailSolid> rails )
	{
		var end = Grind == SkateGrind.Noseslide ? 1 : Grind == SkateGrind.Tailslide ? -1 : 0;
		var found = end != 0 && rails is { Count: > 0 } && DeckBounds is { } bounds ? SlideTip.Find( deck, bounds, rails, end ) : null;
		if ( found is { } tip )
		{
			_tipPivotLocal = deck.PointToLocal( tip.Pivot );
			_tipAxisLocal = deck.Rotation.Inverse * tip.Axis;
			_tipAngle = GrindOnLedgeTop ? SlideTip.BackslashPitch : SlideTip.Pitch;
		}
		// Off the rail the pitch is held while the rider's slide clip plays on (it holds the feet on the pitched nose or tail),
		// and eases out once the animation leaves the slide: let go at the rail's end, the board levelled under the clip's
		// feet and a noseblunt's front ankle folded to 49 degrees.
		_tip = found is null && HoldSlide ? _tipBefore
			: Math.Clamp( _tipBefore + (found is not null ? Time.Delta / TipEase : -Time.Delta / TipLeave), 0f, 1f );
		if ( _tip <= 0f )
		{
			_tipTurn = Rotation.Identity;
			_tipShift = Vector3.Zero;
			return;
		}
		_tipPivot = deck.PointToWorld( _tipPivotLocal );
		// A clip's board taking over in the air off the rail has its own pitch: the slide's gives way to it, so the feet the
		// clip puts on its board are not tipped off it (an ankle folded 10 degrees in a frame as a noseblunt's air began).
		_tipTurn = Rotation.FromAxis( deck.Rotation * _tipAxisLocal, _tip * _tip * (3f - 2f * _tip) * _tipAngle * (1f - _displayWeight) );
		_tipShift = SlideTip.About( deck, _tipPivot, _tipTurn ).Position - deck.Position;
		// The turn lifts the deck's middle: the other way round it would drop it into the rail.
		if ( _tipShift.Dot( Vector3.Up ) < 0f )
		{
			_tipAxisLocal = -_tipAxisLocal;
			_tipTurn = _tipTurn.Inverse;
			_tipShift = SlideTip.About( deck, _tipPivot, _tipTurn ).Position - deck.Position;
		}
	}

	// The rail lift and the slide pitch as the last frame ended. Update runs several times in a frame (again each time an
	// animation sets the display pose), and each run eases on from these: easing on from the run before, the lift let go
	// once a run and an earlier run's pose held it up, so it rose and fell within a frame and jittered from frame to frame.
	private float _frameAt = float.NaN;
	private Vector3 _liftBefore;
	private float _tipBefore;

	// How fast (world inches a second) the shown board is settling back down out of a rail raise, now and as the last
	// frame ended.
	private Vector3 _liftSpeed;
	private Vector3 _liftSpeedBefore;

	private void BeginFrame()
	{
		if ( Time.Now == _frameAt )
			return;
		_frameAt = Time.Now;
		_liftBefore = _lift;
		_liftSpeedBefore = _liftSpeed;
		_tipBefore = _tip;
	}

	// A pose of the shown deck with this frame's slide pitch.
	private Transform Tipped( Transform pose ) => _tip > 0f ? SlideTip.About( pose, _tipPivot, _tipTurn ) : pose;

	private Transform Lifted( Transform pose ) => _lift == Vector3.Zero ? pose : pose.WithPosition( pose.Position + _lift );

	// Each drawn part's model at the pose it is about to be shown at.
	private ReadOnlySpan<(Model Model, Transform Pose)> Drawn( Transform deck, Transform[] wheels, Transform[] trucks )
	{
		var count = 0;
		if ( _deck.IsValid() )
			_drawn[ count++ ] = (_deck.Components.Get<ModelRenderer>()?.Model, deck.ToWorld( _deck.LocalTransform ));
		for ( var i = 0; i < 4; i++ )
		{
			if ( _wheels[ i ].IsValid() )
				_drawn[ count++ ] = (_wheels[ i ].Components.Get<ModelRenderer>()?.Model, wheels[ i ]);
		}
		for ( var i = 0; i < 2; i++ )
		{
			if ( _trucks[ i ].IsValid() )
				_drawn[ count++ ] = (_trucks[ i ].Components.Get<ModelRenderer>()?.Model, trucks[ i ]);
		}
		for ( var i = 0; i < 2; i++ )
		{
			if ( _baseplates[ i ].IsValid() && _deck.IsValid() )
				_drawn[ count++ ] = (_baseplates[ i ].Components.Get<ModelRenderer>()?.Model,
					deck.ToWorld( _deck.LocalTransform ).ToWorld( _baseplates[ i ].LocalTransform ));
		}
		if ( _skinned.IsValid() )
			_drawn[ count++ ] = (_parts.Source, deck.ToWorld( _parts.Placement ));
		// A part of thousands of triangles is measured by its cheaper stand-in, which errs outward (BoardModelParts.Clearance):
		// the full mesh every frame held a 125k-triangle board to 50 fps on a rail. What is drawn is unchanged.
		for ( var i = 0; i < count; i++ )
			_drawn[ i ] = (_parts?.Clearance( _drawn[ i ].Model ) ?? _drawn[ i ].Model, _drawn[ i ].Pose);
		return _drawn.AsSpan( 0, count );
	}

	// A truck or wheel shown on the shown deck: where physics holds it relative to the deck (steering, spin), but
	// never further from its rest place than the joints should allow, so springy joints never show the parts
	// separating at take-off or landing. While the display pose is shown it settles fully onto its rest place.
	// Each truck's and wheel's place and turn on the deck as shown: physics moves them a step at a time, and shown at once
	// each step they bob at the physics rate (and the board's rail clearance with them). Like the deck itself they are
	// recorded at each physics step (RecordStep) and shown between the last two, on the deck's own clock: timed by the
	// frame that first sees a step instead, at 60 steps and 90 frames a second, they ran unevenly against the deck and
	// the clearance stepped as a board tipped off a rail's end. A wheel's spin is added after.
	private readonly StepSmoother[] _partSteps = { new(), new(), new(), new(), new(), new() };

	/// <summary>
	/// Records where physics has the trucks and wheels on the deck after a fixed step (<paramref name="now"/>, of
	/// <paramref name="step"/> seconds), so frames show them between steps as the deck is (see <see cref="StepSmoother"/>).
	/// </summary>
	internal void RecordStep( ReadOnlySpan<DynamicBody> bodies, float now, float step )
	{
		if ( !_restCaptured )
			return;
		var deck = _board.WorldTransform;
		for ( var i = 0; i < 4; i++ )
			_partSteps[ i ].Record( PartOnDeck( deck, new Transform( SkateUnits.Inches( bodies[ i + 3 ].Position ),
				SkateUnits.ToEngine( bodies[ i / 2 + 1 ].Rotation ) * _rig.WheelRotations[ i ] ), _wheelRest[ i ] ), now, step );
		for ( var i = 0; i < 2; i++ )
			_partSteps[ 4 + i ].Record( PartOnDeck( deck, new Transform( SkateUnits.Inches( bodies[ i + 1 ].Position ),
				SkateUnits.ToEngine( bodies[ i + 1 ].Rotation ) ), _truckRest[ i ] ), now, step );
	}

	// A part's pose on the deck, kept within PartSlack of its rest place.
	private static Transform PartOnDeck( Transform deck, Transform physical, Transform rest )
	{
		var local = deck.ToLocal( physical );
		var offset = local.Position - rest.Position;
		return offset.Length > PartSlack ? local.WithPosition( rest.Position + offset.Normal * PartSlack ) : local;
	}

	// A part rises toward the deck at most rise inches above its rest place; a leaned one is placed on the leaned deck.
	private Transform Part( Transform physical, Transform rest, int slot, bool turn, float rise = float.PositiveInfinity,
		bool leaned = false )
	{
		var local = PartOnDeck( _board.WorldTransform, physical, rest );
		var stepped = _partSteps[ slot ].At( local, Time.Now );
		local = new Transform( stepped.Position, turn ? stepped.Rotation : local.Rotation, local.Scale );
		if ( local.Position.z > rest.Position.z + rise )
			local = local.WithPosition( local.Position.WithZ( rest.Position.z + rise ) );
		if ( _displayWeight > 0f )
			local = new Transform( Vector3.Lerp( local.Position, rest.Position, _displayWeight ),
				Rotation.Slerp( local.Rotation, rest.Rotation, _displayWeight ), local.Scale );
		return Blend( leaned ? Leaned( _shown ) : _shown ).ToWorld( local );
	}

	// Whether a truck is bolted to the deck and leans with it (a rig made before this was measured has none).
	private bool Bolted( int truck ) => _rig.BoltedTrucks is { } bolted && bolted[ truck ];

	// How far (inches) the wheels may come up toward the deck at its lean now: the room they have under the level deck,
	// less what the lean takes (all of it at the most the deck leans, BoardRig.LeanLimit).
	private float WheelRise()
	{
		var room = _rig.WheelRoom;
		var limit = _rig.LeanLimit;
		if ( !float.IsFinite( room ) )
			return float.PositiveInfinity;
		return !float.IsFinite( limit ) || limit <= 0f ? room : room * MathF.Max( 0f, 1f - MathF.Abs( Roll ) / limit );
	}

	// Inches a truck or wheel may sit away from its rest place on the deck.
	private const float PartSlack = 0.15f;

	/// <summary>
	/// Physics turned the board over in one step (<paramref name="before"/> to <paramref name="after"/>, righted for a rider
	/// getting on): the shown board takes that turn back and lets it go over <see cref="TurnOverTime"/>, so it rolls over.
	/// </summary>
	internal void BeginTurnOver( Transform before, Transform after )
	{
		_turnOver = before.Rotation * after.Rotation.Inverse;
		_turnShift = before.Position - after.Position;
		_turnStart = Time.Now;
	}

	private Rotation _turnOver = Rotation.Identity;
	private Vector3 _turnShift;
	private float _turnStart = float.NegativeInfinity;

	// Seconds a board turned over by physics takes to roll over as shown: done before a reaching hand takes it.
	private const float TurnOverTime = 0.3f;

	// A pose of the board's (deck or part) with what is left of a turn over, about the physical deck's middle.
	private Transform TurningOver( Transform pose )
	{
		var t = (Time.Now - _turnStart) / TurnOverTime;
		if ( !(t < 1f) || t < 0f )
			return pose;
		var remaining = 1f - t * t * (3f - 2f * t);
		var turn = Rotation.Slerp( Rotation.Identity, _turnOver, remaining );
		var middle = _shown.Position;
		return new Transform( middle + turn * (pose.Position - middle) + _turnShift * remaining, turn * pose.Rotation, pose.Scale );
	}

	private Transform Blend( Transform physical ) => TurningOver( Displayed( physical ) );

	// A part's physical pose moved with the deck from its physical pose to the display pose.
	private Transform Displayed( Transform physical )
	{
		if ( _displayWeight <= 0f )
			return physical;
		// A pose relative to the shown deck moved with it onto the display pose.
		var local = _shown.ToLocal( physical );
		var shown = _display.ToWorld( local );
		return new Transform( Vector3.Lerp( physical.Position, shown.Position, _displayWeight ),
			Rotation.Slerp( physical.Rotation, shown.Rotation, _displayWeight ), physical.Scale );
	}

	internal void Destroy()
	{
		if ( _root.IsValid() )
			_root.Destroy();
	}

	private static GameObject AddModel( GameObject parent, string name, Model model )
	{
		var child = new GameObject( parent, true, name );
		child.Components.Create<ModelRenderer>().Model = model;
		return child;
	}

	// A board model drawn whole at its place on the deck, its bones left to be moved with its parts.
	private static SkinnedModelRenderer AddSkinned( GameObject parent, string name, BoardModelParts parts )
	{
		var child = new GameObject( parent, true, name ) { LocalTransform = parts.Placement };
		var renderer = child.Components.Create<SkinnedModelRenderer>();
		renderer.UseAnimGraph = false;
		renderer.Model = parts.Source;
		return renderer;
	}
}
