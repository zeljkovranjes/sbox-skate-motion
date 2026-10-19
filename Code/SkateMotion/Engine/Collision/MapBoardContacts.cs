namespace SkateMotion.Engine.Collision;

using Sandbox;
using SkateMotion.Core;
using SkateMotion.Core.Collision;
using SkateMotion.Core.Constraints;
using SkateMotion.Core.Vehicle;
using NumericsVector = System.Numerics.Vector3;

/// <summary>Binds static map triangles to the board's existing owner and retains other native contacts.</summary>
internal sealed class MapBoardContacts
{
	/// <summary>Share of the grind friction applied this step (it builds up over a grind).</summary>
	internal float GrindFrictionScale { get; set; } = 1f;

	private const float TruckDiameterScale = .5375f;
	private const float TruckLengthScale = 1.35f;
	private const float DefaultWheelWidth = .095f;

	private readonly Rigidbody _deck;
	private readonly SkateSettings _settings;
	private readonly MapCollisionSource _source;
	private readonly NumericsVector[] _front = new NumericsVector[ ArticulatedBoard.DynamicCount ];
	private readonly MapContactStream _stream;
	private readonly BoardCollisionInputs _inputs;
	private readonly uint[] _staticState = new uint[ 44 ];
	private readonly CollisionMapContactContext _policy;
	private readonly BoardContactBodyBinding[] _bindings = new BoardContactBodyBinding[ 8 ];
	private readonly Func<PhysicsShape, bool> _represented;
	private bool _refresh = true;
	private bool _slide;

	internal int ContactCount => _stream.Count;
	internal int TriangleCount => _stream.TriangleCount;

	internal MapBoardContacts( Rigidbody deck, SkateSettings settings, float radius,
		CollisionMapContactContext policy, uint duplicateSquaredTolerance, bool deferredReduction )
	{
		_deck = deck;
		_settings = settings;
		_policy = policy;
		_source = new( new MeshWorldQuery( 1e-8f, -.00001f,
			2.938735877055719e-39f, 1.52587890625e-5f, 1e-6f ) );
		var truckRadius = (TruckDiameterScale * .5f) * radius;
		var truckHalfLength = (TruckLengthScale * .5f) * DefaultWheelWidth;
		_inputs = BoardCollisionInputs.CreateRoundParts( new NumericsVector( .8f, .22f, .025f ),
			truckRadius, truckHalfLength, radius );
		_stream = new( _source, Bind, duplicateSquaredTolerance, deferredReduction );
		_represented = _stream.ContainsShape;
		for ( var i = 0; i < ArticulatedBoard.DynamicCount; ++i )
			_bindings[ i ] = new( (uint)i + 1, i );
		_bindings[ 7 ] = new( 8, ArticulatedBoard.StaticMapBodyIndex );
	}

	internal void Refresh()
	{
		_refresh = true;
		_stream.Reset();
	}

	internal SkateContactState[] CaptureContacts( ArticulatedContacts native )
	{
		var remaining = native.WithoutGeneratedShapes( _represented );
		var result = new SkateContactState[ _stream.Count + remaining.Length ];
		for ( var i = 0; i < _stream.Count; ++i )
		{
			var record = _stream.Records.Slice( i * GeneratedContactRecord.WordCount, GeneratedContactRecord.WordCount );
			var part = checked((int)record[ 3 ] - 1);
			var contact = GeneratedContactRecord.Decode( record, part, ArticulatedBoard.StaticMapBodyIndex ).Geometry;
			result[ i ] = new( (SkateBodyPart)part, SkateUnits.ToEngine( contact.PointA ),
				SkateUnits.ToEngine( contact.PointB ), SkateUnits.ToEngine( contact.Normal ), contact.StaticFriction );
		}
		for ( var i = 0; i < remaining.Length; ++i )
		{
			var contact = remaining[ i ];
			result[ _stream.Count + i ] = new( (SkateBodyPart)contact.BodyA, SkateUnits.ToEngine( contact.PointA ),
				SkateUnits.ToEngine( contact.PointB ), SkateUnits.ToEngine( contact.Normal ), contact.StaticFriction );
		}
		return result;
	}

	internal void Step( ArticulatedBoard board, ArticulatedContacts native,
		bool slide, float steering, float step )
	{
		if ( _refresh )
		{
			// The host exposes shape friction but no readable per-face material table.
			_source.Refresh( _deck.Scene.PhysicsWorld, static (_, _) => 0 );
			_refresh = false;
		}
		_slide = slide;
		CollisionBodyCache.Write( board.StaticMapBody, 1, _staticState );
		// Profile friction is supplied by the map side of the max/max/min material combination.
		_inputs.Update( board.Bodies, 0, 0, 0f, 0f, 0f );
		// Faces any board body was behind before this step stay out of it (a thin ramp's back, or its underside).
		for ( var i = 0; i < _front.Length; ++i )
			_front[ i ] = board.Bodies[ i ].Position;
		_source.FrontReferences = _front;
		_stream.Generate( _inputs.Bodies );
		board.StepGenerated( _stream.Records, _bindings,
			native.WithoutGeneratedShapes( _represented ), steering, 0f, step );
	}

	private CollisionMapContactContext? Bind( PhysicsShape shape )
	{
		// Stationary scene colliders can be keyframed. Their motion, not the enum,
		// determines whether the zero-mass world binding represents them.
		if ( !shape.IsValid() || !shape.Body.IsValid() || shape.Body.MotionEnabled
			|| shape.Body.Velocity != Vector3.Zero || shape.Body.AngularVelocity != Vector3.Zero
			|| shape.SurfaceVelocity != Vector3.Zero )
			return null;
		for ( var owner = shape.Collider?.GameObject; owner.IsValid(); owner = owner.Parent )
			if ( owner == _deck.GameObject || owner.Tags.Has( SkateTags.Rider ) )
				return null;
		// A negative shape value inherits the surface; an explicit zero remains frictionless.
		var surfaceFriction = shape.Friction;
		if ( surfaceFriction < 0f )
			surfaceFriction = shape.Surface?.Friction ?? 1f;
		var friction = _settings.Grip * Math.Clamp( surfaceFriction, 0f, 4f );
		if ( _slide ) friction *= .35f;
		if ( shape.Collider.IsValid() && shape.Collider.GameObject.Tags.Has( SkateTags.Grind ) )
			friction = _settings.GrindFriction * GrindFrictionScale;
		return _policy with
		{
			BodyState = _staticState,
			BodyIdentity = 8,
			Category = 0,
			StaticFriction = BitConverter.SingleToUInt32Bits( friction ),
			SlidingFriction = BitConverter.SingleToUInt32Bits( friction * .8f ),
			Restitution = 0
		};
	}
}
