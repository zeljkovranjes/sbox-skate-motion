namespace SkateMotion.Core.Animation;

/// <summary>Connects stream dispatch to snapshot storage through an explicit pose-value resolver.</summary>
internal sealed class AnimationPoseCommandBufferOperations : IAnimationPoseCommandOperations
{
	private readonly AnimationPoseStack _stack;
	private readonly Func<int, SkeletonPose> _sampleValue;

	internal AnimationPoseCommandBufferOperations( AnimationPoseStack stack, Func<int, SkeletonPose> sampleValue )
	{
		ArgumentNullException.ThrowIfNull( stack );
		ArgumentNullException.ThrowIfNull( sampleValue );
		_stack = stack;
		_sampleValue = sampleValue;
	}

	internal SkeletonPose Pose => _stack.Pose;
	internal int PreparedCount => _stack.Count;
	internal void Begin() => _stack.Clear();

	/// <summary>Resolves and snapshots the selected pose before another value can alter it.</summary>
	public void Sample( int value ) => _stack.Push( _sampleValue( value ) );

	/// <summary>Reduces the most recent pair using its queued interpolation weight.</summary>
	public void Blend( float weight ) => _stack.Blend( weight );

	/// <summary>Reduces the most recent pose group using ordered weighted accumulation.</summary>
	public void BlendMany( ReadOnlySpan<float> weights ) => _stack.BlendMany( weights );
}
