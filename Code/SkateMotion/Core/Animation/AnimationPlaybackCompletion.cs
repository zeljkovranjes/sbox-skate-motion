namespace SkateMotion.Core.Animation;

/// <summary>Child update output, including unused time that a queued successor can consume.</summary>
internal readonly record struct AnimationPlaybackCompletion(
	byte Flags, bool Finished, float UnusedDelta, float Remaining )
{
	internal static AnimationPlaybackCompletion Empty => new( 0, false, -1f, -1f );
}
