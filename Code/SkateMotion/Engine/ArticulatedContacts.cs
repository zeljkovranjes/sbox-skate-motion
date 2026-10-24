using Sandbox;
using SkateMotion.Core;
using SkateMotion.Core.Constraints;
using SkateMotion.Core.Grinding;
using SkateMotion.Core.Vehicle;
using SkateMotion.Engine.Collision;

namespace SkateMotion.Engine;

using NumericsVector = System.Numerics.Vector3;
using NumericsMatrix = System.Numerics.Matrix4x4;
using NumericsRotation = System.Numerics.Quaternion;

internal sealed class ArticulatedContacts
{
	/// <summary>Share of the grind friction applied this step (it builds up over a grind).</summary>
	internal float GrindFrictionScale { get; set; } = 1f;

	private const float BackSideDepth = 0.03f;
	private readonly Rigidbody _deck;
	private readonly SkateSettings _settings;
	private readonly float _radius;
	private readonly CollisionContact[] _contacts = new CollisionContact[ ArticulatedBoard.ContactCapacity ];
	private readonly CollisionContact[] _unrepresented = new CollisionContact[ ArticulatedBoard.ContactCapacity ];
	private readonly PhysicsShape[] _contactShapes = new PhysicsShape[ ArticulatedBoard.ContactCapacity ];
	private readonly PhysicsBody[] _surfaces = new PhysicsBody[ ArticulatedBoard.ContactCapacity ];
	private int _surfaceCount;
	private int _count;
	private readonly DeckManifold _manifold;
	private readonly DeckManifold _staticManifold;
	private readonly MeshWheelContacts _mesh;
	private bool _recover;
	private readonly NumericsVector[] _wheelNormals = new NumericsVector[ 4 ];
	private readonly NumericsVector[] _wheelPoints = new NumericsVector[ 4 ];
	private readonly bool[] _wheelHit = new bool[ 4 ];
	private readonly PhysicsBody[] _wheelBodies = new PhysicsBody[ 4 ];
	private readonly bool[] _wheelTouching = new bool[ 4 ];
	private readonly bool[] _wheelTouchedBefore = new bool[ 4 ];
	private readonly NumericsVector[] _previousCenters = new NumericsVector[ 4 ];
	private bool _havePreviousCenters;
	internal bool HasDeepPenetration { get; private set; }
	internal GrindLock Grind { get; private set; }
	internal ReadOnlySpan<CollisionContact> Latest => _contacts.AsSpan( 0, _count );

	internal ReadOnlySpan<CollisionContact> WithoutGeneratedShapes( Func<PhysicsShape, bool> represented )
	{
		ArgumentNullException.ThrowIfNull( represented );
		var count = 0;
		for ( var i = 0; i < _count; ++i )
		{
			var shape = _contactShapes[ i ];
			if ( shape.IsValid() && represented( shape ) ) continue;
			_unrepresented[ count++ ] = _contacts[ i ];
		}
		return _unrepresented.AsSpan( 0, count );
	}

	/// <param name="shapes">The board's read collision shapes, shared with its contact generation.</param>
	internal ArticulatedContacts( Rigidbody deck, SkateSettings settings, float radius, MapShapeCache shapes )
	{
		_deck = deck;
		_settings = settings;
		_radius = radius;
		_mesh = new( shapes );
		_manifold = new( deck );
		_staticManifold = new( deck, true );
	}

	internal void Reset( NumericsVector up )
	{
		_count = 0;
		_surfaceCount = 0;
		Array.Clear( _contactShapes );
		_recover = true;
		_manifold.Reset( up );
		_staticManifold.Reset( up );
		Array.Clear( _wheelHit );
		Array.Clear( _wheelBodies );
		Array.Clear( _wheelTouching );
		Array.Clear( _wheelTouchedBefore );
		_havePreviousCenters = false;
		_mesh.Clear();
	}

