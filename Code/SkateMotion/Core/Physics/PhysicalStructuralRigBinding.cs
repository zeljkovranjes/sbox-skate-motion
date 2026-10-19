using SkateMotion.Core.Animation;

namespace SkateMotion.Core.Physics;

/// <summary>Owns a canonical rig's structural joints in the shared solver world.</summary>
internal sealed class PhysicalStructuralRigBinding
{
	private readonly PhysicalSolverWorld _world;
	private readonly PhysicalStructuralJointRegistration[] _joints;
	private bool _released;

	internal PhysicalStructuralRigBinding( PhysicalSolverWorld world, PhysicalBodyBinding[] bodies,
		PhysicalStructuralJoint[] joints )
	{
		ArgumentNullException.ThrowIfNull( world );
		ArgumentNullException.ThrowIfNull( bodies );
		ArgumentNullException.ThrowIfNull( joints );
		if ( bodies.Length < 24 || joints.Length > 22 )
			throw new ArgumentException( "Structural binding requires 24 canonical bodies and at most 22 joints." );
		if ( world.AvailableStructuralJoints < joints.Length )
			throw new ArgumentException( "The world has insufficient reserved structural capacity." );
		var indices = new int[ 24 ];
		for ( var bone = 0; bone < indices.Length; ++bone )
		{
			indices[ bone ] = world.IndexOfBody( bodies[ bone ]?.Body );
			if ( indices[ bone ] < 0 )
				throw new ArgumentException( "Every canonical body must belong to the shared world." );
			for ( var previous = 0; previous < bone; ++previous )
				if ( indices[ bone ] == indices[ previous ] )
					throw new ArgumentException( "Canonical bodies must have distinct physical identities." );
		}
		foreach ( var joint in joints )
			if ( (uint)joint.ChildBone >= 24 || (uint)joint.ParentBone >= 24 )
				throw new ArgumentException( "Structural endpoints must refer to canonical bodies." );

		_world = world;
		_joints = new PhysicalStructuralJointRegistration[ joints.Length ];
		// Validate the complete set before changing world membership.
		for ( var i = 0; i < joints.Length; ++i )
		{
			_joints[ i ] = new( joints[ i ] );
			world.TryRegisterStructuralJoint( _joints[ i ], indices[ joints[ i ].ChildBone ], indices[ joints[ i ].ParentBone ] );
		}
	}

	internal void UpdateJoint( int index, in PhysicalStructuralJoint joint )
	{
		if ( _released )
			throw new InvalidOperationException( "A released rig no longer owns structural joints." );
		if ( (uint)index >= (uint)_joints.Length )
			throw new ArgumentOutOfRangeException( nameof( index ) );
		var previous = _joints[ index ].Joint;
		if ( previous.ChildBone != joint.ChildBone || previous.ParentBone != joint.ParentBone )
			throw new ArgumentException( "Changing structural endpoints requires a new binding." );
		_joints[ index ].Joint = joint;
	}

	internal void Release()
	{
		if ( _released )
			return;
		foreach ( var joint in _joints )
			_world.ReleaseStructuralJoint( joint );
		_released = true;
	}
}
