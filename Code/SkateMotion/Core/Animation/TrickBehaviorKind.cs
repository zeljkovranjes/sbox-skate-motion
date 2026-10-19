namespace SkateMotion.Core.Animation;

/// <summary>Identifies the supported callback family; other families require a caller binding.</summary>
internal enum TrickBehaviorKind
{
	External,
	Continuation,
	Height
}
