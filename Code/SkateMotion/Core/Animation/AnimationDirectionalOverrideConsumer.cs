namespace SkateMotion.Core.Animation;

/// <summary>Connects ordered channel parameters to retained directional playback selection.</summary>
internal sealed class AnimationDirectionalOverrideConsumer : IAnimationAttributeOverrideConsumer
{
	private readonly AnimationDirectionalPlayback _playback;
	private readonly AnimationAttributeKey _name;
	private readonly IAnimationDirectionalParameters _children;

	internal AnimationDirectionalOverrideConsumer( AnimationDirectionalPlayback playback,
		AnimationAttributeKey name, IAnimationDirectionalParameters children )
	{
		ArgumentNullException.ThrowIfNull( playback );
		ArgumentNullException.ThrowIfNull( children );
		_playback = playback;
		_name = name;
		_children = children;
	}

	/// <summary>Uses the first matching key, independent of owner, while retaining missing parameters.</summary>
	public bool UpdateOverrides( ReadOnlySpan<AnimationAttributeOverride> attributes )
	{
		foreach ( var attribute in attributes )
		{
			if ( attribute.Attribute.Name == _name )
				return _playback.UpdateParameters( _children, attribute.Attribute.Payload.Scalar,
					attribute.Normalized );
		}
		return _playback.UpdateParameters( _children, 0f, false, false );
	}
}
