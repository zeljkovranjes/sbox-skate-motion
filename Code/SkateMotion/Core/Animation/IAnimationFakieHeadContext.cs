using SkateMotion.Core.Input;

namespace SkateMotion.Core.Animation;

/// <summary>Actor inputs and owner dispatch used by the retained fakie head behavior.</summary>
internal interface IAnimationFakieHeadContext
{
	bool IsPrimaryInputActive();
	bool IsSecondaryInputActive();
	bool IsRidingFakie();
	/// <summary>Starts the fixed head request: blend .1, rate 1, recovery and exit .3.</summary>
	void StartChannel( InputCommandKey channel, InputCommandKey motion );
	void StopChannel( InputCommandKey channel );
	void SetParameter( AnimationAttributeKey name, float value, bool normalized, int owner );
}
