using SkateMotion.Core.Constraints;

namespace SkateMotion.Core.Physics;

internal sealed partial class PhysicalSolverWorld
{
	private readonly StructuralJointMembership[] _structuralJoints;
	private int _structuralJointCount;

	internal int StructuralJointCount => _structuralJointCount;
	internal int AvailableStructuralJoints => _structuralJoints.Length - _structuralJointCount;

	internal bool TryRegisterStructuralJoint( PhysicalStructuralJointRegistration joint, int childBody, int parentBody )
	{
		ArgumentNullException.ThrowIfNull( joint );
		if ( (uint)childBody >= (uint)BodyCount || (uint)parentBody >= (uint)BodyCount )
			throw new ArgumentOutOfRangeException( nameof( childBody ) );
		if ( joint.Owner is not null )
			throw new ArgumentException( "A retained joint already has a world owner.", nameof( joint ) );
		if ( _structuralJointCount == _structuralJoints.Length )
			return false;
		_structuralJoints[ _structuralJointCount++ ] = new( joint, childBody, parentBody );
		joint.Owner = this;
		return true;
	}

	internal void ReleaseStructuralJoint( PhysicalStructuralJointRegistration joint )
	{
		ArgumentNullException.ThrowIfNull( joint );
		if ( !ReferenceEquals( joint.Owner, this ) )
			return;
		for ( var index = 0; index < _structuralJointCount; ++index )
		{
			if ( !ReferenceEquals( _structuralJoints[ index ].Registration, joint ) )
				continue;
			--_structuralJointCount;
			for ( var next = index; next < _structuralJointCount; ++next )
				_structuralJoints[ next ] = _structuralJoints[ next + 1 ];
			_structuralJoints[ _structuralJointCount ] = default;
			joint.Owner = null;
			return;
		}
	}

	internal int CompileStructuralJoints( Span<JointConstraint> destination, float step )
	{
		if ( !_prepared )
			throw new InvalidOperationException( "Read current physical bodies before compiling their constraints." );
		if ( !float.IsFinite( step ) || step <= 0f )
			throw new ArgumentOutOfRangeException( nameof( step ) );
		var count = 0;
		for ( var i = 0; i < _structuralJointCount; ++i )
			if ( StructuralResponds( _structuralJoints[ i ] ) )
				++count;
		if ( destination.Length < count )
			throw new ArgumentException( "The destination cannot hold all responding structural joints.", nameof( destination ) );
		count = 0;
		for ( var i = 0; i < _structuralJointCount; ++i )
		{
			var entry = _structuralJoints[ i ];
			if ( StructuralResponds( entry ) )
				destination[ count++ ] = CompileStructuralJoint( entry.Registration.Joint, entry.ChildBody, entry.ParentBody, step );
		}
		return count;
	}

	private bool StructuralResponds( in StructuralJointMembership entry ) =>
		((_physical[ entry.ChildBody ].MotionMode | _physical[ entry.ParentBody ].MotionMode) & 4) != 0;

	private readonly record struct StructuralJointMembership(
		PhysicalStructuralJointRegistration Registration, int ChildBody, int ParentBody );
}
