using SkateMotion.Core.Constraints;
using SkateMotion.Core.Animation;
using SkateMotion.Core.Physics;

namespace SkateMotion.Core.Vehicle;

using Vector3 = System.Numerics.Vector3;
using Quaternion = System.Numerics.Quaternion;
using Matrix4x4 = System.Numerics.Matrix4x4;

internal sealed partial class ArticulatedBoard
{
	internal const int DynamicCount = 7;
	internal const int ContactCapacity = 24;
	private const int TargetBodyIndex = DynamicCount + ContactCapacity;
	private const int FirstHandBodyIndex = TargetBodyIndex + 1;
	internal const int StaticMapBodyIndex = FirstHandBodyIndex + 2;
	private readonly DynamicBody[] _bodies = new DynamicBody[ StaticMapBodyIndex + 1 ];
	private readonly SolverBody[] _workspace = new SolverBody[ StaticMapBodyIndex + 1 ];
	private readonly bool[] _handConfigured = new bool[ 2 ];
	private readonly BodyDynamicsSettings[] _integration = new BodyDynamicsSettings[ DynamicCount ];
	private readonly Vector3[] _origins = new Vector3[ DynamicCount ];
	private readonly JointConstraint[] _joints = new JointConstraint[ 6 ];
	private readonly DriveConstraint[] _drives = new DriveConstraint[ 5 ];
	private ContactConstraint[] _contacts = new ContactConstraint[ ContactCapacity ];
	private readonly SkateSettings _settings;
	private bool _mirrorFirst;
	private bool _targetConfigured;
	private int _driveCount;
	private readonly float _radius;
	private readonly Quaternion _axleFrame = Quaternion.CreateFromAxisAngle( Vector3.UnitZ, MathF.PI * 0.5f );
	private readonly Quaternion _steeringFrame = Quaternion.CreateFromAxisAngle( Vector3.UnitY, -MathF.PI * 0.5f );

	internal ArticulatedBoard( SkateSettings settings, ReadOnlySpan<Vector3> wheelCenters, float radius )
	{
		settings.Validate();
		if ( wheelCenters.Length != 4 || !float.IsFinite( radius ) || radius <= 0f )
			throw new ArgumentException( "An articulated board needs four wheel centers and a positive radius." );
		_settings = settings;
		_radius = radius;
		_bodies[ StaticMapBodyIndex ] = new DynamicBody { Rotation = Quaternion.Identity, MotionFlags = 1 };
		for ( var i = 0; i < 4; i++ )
			_origins[ i + 3 ] = wheelCenters[ i ];
		_origins[ 1 ] = (wheelCenters[ 0 ] + wheelCenters[ 1 ]) * 0.5f;
		_origins[ 2 ] = (wheelCenters[ 2 ] + wheelCenters[ 3 ]) * 0.5f;
		SetRiderAttached( true );
		Reset( Vector3.Zero, Quaternion.Identity, Vector3.Zero, Vector3.Zero );
	}

	internal ref DynamicBody Body( int index )
	{
		if ( index == StaticMapBodyIndex )
			throw new ArgumentOutOfRangeException( nameof( index ), "The static map body is read-only." );
		return ref _bodies[ index ];
	}

	internal ReadOnlySpan<DynamicBody> Bodies => _bodies.AsSpan( 0, DynamicCount );
	internal ref readonly DynamicBody StaticMapBody => ref _bodies[ StaticMapBodyIndex ];
	internal int ContactCount { get; private set; }
	internal BoardTargetDrive TargetDrive { get; } = new();
	internal BoardHeldConstraints HeldConstraints { get; } = new();
	internal ref readonly DynamicBody TargetBody => ref _bodies[ TargetBodyIndex ];
	internal ref readonly SolverBody Response( int index ) => ref _workspace[ index ];
	internal BoardBody Deck => new( _bodies[ 0 ].Position, _bodies[ 0 ].Rotation,
		_bodies[ 0 ].Velocity, _bodies[ 0 ].AngularVelocity, Vector3.One / _integration[ 0 ].LocalInverseInertia );
	internal ReadOnlySpan<ContactConstraint> Contacts( int count ) => _contacts.AsSpan( 0, count );

	internal void SetTargetFrame( Vector3 position, Quaternion rotation )
	{
		_bodies[ TargetBodyIndex ] = new DynamicBody { Position = position, Rotation = rotation };
		_targetConfigured = true;
	}

