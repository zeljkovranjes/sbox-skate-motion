namespace SkateMotion.Core.Animation;

/// <summary>Selects the frame boundary and arithmetic rules encoded by a pose container.</summary>
internal enum AnimationClipSamplingMode : byte
{
	Block = 1,
	Continuous = 2
}
