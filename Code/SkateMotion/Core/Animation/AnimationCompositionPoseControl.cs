namespace SkateMotion.Core.Animation;

/// <summary>Prepares the active child before its reference and composes only when the active pose exists.</summary>
internal sealed class AnimationCompositionPoseControl
{
	private readonly IAnimationCompositionPoseOperations _operations;
	private readonly bool _firstActive;

	internal AnimationCompositionPoseControl( IAnimationCompositionPoseOperations operations, bool firstActive )
	{
		_operations = operations;
		_firstActive = firstActive;
	}

	internal bool HasPose { get; private set; }

	internal bool Prepare( bool enabled )
	{
		var active = _firstActive ? 0 : 1;
		if ( !_operations.Prepare( active, enabled ) )
		{
			HasPose = false;
			return false;
		}
		_operations.Prepare( 1 - active, enabled );
		_operations.Compose( _firstActive, true );
		HasPose = true;
		return true;
	}

	internal void Sample() => _operations.Sample( _firstActive ? 0 : 1 );
}
