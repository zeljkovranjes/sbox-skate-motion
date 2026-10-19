namespace SkateMotion.Core.Animation;

/// <summary>Queries a selected pair with independent endpoint, filter and value blending rules.</summary>
internal static class AnimationDirectionalAttributeQuery
{
	internal static bool TryGet( IAnimationAttributePlaybackNode first, IAnimationAttributePlaybackNode second,
		float weight, float threshold, AnimationAttributeKey name, byte flags, ref AnimationAttributeQueryResult result )
	{
		if ( weight < threshold )
			return first.TryGetAuthoredAttribute( name, flags, ref result );
		if ( 1f - threshold < weight )
			return second.TryGetAuthoredAttribute( name, flags, ref result );

		var other = new AnimationAttributeQueryResult( default, default, -1f, -1f, 6, 0 );
		var firstFound = first.TryGetAuthoredAttribute( name, flags, ref result );
		var secondFound = second.TryGetAuthoredAttribute( name, 31, ref other );
		if ( !firstFound || !secondFound )
			return false;

		result = AnimationAttributeBlend.Blend( result, other, weight );
		return true;
	}
}
