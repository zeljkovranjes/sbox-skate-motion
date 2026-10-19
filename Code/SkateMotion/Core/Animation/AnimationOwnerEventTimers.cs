namespace SkateMotion.Core.Animation;

/// <summary>Retains independent event clocks shared by graph behaviors and conditions.</summary>
internal sealed class AnimationOwnerEventTimers
{
	internal float SinceKickturn { get; private set; }
	internal float SinceTeleport { get; private set; }

	internal AnimationOwnerEventTimers( float sinceKickturn, float sinceTeleport )
	{
		SinceKickturn = sinceKickturn;
		SinceTeleport = sinceTeleport;
	}

	internal void ResetKickturn() => SinceKickturn = 0f;

	internal void ResetTeleport() => SinceTeleport = 0f;

	internal void AdvanceKickturn( float graphDelta ) => SinceKickturn += graphDelta;

	internal void AdvanceTeleport( float graphDelta ) => SinceTeleport += graphDelta;
}
