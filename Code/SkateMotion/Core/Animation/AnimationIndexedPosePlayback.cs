namespace SkateMotion.Core.Animation;

/// <summary>Resolves indexed local streams, then composes their reference or uses it as the missing-stream fallback.</summary>
internal sealed class AnimationIndexedPosePlayback
{
	private readonly SkeletonPose _reference;
	private readonly AnimationPoseStack _stack;
	private readonly AnimationPoseCommandBufferOperations _operations;
	private readonly AnimationPoseCommandExecutor _executor;

	internal AnimationIndexedPosePlayback( AnimationIndexedPoseBank bank,
		AnimationBoneTransform[] reference, int capacity )
	{
		ArgumentNullException.ThrowIfNull( bank );
		ArgumentNullException.ThrowIfNull( reference );
		if ( reference.Length != bank.BoneCount )
			throw new ArgumentException( "Indexed poses and reference need matching bone counts.", nameof( reference ) );
		if ( capacity < 1 )
			throw new ArgumentOutOfRangeException( nameof( capacity ) );
		_reference = new SkeletonPose( bank.BoneCount );
		_reference.SetTransforms( reference );
		Pose = new SkeletonPose( bank.BoneCount );
		// Reference composition needs one additional operand after stream evaluation.
		_stack = new AnimationPoseStack( bank.BoneCount, checked( capacity + 1 ) );
		_operations = new AnimationPoseCommandBufferOperations( _stack, bank.Sample );
		_executor = new AnimationPoseCommandExecutor( _operations );
	}

	internal SkeletonPose Pose { get; }

	/// <summary>Updates the reusable result and reports whether a stream supplied any indexed poses.</summary>
	internal bool Prepare( AnimationPoseStreamContext context, AnimationPoseStreamKey name, int sampleLimit )
	{
		ArgumentNullException.ThrowIfNull( context );
		_operations.Begin();
		var sampled = _executor.TryExecute( context, name, sampleLimit, out var result ) && result.Values > 0;
		if ( !sampled )
		{
			Pose.CopyFrom( _reference );
			return false;
		}
		_stack.Push( _reference );
		_stack.Compose( true, true );
		Pose.CopyFrom( _stack.Top );
		return true;
	}
}
