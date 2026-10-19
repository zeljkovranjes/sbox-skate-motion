namespace SkateMotion.Core.Animation;

/// <summary>Merges ordered channel records, retaining the overlay record for equal identities.</summary>
internal static class AnimationChannelAttributeProducer
{
	internal static void Sample( IAnimationEvaluatedAttributeNode baseNode, IAnimationEvaluatedAttributeNode overlay,
		AnimationEvaluatedAttributeContext context, AnimationEvaluatedAttributeBuffer output, byte flags )
	{
		var upper = context.Rent();
		try
		{
			overlay.SampleEvaluatedAttributes( context, upper, flags );
			if ( upper.Values.IsEmpty )
			{
				baseNode.SampleEvaluatedAttributes( context, output, flags );
				return;
			}
			var lower = context.Rent();
			try
			{
				baseNode.SampleEvaluatedAttributes( context, lower, flags );
				Merge( lower.Values, upper.Values, output );
			}
			finally
			{
				context.Return( lower );
			}
		}
		finally
		{
			context.Return( upper );
		}
	}

	private static void Merge( ReadOnlySpan<AnimationAttributeQueryResult> lower,
		ReadOnlySpan<AnimationAttributeQueryResult> upper, AnimationEvaluatedAttributeBuffer output )
	{
		var first = 0;
		var second = 0;
		while ( first < lower.Length && second < upper.Length )
		{
			var order = Compare( lower[ first ].Name, upper[ second ].Name );
			if ( order < 0 )
				output.Append( lower[ first++ ] );
			else
			{
				output.Append( upper[ second++ ] );
				if ( order == 0 )
					first++;
			}
		}
		while ( first < lower.Length )
			output.Append( lower[ first++ ] );
		while ( second < upper.Length )
			output.Append( upper[ second++ ] );
	}

	private static int Compare( AnimationAttributeKey left, AnimationAttributeKey right )
	{
		if ( left.A != right.A ) return left.A < right.A ? -1 : 1;
		if ( left.B != right.B ) return left.B < right.B ? -1 : 1;
		if ( left.C != right.C ) return left.C < right.C ? -1 : 1;
		if ( left.D != right.D ) return left.D < right.D ? -1 : 1;
		return left.E == right.E ? 0 : left.E < right.E ? -1 : 1;
	}
}