	/// <param name="railSlide">The deck slides across a rail: the board's own collision with rails is left to the rail
	/// contact.</param>
	internal ReadOnlySpan<CollisionContact> Sample( ArticulatedBoard assembly, float step, bool slide,
		ReadOnlySpan<RailSegment> rails, bool railSlide = false )
	{
		_count = 0;
		_surfaceCount = 0;
		Array.Clear( _contactShapes );
		HasDeepPenetration = false;
		var up = NumericsVector.Transform( NumericsVector.UnitZ, assembly.Body( 0 ).Rotation );
		Array.Copy( _wheelTouching, _wheelTouchedBefore, 4 );
		Array.Clear( _wheelTouching );
		for ( var wheel = 3; wheel < 7; wheel++ )
		{
			ref var body = ref assembly.Body( wheel );
			var index = wheel - 3;
			var direction = _wheelHit[ index ] ? _wheelNormals[ index ] : up;
			// A wheel's last support has moved on with what it was on: a prop lifted under the board (held by a physics
			// tool) rose several centimetres a step. Probed from just above where it had been, each wheel's probe began
			// inside it and was thrown out; and probed down only to where the wheel itself would be, the probe of a board
			// rising with the prop stopped short of it, so the board lost its wheels, fell back onto the prop, was knocked
			// up again, and at a quick lift sank in and dropped through. Both ends of the probe go by the support's motion,
			// taken from a support the wheel touched last step.
			var moved = NumericsVector.Zero;
			if ( _wheelTouchedBefore[ index ] && _wheelBodies[ index ].IsValid() )
				moved = SkateUnits.Metres( _wheelBodies[ index ].GetVelocityAtPoint( SkateUnits.Inches( _wheelPoints[ index ] ) ) ) * step;
			var predicted = body.Position + (body.Velocity + body.Acceleration * step) * step - moved;
			var lift = _recover ? 0.15f : 0.02f;
			if ( _wheelHit[ index ] )
			{
				var gap = NumericsVector.Dot( body.Position - (_wheelPoints[ index ] + moved), direction ) - _radius;
				lift = Math.Max( lift, Math.Clamp( -gap + 0.01f, 0f, 0.15f ) );
			}
			var start = body.Position + direction * lift;
			var end = predicted - direction * 0.015f;
			var query = _deck.Scene.Trace.Sphere( _radius * SkateUnits.InchesPerMetre,
				SkateUnits.Inches( start ), SkateUnits.Inches( end ) )
				.UseHitPosition().IgnoreGameObjectHierarchy( _deck.GameObject ).WithoutTags( SkateTags.Rider );
			var trace = query.Run();
			if ( trace.Body.IsValid() && trace.Body.MotionEnabled )
			{
				// Keep terrain support when a dynamic body covers a wheel probe.
				var terrain = query.IgnoreDynamic().Run();
				if ( terrain.Hit && !terrain.StartedSolid && !MeshFaces.IsBackFace( _deck.Scene, terrain, _deck.GameObject )
					&& _mesh.Refine( ref terrain, start, end, _radius ) )
					Add( assembly, wheel, body.Position - SkateUnits.ToCore( terrain.Normal ) * _radius, terrain, slide );
			}
			if ( !trace.Hit || trace.StartedSolid || MeshFaces.IsBackFace( _deck.Scene, trace, _deck.GameObject )
				|| railSlide && trace.HasTag( SkateTags.Grind ) || !_mesh.Refine( ref trace, start, end, _radius ) )
				continue;
			var normal = SkateUnits.ToCore( trace.Normal );
			// A lifted support probe can cross a thin mesh from its back side. Only use
			// the face when the actual wheel approached from the side reported by its normal.
			if ( !_recover && _havePreviousCenters && trace.Shape.IsValid() && trace.Shape.IsMeshShape
				&& NumericsVector.Dot( _previousCenters[ index ] + moved - SkateUnits.Metres( trace.HitPosition ), normal ) < -0.001f )
				continue;
			_wheelNormals[ index ] = normal;
			_wheelPoints[ index ] = SkateUnits.Metres( trace.HitPosition );
			_wheelHit[ index ] = true;
			_wheelTouching[ index ] = true;
			_wheelBodies[ index ] = trace.Body;
			Add( assembly, wheel, body.Position - normal * _radius, trace, slide );
		}
		foreach ( var hit in _manifold.Sample( assembly.Deck, assembly.Body( 0 ).Acceleration, step, _recover ) )
		{
			if ( !(railSlide && hit.Trace.HasTag( SkateTags.Grind )) )
				Add( assembly, 0, SkateUnits.Metres( hit.Point ), hit.Trace, slide );
		}
		if ( _manifold.HasDynamicObstruction )
		{
			foreach ( var hit in _staticManifold.Sample( assembly.Deck, assembly.Body( 0 ).Acceleration, step, _recover ) )
			{
				if ( !(railSlide && hit.Trace.HasTag( SkateTags.Grind )) )
					Add( assembly, 0, SkateUnits.Metres( hit.Point ), hit.Trace, slide );
			}
		}
		Grind = rails.IsEmpty ? default
			: RailContacts.Build( assembly, rails, GetSurface( assembly, null ), _settings.GrindFriction * GrindFrictionScale, _contacts, ref _count );
		for ( var i = 0; i < 4; i++ )
			_previousCenters[ i ] = assembly.Body( i + 3 ).Position;
		_havePreviousCenters = true;
		_recover = false;
		return _contacts.AsSpan( 0, _count );
	}

	/// <summary>
	/// After the step's solve: what the wheels stand on, still rising into the board faster than the board (a prop lifted by
	/// a physics tool, whose hold gives way to no weight), carries the whole board up to its speed, as a lift carries what
	/// stands on it, and never past it. The shared solve weighs the prop as free to be pushed back down: a ridden board (80
	/// kg) came out of it at half the speed of a prop lifted under it, was pushed back out of the prop step after step, and
	/// at a quick lift sank in and dropped through. A board no one rides (3 kg) is already carried at the prop's speed by
	/// the solve; carried again before it, it was thrown 30 in up off the prop.
	/// </summary>
	internal void CarryOnSupport( ArticulatedBoard assembly )
	{
		var carry = 0f;
		var along = NumericsVector.Zero;
		for ( var i = 0; i < 4; i++ )
		{
			if ( !_wheelTouching[ i ] || !_wheelBodies[ i ].IsValid() || !_wheelBodies[ i ].MotionEnabled )
				continue;
			var surface = SkateUnits.Metres( _wheelBodies[ i ].GetVelocityAtPoint( SkateUnits.Inches( _wheelPoints[ i ] ) ) );
			var rise = NumericsVector.Dot( surface - assembly.Body( i + 3 ).Velocity, _wheelNormals[ i ] );
			if ( rise > carry )
			{
				carry = rise;
				along = _wheelNormals[ i ];
			}
		}
		if ( !(carry > 0f) )
			return;
		for ( var part = 0; part < ArticulatedBoard.DynamicCount; part++ )
			assembly.Body( part ).Velocity += along * carry;
	}

