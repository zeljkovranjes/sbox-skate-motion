namespace SkateMotion.Core.Animation;

/// <summary>Result of keyed overlay creation or replacement.</summary>
internal enum AnimationOverlayRequestResult
{
	Success = 0,
	FadingOut = 1,
	MissingChannel = 2,
	Capacity = 3,
	MissingTree = 4
}