	internal void SetTargetBody( PhysicalBodyState body )
	{
		_bodies[ TargetBodyIndex ] = PhysicalSolverBody.Read( body );
		_targetConfigured = true;
	}

	// Hand poses, velocities and inertia use the same world axes and metres as the board.
	// Their owner consumes HandResponse and integrates them with the character's other constraints.
	internal void SetHandBody( int hand, in DynamicBody body )
	{
		var index = HandBodyIndex( hand );
		_bodies[ index ] = body;
		_handConfigured[ hand ] = true;
	}

	internal void SetPhysicalHandBody( int hand, PhysicalBodyState body )
		=> SetHandBody( hand, PhysicalSolverBody.Read( body ) );

	internal ref readonly DynamicBody HandBody( int hand ) => ref _bodies[ HandBodyIndex( hand ) ];
	internal ref readonly SolverBody HandResponse( int hand ) => ref _workspace[ HandBodyIndex( hand ) ];

	private static int HandBodyIndex( int hand )
	{
		if ( (uint)hand >= 2 )
			throw new ArgumentOutOfRangeException( nameof( hand ) );
		return FirstHandBodyIndex + hand;
	}

	internal float KineticEnergy()
	{
		var energy = 0f;
		for ( var i = 0; i < DynamicCount; i++ )
		{
			var body = _bodies[ i ];
			var angular = Vector3.Transform( body.AngularVelocity, Quaternion.Conjugate( body.Rotation ) );
			energy += 0.5f * (body.Velocity.LengthSquared() / body.InverseMass
				+ Vector3.Dot( angular, angular / _integration[ i ].LocalInverseInertia ));
		}
		return energy;
	}

	internal float MaximumAnchorError()
	{
		var maximum = 0f;
		for ( var child = 1; child < DynamicCount; child++ )
		{
			var parent = child < 3 ? 0 : (child - 3) / 2 + 1;
			var anchor = _bodies[ parent ].Position + Vector3.Transform(
				_origins[ child ] - _origins[ parent ], _bodies[ parent ].Rotation );
			maximum = Math.Max( maximum, Vector3.Distance( anchor, _bodies[ child ].Position ) );
		}
		return maximum;
	}

	internal void SetRiderAttached( bool attached )
	{
		// Geometry and mass distribution are estimates.
		var wheelMass = _settings.BoardMass * 0.025f;
		var truckMass = _settings.BoardMass * 0.12f;
		var deckMass = (attached ? _settings.Mass : _settings.BoardMass) - wheelMass * 4f - truckMass * 2f;
		Configure( 0, deckMass, BoxInertia( deckMass, new Vector3( 0.8f, 0.22f, 0.025f ) ) );
		for ( var i = 1; i < 3; i++ )
			Configure( i, truckMass, BoxInertia( truckMass, new Vector3( 0.045f, _settings.Track, 0.025f ) ) );
		var transverse = wheelMass * (3f * _radius * _radius + 0.035f * 0.035f) / 12f;
		var inertia = new Vector3( transverse, 0.5f * wheelMass * _radius * _radius, transverse );
		for ( var i = 3; i < DynamicCount; i++ )
			Configure( i, wheelMass, inertia );
	}

	internal void Reset( Vector3 position, Quaternion rotation, Vector3 velocity, Vector3 angularVelocity )
	{
		rotation = Quaternion.Normalize( rotation );
		_mirrorFirst = false;
		ContactCount = 0;
		for ( var i = 0; i < DynamicCount; i++ )
		{
			ref var body = ref _bodies[ i ];
			var offset = Vector3.Transform( _origins[ i ], rotation );
			body.Position = position + offset;
			body.Rotation = rotation;
			body.Velocity = velocity + Vector3.Cross( angularVelocity, offset );
			body.AngularVelocity = angularVelocity;
			RefreshInertia( i );
		}
		BeginStep();
	}

	internal void BeginStep()
	{
		for ( var i = 0; i < DynamicCount; i++ )
		{
			_bodies[ i ].Acceleration = -Vector3.UnitZ * _settings.Gravity;
			_bodies[ i ].AngularAcceleration = Vector3.Zero;
		}
	}