	internal void ApplyReactions( ArticulatedBoard assembly, float step )
	{
		// Include each prop's mass and inertia in the shared solve. Transfer only its
		// kinetic response; positional correction must not become engine velocity.
		for ( var i = 0; i < _surfaceCount; i++ )
		{
			var surface = _surfaces[ i ];
			if ( !surface.IsValid() || !surface.MotionEnabled )
				continue;
			ref readonly var response = ref assembly.Response( ArticulatedBoard.DynamicCount + i );
			surface.Velocity += SkateUnits.Inches( response.LinearVelocity / step );
			surface.AngularVelocity += SkateUnits.ToEngine( response.AngularVelocity / step );
		}
	}

	private void Add( ArticulatedBoard assembly, int bodyIndex, NumericsVector pointA, SceneTraceResult trace, bool slide )
	{
		var pointB = SkateUnits.Metres( trace.HitPosition );
		var normal = NumericsVector.Normalize( SkateUnits.ToCore( trace.Normal ) );
		// Map meshes are one-sided: while the deck is behind a face, no part of the board touches it.
		// Sweeps stop real approaches before deep overlap, so a part far behind a face came through
		// it from the back side; recovering that overlap would lift the whole board.
		if ( !_recover && trace.Shape.IsValid() && trace.Shape.IsMeshShape
			&& (NumericsVector.Dot( assembly.Body( 0 ).Position - pointB, normal ) < -0.001f
				|| NumericsVector.Dot( pointA - pointB, normal ) < -BackSideDepth) )
			return;
		HasDeepPenetration |= NumericsVector.Dot( pointA - pointB, normal ) < -0.005f;
		// A spinning wheel's local X is not its rolling direction. Use its axle instead.
		var axle = NumericsVector.Transform( NumericsVector.UnitY, assembly.Body( bodyIndex ).Rotation );
		var tangent = NumericsVector.Cross( axle, normal );
		if ( tangent.LengthSquared() < 0.0001f )
			tangent = NumericsVector.Cross( Math.Abs( normal.Z ) < 0.9f ? NumericsVector.UnitZ : NumericsVector.UnitX, normal );
		tangent = NumericsVector.Normalize( tangent );
		var friction = _settings.Grip * (trace.Surface is null ? 1f : Math.Clamp( trace.Surface.Friction, 0f, 4f ));
		if ( slide )
			friction *= 0.35f;
		if ( trace.HasTag( SkateTags.Grind ) )
			friction = _settings.GrindFriction * GrindFrictionScale;
		var surface = GetSurface( assembly, trace.Body );
		_contactShapes[ _count ] = trace.Shape;
		_contacts[ _count++ ] = new CollisionContact( bodyIndex, surface, pointA, pointB, normal, tangent,
			NumericsVector.Cross( normal, tangent ), friction, friction * 0.8f, 0f ).WithProjectedSurfacePoint();
	}

	private int GetSurface( ArticulatedBoard assembly, PhysicsBody surface )
	{
		for ( var i = 0; i < _surfaceCount; i++ )
		{
			if ( _surfaces[ i ] == surface )
				return ArticulatedBoard.DynamicCount + i;
		}
		var index = ArticulatedBoard.DynamicCount + _surfaceCount;
		_surfaces[ _surfaceCount++ ] = surface;
		ref var body = ref assembly.Body( index );
		body = default;
		body.Rotation = NumericsRotation.Identity;
		if ( !surface.IsValid() )
			return index;
		body.Position = SkateUnits.Metres( surface.MassCenter );
		body.Velocity = SkateUnits.Metres( surface.Velocity );
		body.AngularVelocity = SkateUnits.ToCore( surface.AngularVelocity );
		if ( !surface.MotionEnabled || surface.Mass <= 0f )
			return index;
		body.InverseMass = 1f / surface.Mass;
		var moments = SkateUnits.ToCore( surface.Inertia ) / SkateUnits.InertiaScale;
		var basis = NumericsMatrix.CreateFromQuaternion( SkateUnits.ToCore( surface.Rotation * surface.InertiaRotation ) );
		body.WorldInverseInertia = NumericsMatrix.Transpose( basis )
			* NumericsMatrix.CreateScale( NumericsVector.One / NumericsVector.Max( moments, new NumericsVector( 0.000001f ) ) ) * basis;
		return index;
	}
}
