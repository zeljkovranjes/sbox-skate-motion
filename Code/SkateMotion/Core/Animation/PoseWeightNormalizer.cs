namespace SkateMotion.Core.Animation;

/// <summary>Prepares pose-cache weights using an ordered sum and one rounded reciprocal.</summary>
internal static class PoseWeightNormalizer
{
	internal static float Normalize( float[] rawWeights, float[] normalizedWeights )
	{
		if ( rawWeights.Length == 0 || normalizedWeights.Length != rawWeights.Length )
			throw new ArgumentException( "Weight arrays must have the same nonzero count." );
		var total = 0f;
		for ( var i = 0; i < rawWeights.Length; i++ )
			total += rawWeights[ i ];
		var reciprocal = 1f / total;
		for ( var i = 0; i < rawWeights.Length; i++ )
			normalizedWeights[ i ] = rawWeights[ i ] * reciprocal;
		return total;
	}
}
