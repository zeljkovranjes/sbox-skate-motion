namespace SkateMotion.Core.Animation;

/// <summary>Result of replacing an overlay on an existing channel.</summary>
internal enum AnimationChannelReplaceResult
{
	Success = 0,
	FadingOut = 1,
	MissingTree = 4
}
