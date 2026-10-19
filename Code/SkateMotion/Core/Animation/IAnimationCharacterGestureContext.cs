using SkateMotion.Core.Input;

namespace SkateMotion.Core.Animation;

internal interface IAnimationCharacterGestureContext
{
	bool ContainsRequest( InputCommandKey key );
	bool HasChannel( InputCommandKey channel );
	void StopChannel( InputCommandKey channel );
	AnimationGestureSelection ReadSelection();
	AnimationGestureModeInput ReadModeInput();
	bool IsUpBlocked();
	float Remaining( InputCommandKey channel );
	bool HasPending( InputCommandKey channel );
	InputCommandKey Motion( int configuration, int mode, int phase );
	void StartChannel( InputCommandKey channel, InputCommandKey motion );
	void QueueChannel( InputCommandKey channel, InputCommandKey motion, bool cycle );
	void PublishGesture( int configuration, bool down );
	float ReadDistance();
	void SetDistance( AnimationAttributeKey key, float value );
}
