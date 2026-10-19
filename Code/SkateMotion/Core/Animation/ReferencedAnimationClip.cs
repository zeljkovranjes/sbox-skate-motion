namespace SkateMotion.Core.Animation;

/// <summary>Samples raw keys and composes their selected reference pose once.</summary>
internal sealed class ReferencedAnimationClip
{
	private readonly AnimationBoneTransform[] _reference;

	internal ReferencedAnimationClip( RawAnimationClip clip, AnimationBoneTransform[] reference )
	{
		ArgumentNullException.ThrowIfNull( clip );
		ArgumentNullException.ThrowIfNull( reference );
		if ( reference.Length != clip.BoneCount )
			throw new ArgumentException( "Reference and clip need matching bone counts.", nameof( reference ) );
		Clip = clip;
		_reference = reference;
	}

	internal RawAnimationClip Clip { get; }

	internal void Sample( float time, bool interpolate, Span<AnimationBoneTransform> output )
	{
		Clip.Sample( time, interpolate, output );
		var pose = output.Slice( 0, Clip.BoneCount );
		AnimationPoseComposer.Compose( pose, _reference, true, true, pose );
	}

	internal AnimationBoneTransform SampleBone( float time, int bone, bool interpolate )
	{
		var sampled = Clip.SampleBone( time, bone, interpolate );
		return AnimationPoseComposer.Compose( sampled, _reference[ bone ], true, true );
	}

	internal void SampleInterval( float previousTime, float currentTime, int wraps, bool interpolate,
		Span<AnimationBoneTransform> output )
	{
		Clip.SampleInterval( previousTime, currentTime, wraps, interpolate, output );
		var pose = output.Slice( 0, Clip.BoneCount );
		AnimationPoseComposer.Compose( pose, _reference, true, true, pose );
	}
}
