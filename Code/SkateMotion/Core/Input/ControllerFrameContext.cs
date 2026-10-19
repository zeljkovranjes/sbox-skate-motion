namespace SkateMotion.Core.Input;

internal readonly record struct ControllerFrameContext
{
	internal bool HasOverride { get; init; }
	internal bool ForceBrake { get; init; }
	internal bool BlockControllerDuringForcedBrake { get; init; }
	internal bool SuppressForcedBail { get; init; }
}
