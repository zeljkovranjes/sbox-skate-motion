using SkateMotion.Core.Animation;
using SkateMotion.Core.Constraints;

namespace SkateMotion.Core.Physics;

using Vector3 = System.Numerics.Vector3;

/// <summary>Solves connected physical bodies in one workspace before updating their retained poses.</summary>
internal sealed partial class PhysicalSolverWorld
{
	private readonly PhysicalBodyState[] _physical;
	private readonly DynamicBody[] _bodies;
	private readonly SolverBody[] _workspace;
	private readonly BodyDynamicsSettings[] _settings;
	private readonly int[] _integrationOrder;
	private bool _prepared;

	// The world owner supplies its selected integration list and properties. Constraint response
	// flags alone do not describe activation, sleeping or the world's update scheduling.
	internal PhysicalSolverWorld( PhysicalBodyState[] bodies, BodyDynamicsSettings[] settings,
		int[] integrationOrder )
		: this( bodies, settings, integrationOrder, 0 )
	{
	}

	internal PhysicalSolverWorld( PhysicalBodyState[] bodies, BodyDynamicsSettings[] settings,
		int[] integrationOrder, int poseAttachmentCapacity )
		: this( bodies, settings, integrationOrder, poseAttachmentCapacity, 0 )
	{
	}

	internal PhysicalSolverWorld( PhysicalBodyState[] bodies, BodyDynamicsSettings[] settings,
		int[] integrationOrder, int poseAttachmentCapacity, int structuralJointCapacity )
	{
		ArgumentNullException.ThrowIfNull( bodies );
		ArgumentNullException.ThrowIfNull( settings );
		ArgumentNullException.ThrowIfNull( integrationOrder );
		ArgumentOutOfRangeException.ThrowIfNegative( poseAttachmentCapacity );
		ArgumentOutOfRangeException.ThrowIfNegative( structuralJointCapacity );
		if ( bodies.Length != settings.Length )
			throw new ArgumentException( "Each physical body requires an integration property slot." );
		for ( var i = 0; i < bodies.Length; ++i )
		{
			ArgumentNullException.ThrowIfNull( bodies[ i ] );
			for ( var j = 0; j < i; ++j )
				if ( ReferenceEquals( bodies[ i ], bodies[ j ] ) )
					throw new ArgumentException( "A shared physical body must use one solver index." );
		}
		for ( var i = 0; i < integrationOrder.Length; ++i )
		{
			if ( (uint)integrationOrder[ i ] >= (uint)bodies.Length )
				throw new ArgumentOutOfRangeException( nameof( integrationOrder ) );
			for ( var j = 0; j < i; ++j )
				if ( integrationOrder[ i ] == integrationOrder[ j ] )
					throw new ArgumentException( "A physical body must be integrated only once." );
		}
		_physical = bodies.AsSpan().ToArray();
		_settings = settings.AsSpan().ToArray();
		_integrationOrder = integrationOrder.AsSpan().ToArray();
		_bodies = new DynamicBody[ bodies.Length ];
		_workspace = new SolverBody[ bodies.Length ];
		_poseAttachments = new PoseAttachment[ poseAttachmentCapacity ];
		_structuralJoints = new StructuralJointMembership[ structuralJointCapacity ];
	}

	internal int BodyCount => _bodies.Length;
	internal ref readonly DynamicBody Body( int index ) => ref _bodies[ index ];

	internal int IndexOfBody( PhysicalBodyState body )
	{
		for ( var i = 0; i < _physical.Length; ++i )
			if ( ReferenceEquals( _physical[ i ], body ) )
				return i;
		return -1;
	}

	internal DriveConstraint CompileDrive( in DriveGeometry geometry, in DriveSettings linear,
		in DriveSettings angular, float step )
	{
		if ( !_prepared )
			throw new InvalidOperationException( "Read current physical bodies before compiling their constraints." );
		return DriveCompiler.Compile( _bodies[ geometry.BodyA ], _bodies[ geometry.BodyB ],
			geometry, linear, angular, step );
	}

	internal void Prepare()
	{
		for ( var i = 0; i < _bodies.Length; ++i )
		{
			_bodies[ i ] = PhysicalSolverBody.Read( _physical[ i ] );
			_workspace[ i ] = new SolverBody { InverseMass = _bodies[ i ].InverseMass };
		}
		_prepared = true;
	}

	internal void SolveAndIntegrate( Span<ContactConstraint> contacts, Span<JointConstraint> joints,
		Span<DriveConstraint> drives, int passes, float step, Vector3 gravity )
	{
		SolveAndIntegrateCore( contacts, joints, drives, passes, step, gravity, false );
	}

	private void SolveAndIntegrateCore( Span<ContactConstraint> contacts, Span<JointConstraint> joints,
		Span<DriveConstraint> drives, int passes, float step, Vector3 gravity, bool generated )
	{
		if ( !_prepared )
			throw new InvalidOperationException( "Read current physical bodies before compiling their constraints." );
		if ( !float.IsFinite( step ) || step <= 0f )
			throw new ArgumentOutOfRangeException( nameof( step ) );
		if ( generated )
			ConstraintSolver.SolveGenerated( _workspace, contacts, joints, drives, passes );
		else
			ConstraintSolver.Solve( _workspace, contacts, joints, drives, passes );
		_prepared = false;
		foreach ( var index in _integrationOrder )
		{
			ref var body = ref _bodies[ index ];
			// Integration reads the retained mass, rather than the constraint response mask.
			body.InverseMass = _physical[ index ].InverseMass;
			BodyDynamics.Step( ref body, ref _workspace[ index ], _settings[ index ], step, gravity );
			PhysicalSolverBody.WriteDynamics( _physical[ index ], body );
			body = PhysicalSolverBody.Read( _physical[ index ] );
		}
	}
}
