using SkateMotion.Core.Animation;
using SkateMotion.Core.Constraints;

namespace SkateMotion.Core.Physics;

internal sealed partial class PhysicalSolverWorld
{
	private readonly PoseAttachment[] _poseAttachments;
	private int _poseAttachmentCount;

	internal int PoseAttachmentCount => _poseAttachmentCount;
	internal int AvailablePoseAttachments => _poseAttachments.Length - _poseAttachmentCount;

	internal bool HasPoseAttachment( PhysicalJointState joint, bool secondAttachment ) =>
		FindPoseAttachment( joint, secondAttachment ) >= 0;

	internal void RegisterPoseAttachment( PhysicalJointState joint, bool secondAttachment,
		int childBody, int attachedBody )
	{
		ArgumentNullException.ThrowIfNull( joint );
		if ( (uint)childBody >= (uint)BodyCount )
			throw new ArgumentOutOfRangeException( nameof( childBody ) );
		if ( (uint)attachedBody >= (uint)BodyCount )
			throw new ArgumentOutOfRangeException( nameof( attachedBody ) );
		var existing = FindPoseAttachment( joint, secondAttachment );
		if ( existing < 0 && _poseAttachmentCount == _poseAttachments.Length )
			throw new InvalidOperationException( "The world's reserved pose attachment capacity is full." );
		if ( existing >= 0 )
			ReleasePoseAttachment( joint, secondAttachment );

		// Replacement removes the old membership and appends the new one. The iterative
		// solver observes this order; reusing the previous array slot changes its result.
		_poseAttachments[ _poseAttachmentCount++ ] = new( joint, secondAttachment, childBody, attachedBody );
	}

	internal void ReleasePoseAttachment( PhysicalJointState joint, bool secondAttachment )
	{
		ArgumentNullException.ThrowIfNull( joint );
		var index = FindPoseAttachment( joint, secondAttachment );
		if ( index >= 0 )
		{
			--_poseAttachmentCount;
			for ( var i = index; i < _poseAttachmentCount; ++i )
				_poseAttachments[ i ] = _poseAttachments[ i + 1 ];
			_poseAttachments[ _poseAttachmentCount ] = default;
		}
		if ( secondAttachment )
			joint.SecondEnabled = false;
		else
			joint.FirstEnabled = false;
	}

	internal int CompilePoseAttachments( Span<DriveConstraint> destination, float step )
	{
		if ( !_prepared )
			throw new InvalidOperationException( "Read current physical bodies before compiling their constraints." );
		if ( !float.IsFinite( step ) || step <= 0f )
			throw new ArgumentOutOfRangeException( nameof( step ) );
		var count = 0;
		for ( var i = 0; i < _poseAttachmentCount; ++i )
			if ( Responds( _poseAttachments[ i ] ) )
				++count;
		if ( destination.Length < count )
			throw new ArgumentException( "The destination cannot hold all responding pose attachments.", nameof( destination ) );
		count = 0;
		for ( var i = 0; i < _poseAttachmentCount; ++i )
		{
			var attachment = _poseAttachments[ i ];
			if ( Responds( attachment ) )
				destination[ count++ ] = PhysicalPoseConstraints.Compile( this, attachment.Joint,
					attachment.Second, attachment.ChildBody, attachment.AttachedBody, step );
		}
		return count;
	}

	private bool Responds( in PoseAttachment attachment ) =>
		((_physical[ attachment.ChildBody ].MotionMode | _physical[ attachment.AttachedBody ].MotionMode) & 4) != 0;

	private int FindPoseAttachment( PhysicalJointState joint, bool secondAttachment )
	{
		for ( var i = 0; i < _poseAttachmentCount; ++i )
			if ( ReferenceEquals( _poseAttachments[ i ].Joint, joint ) && _poseAttachments[ i ].Second == secondAttachment )
				return i;
		return -1;
	}

	private readonly record struct PoseAttachment( PhysicalJointState Joint, bool Second, int ChildBody, int AttachedBody );
}
