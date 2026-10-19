using SkateMotion.Core.Animation;

namespace SkateMotion.Core.Physics;

using Vector3 = System.Numerics.Vector3;
using Vector4 = System.Numerics.Vector4;

/// <summary>Owns the root, feedback and mapped joint drives connecting one physical rig to its world.</summary>
internal sealed class PhysicalRigSolverBinding
{
	private static readonly PhysicalJointDrive RootSettings = new( 599.9999389648438f, 0f, 35999.99609375f, 2 );
	private readonly PhysicalSolverWorld _world;
	private readonly PhysicalRigState _rig;
	private readonly PhysicalJointState[] _roots = new PhysicalJointState[ 4 ];
	private readonly PhysicalJointState[] _joints = new PhysicalJointState[ 22 ];
	private bool _released;

	internal PhysicalRigSolverBinding( PhysicalSolverWorld world, PhysicalRigState rig,
		PhysicalJointState[] canonicalJoints, LimbRigidTransform[] bindPoses, int[] firstBodyBones )
	{
		ArgumentNullException.ThrowIfNull( world );
		ArgumentNullException.ThrowIfNull( rig );
		ArgumentNullException.ThrowIfNull( canonicalJoints );
		ArgumentNullException.ThrowIfNull( bindPoses );
		ArgumentNullException.ThrowIfNull( firstBodyBones );
		if ( canonicalJoints.Length != 24 || bindPoses.Length != 24 || firstBodyBones.Length != 24
			|| rig.Bodies.Length != 26 || rig.Targets.Length != 4 || rig.Joints.Length != 22 )
			throw new ArgumentException( "Binding requires a complete canonical rig, four targets and two feedback bodies." );
		var bodyIndices = ResolveBodies( world, rig.Bodies );
		var targetIndices = ResolveBodies( world, rig.Targets );
		foreach ( var target in targetIndices )
			foreach ( var body in bodyIndices )
				if ( target == body )
					throw new ArgumentException( "Rig targets must use their own physical bodies." );
		var required = 4;
		for ( var bone = 0; bone < 24; ++bone )
		{
			if ( canonicalJoints[ bone ] is not null )
				for ( var previous = 0; previous < bone; ++previous )
					if ( ReferenceEquals( canonicalJoints[ bone ], canonicalJoints[ previous ] ) )
						throw new ArgumentException( "Canonical joints require distinct retained state." );
			if ( firstBodyBones[ bone ] < -1 || firstBodyBones[ bone ] >= 24 )
				throw new ArgumentOutOfRangeException( nameof( firstBodyBones ) );
			if ( firstBodyBones[ bone ] >= 0 )
				ArgumentNullException.ThrowIfNull( canonicalJoints[ bone ] );
			if ( bone is 0 or 23 )
				continue;
			if ( !ReferenceEquals( canonicalJoints[ bone ], rig.Joints[ bone - 1 ] ) )
				throw new ArgumentException( "The rig output must retain the same canonical joint objects as its solver binding." );
			if ( firstBodyBones[ bone ] < 0 )
				continue;
			if ( world.HasPoseAttachment( canonicalJoints[ bone ], false ) || world.HasPoseAttachment( canonicalJoints[ bone ], true ) )
				throw new ArgumentException( "A registered rig joint already has a world owner." );
			_joints[ bone - 1 ] = canonicalJoints[ bone ];
			required += 2;
		}
		if ( world.AvailablePoseAttachments < required )
			throw new ArgumentException( "The world has insufficient reserved capacity for this rig's drives." );

		_world = world;
		_rig = rig;
		PhysicalJointInitialization.Initialize( canonicalJoints, bindPoses, firstBodyBones, rig.Presets );
		rig.RootDrive = RootSettings;
		for ( var i = 0; i < 4; ++i )
		{
			var settings = i == 1 ? RootSettings with { VelocityGain = 59.999996185302734f } : RootSettings;
			// Root and feedback drives share the same retained frame/settings storage as
			// a single joint attachment. Their canonical bone metadata is not consulted.
			_roots[ i ] = new PhysicalJointState
			{
				FirstFrame = new( i >= 2 ? new Vector4( .5f ) : Vector4.UnitW, Vector3.Zero, Vector4.UnitW, Vector3.Zero ),
				FirstConstraint = i >= 2 ? new( 0f, 0f, 0f, 2 ) : settings,
				SecondConstraint = settings
			};
			var bone = i == 0 ? 23 : i == 1 ? 0 : 22 + i;
			world.RegisterPoseAttachment( _roots[ i ], false, bodyIndices[ bone ], targetIndices[ i ] );
		}
		for ( var bone = 1; bone < 23; ++bone )
		{
			if ( _joints[ bone - 1 ] is not { } joint )
				continue;
			world.RegisterPoseAttachment( joint, false, bodyIndices[ bone ], bodyIndices[ firstBodyBones[ bone ] ] );
			world.RegisterPoseAttachment( joint, true, bodyIndices[ bone ], bodyIndices[ 23 ] );
		}
	}

	internal void RefreshRootDrive()
	{
		if ( _released )
			throw new InvalidOperationException( "A released rig no longer owns solver drives." );
		// Pose output updates the primary linear drive; the angular channel remains retained.
		_roots[ 0 ].FirstConstraint = _rig.RootDrive;
	}

	internal void Release()
	{
		if ( _released )
			return;
		foreach ( var root in _roots )
			_world.ReleasePoseAttachment( root, false );
		foreach ( var joint in _joints )
		{
			if ( joint is null )
				continue;
			_world.ReleasePoseAttachment( joint, false );
			_world.ReleasePoseAttachment( joint, true );
		}
		_released = true;
	}

	private static int[] ResolveBodies( PhysicalSolverWorld world, PhysicalBodyBinding[] bindings )
	{
		var indices = new int[ bindings.Length ];
		for ( var i = 0; i < bindings.Length; ++i )
		{
			indices[ i ] = world.IndexOfBody( bindings[ i ]?.Body );
			if ( indices[ i ] < 0 )
				throw new ArgumentException( "Every rig body must already belong to the shared solver world." );
			for ( var previous = 0; previous < i; ++previous )
				if ( indices[ i ] == indices[ previous ] )
					throw new ArgumentException( "Each canonical binding must have its own physical body." );
		}
		return indices;
	}
}
