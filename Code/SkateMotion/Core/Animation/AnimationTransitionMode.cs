namespace SkateMotion.Core.Animation;

/// <summary>Selects how the successor's clock follows the previous playback during a transition.</summary>
internal enum AnimationTransitionMode
{
	Both,
	NextOnly,
	MatchPhase,
	MatchTime
}
