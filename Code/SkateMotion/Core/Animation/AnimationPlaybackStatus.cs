namespace SkateMotion.Core.Animation;

/// <summary>Animation-owned expiry and transition state for graph conditions.</summary>
internal readonly record struct AnimationPlaybackStatus( float Remaining, bool Transitioning, bool Finished = false )
{
	internal bool ExpiresWithin( float seconds, bool waitForTransition ) =>
		(!waitForTransition || !Transitioning) && (Finished || !(seconds < Remaining));
}
