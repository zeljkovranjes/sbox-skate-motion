namespace SkateMotion.Core.Animation;

/// <summary>Supplies a base tree definition, initial clock settings and retained wrapper behavior.</summary>
internal readonly record struct AnimationBasePoseRequest(
	int Definition,
	AnimationBaseTransition Transition,
	float Rate = 1f,
	float Duration = 0.2f,
	AnimationTransitionMode Synchronization = AnimationTransitionMode.Both,
	bool Under = false,
	float StartTime = 0f,
	bool UseChannelFromWeights = false );