	internal bool RecoverInitialOverlap( ReadOnlySpan<CollisionContact> contacts )
	{
		var correction = AssemblyRecovery.Translation( contacts );
		if ( correction.LengthSquared() < 0.0000000001f )
			return false;
		for ( var i = 0; i < DynamicCount; i++ )
			_bodies[ i ].Position += correction;
		return true;
	}

	internal void AddForce( int bodyIndex, Vector3 force, Vector3 point )
	{
		ref var body = ref _bodies[ bodyIndex ];
		body.Acceleration += force * body.InverseMass;
		body.AngularAcceleration += Vector3.TransformNormal(
			Vector3.Cross( point - body.Position, force ), body.WorldInverseInertia );
	}

	internal void AddTorque( int bodyIndex, Vector3 torque )
	{
		ref var body = ref _bodies[ bodyIndex ];
		body.AngularAcceleration += Vector3.TransformNormal( torque, body.WorldInverseInertia );
	}

	internal void AddImpulse( int bodyIndex, Vector3 impulse, Vector3 angularImpulse )
	{
		ref var body = ref _bodies[ bodyIndex ];
		body.Velocity += impulse * body.InverseMass;
		body.AngularVelocity += Vector3.TransformNormal( angularImpulse, body.WorldInverseInertia );
	}

	internal void Step( ReadOnlySpan<CollisionContact> contacts, float steeringAngle, float brake, float step )
	{
		if ( contacts.Length > ContactCapacity )
			throw new ArgumentException( "Too many board contacts." );
		PrepareWorkspace();
		CompileGenericContacts( contacts, 0, step );
		CompleteStep( contacts.Length, steeringAngle, brake, step, 0 );
	}

	private void PrepareWorkspace()
	{
		Array.Clear( _workspace );
		for ( var i = 0; i < _bodies.Length; i++ )
			_workspace[ i ].InverseMass = _bodies[ i ].InverseMass;
	}

	private void CompileGenericContacts( ReadOnlySpan<CollisionContact> contacts, int destination, float step )
	{
		for ( var i = 0; i < contacts.Length; i++ )
		{
			ref readonly var contact = ref contacts[ i ];
			_contacts[ destination + i ] = ContactCompiler.Compile( _bodies[ contact.BodyA ], _bodies[ contact.BodyB ], contact, step );
		}
	}

	private void CompleteStep( int count, float steeringAngle, float brake, float step, int generatedCount )
	{
		ContactCount = count;
		CompileConnections( steeringAngle, brake, step );
		// Estimated convergence setting for the 80 kg rider / 75 g wheel mass ratio.
		// 25 passes fail loaded settling; 128 still accumulate lateral drift in a
		// straight coast.
		SolveSymmetric( count, 256, generatedCount );
		for ( var i = 0; i < DynamicCount; i++ )
			BodyDynamics.Step( ref _bodies[ i ], ref _workspace[ i ], _integration[ i ], step,
				-Vector3.UnitZ * _settings.Gravity );
	}

	// Sequential passes favour whichever wheel hinge is solved first. When a crease jolts the
	// 75 g wheels against the 80 kg deck the solve is not fully converged, and a fixed
	// left-before-right order turned that residual into about one degree of yaw per
	// quarterpipe. Swapping each axle's hinges between passes, and which side leads on
	// alternate substeps, cancels the bias while every pass keeps the
	// contact -> joint -> drive order.
	private void SolveSymmetric( int contactCount, int passes, int generatedCount )
	{
		var contacts = _contacts.AsSpan( 0, contactCount );
		var drives = _drives.AsSpan( 0, _driveCount );
		ConstraintSolver.Validate( _workspace.Length, contacts, _joints, drives );
		_mirrorFirst = !_mirrorFirst;
		if ( _mirrorFirst )
			SwapWheelHinges();
		for ( var pass = 0; pass < passes; pass++ )
		{
			ConstraintSolver.SolveGeneratedPass( _workspace, contacts, _joints, drives, generatedCount );
			SwapWheelHinges();
		}
		if ( (passes + (_mirrorFirst ? 1 : 0)) % 2 != 0 )
			SwapWheelHinges();
	}

	private void SwapWheelHinges()
	{
		(_joints[ 2 ], _joints[ 3 ]) = (_joints[ 3 ], _joints[ 2 ]);
		(_joints[ 4 ], _joints[ 5 ]) = (_joints[ 5 ], _joints[ 4 ]);
	}

