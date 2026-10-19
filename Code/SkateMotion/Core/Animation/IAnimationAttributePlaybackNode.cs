namespace SkateMotion.Core.Animation;

/// <summary>Queries authored attributes through the same retained state used for playback.</summary>
internal interface IAnimationAttributePlaybackNode
{
	/// <summary>Queries a value, retaining any output mutations even when the lookup fails.</summary>
	bool TryGetAuthoredAttribute( AnimationAttributeKey name, byte flags, ref AnimationAttributeQueryResult result );
}