	private void CompileConnections( float steeringAngle, float brake, float step )
	{
		_driveCount = 2;
		var freeSteering = new QuaternionJointGeometry( _steeringFrame, _steeringFrame, false, 1000f );
		var freeWheel = new QuaternionJointGeometry( _axleFrame, _axleFrame, false,
			1000f * (1f - Math.Clamp( brake, 0f, 1f )) );
		var noLinearDrive = new DriveSettings( DriveMode.Spring, 0f, 0f, 0f );
		// Estimated loaded-truck response: the former 1200/60/300 drive saturated
		// under wheel friction and allowed the truck to turn over a radian off target.
		// These are estimated acceleration-normalized spring/damping/limit values.
		var steeringDrive = new DriveSettings( DriveMode.Spring, 30000f, 350f, 20000f );
		for ( var truck = 1; truck <= 2; truck++ )
		{
			var joint = new JointGeometry( 0, truck, _origins[ truck ], Vector3.Zero,
				Quaternion.Identity, Vector3.Zero, Vector3.Zero );
			_joints[ truck - 1 ] = QuaternionJointCompiler.Compile( _bodies[ 0 ], _bodies[ truck ], joint, freeSteering, step );
			var target = Quaternion.CreateFromAxisAngle( Vector3.UnitZ, truck == 1 ? steeringAngle : -steeringAngle );
			var drive = new DriveGeometry( 0, truck, _origins[ truck ], Vector3.Zero, target, Quaternion.Identity );
			_drives[ truck - 1 ] = DriveCompiler.Compile( _bodies[ 0 ], _bodies[ truck ], drive, noLinearDrive, steeringDrive, step );
		}
		for ( var wheel = 0; wheel < 4; wheel++ )
		{
			var truck = wheel / 2 + 1;
			var body = wheel + 3;
			var joint = new JointGeometry( truck, body, _origins[ body ] - _origins[ truck ], Vector3.Zero,
				Quaternion.Identity, Vector3.Zero, Vector3.Zero );
			_joints[ wheel + 2 ] = QuaternionJointCompiler.Compile( _bodies[ truck ], _bodies[ body ], joint, freeWheel, step );
		}
		if ( _targetConfigured && (TargetDrive.Linear.MaximumAcceleration > 0f || TargetDrive.Angular.MaximumAcceleration > 0f) )
		{
			var geometry = new DriveGeometry( TargetBodyIndex, 0, Vector3.Zero, Vector3.Zero,
				Quaternion.Identity, Quaternion.Identity );
			_drives[ _driveCount++ ] = DriveCompiler.Compile( _bodies[ TargetBodyIndex ], _bodies[ 0 ],
				geometry, TargetDrive.Linear, TargetDrive.Angular, step );
		}
		for ( var hand = 0; hand < 2; ++hand )
		{
			var settings = HeldConstraints.Drive( hand );
			if ( settings.MaximumAcceleration <= 0f )
				continue;
			if ( !_handConfigured[ hand ] )
				throw new InvalidOperationException( "An active hand drive requires its physical hand body." );
			var body = HandBodyIndex( hand );
			var geometry = HeldConstraints.Geometry( hand, body, 0 );
			_drives[ _driveCount++ ] = DriveCompiler.Compile( _bodies[ body ], _bodies[ 0 ],
				geometry, settings, settings, step );
		}
	}

	private void Configure( int index, float mass, Vector3 inertia )
	{
		_bodies[ index ].InverseMass = 1f / mass;
		_integration[ index ] = new BodyDynamicsSettings( Vector3.One / inertia, 0f, 0f, 100f, 2000f, 1f, 0f, 0 );
		RefreshInertia( index );
	}

	private void RefreshInertia( int index )
	{
		var basis = Matrix4x4.CreateFromQuaternion( _bodies[ index ].Rotation );
		_bodies[ index ].WorldInverseInertia = Matrix4x4.Transpose( basis )
			* Matrix4x4.CreateScale( _integration[ index ].LocalInverseInertia ) * basis;
	}

	private static Vector3 BoxInertia( float mass, Vector3 size ) => mass / 12f * new Vector3(
		size.Y * size.Y + size.Z * size.Z,
		size.X * size.X + size.Z * size.Z,
		size.X * size.X + size.Y * size.Y );
}
