namespace SkateMotion.Core.Animation;

/// <summary>Samples the selected pair and intersects its ordered full-key record lists.</summary>
internal static class AnimationDirectionalAttributeProducer
{
	internal static void Sample( IAnimationEvaluatedAttributeNode first, IAnimationEvaluatedAttributeNode second,
		float weight, float threshold, AnimationEvaluatedAttributeContext context,
		AnimationEvaluatedAttributeBuffer output, byte flags )
	{
		if ( weight < threshold )
		{
			first.SampleEvaluatedAttributes( context, output, flags );
			return;
		}
		if ( 1f - threshold < weight )
		{
			second.SampleEvaluatedAttributes( context, output, flags );
			return;
		}
		var left = context.Rent();
		try
		{
			var right = context.Rent();
			try
			{
				first.SampleEvaluatedAttributes( context, left, flags );
				second.SampleEvaluatedAttributes( context, right, 31 );
				Merge( left.Values, right.Values, weight, output );
			}
			finally
			{
				context.Return( right );
			}
		}
		finally
		{
			context.Return( left );
		}
	}

	private static void Merge( ReadOnlySpan<AnimationAttributeQueryResult> first,
		ReadOnlySpan<AnimationAttributeQueryResult> second, float weight, AnimationEvaluatedAttributeBuffer output )
	{
		var next = 0;
		foreach ( var record in first )
		{
			while ( next < second.Length && Compare( second[ next ].Name, record.Name ) < 0 )
				next++;
			if ( next == second.Length )
				break;
			if ( second[ next ].Name == record.Name )
				output.Append( AnimationAttributeBlend.Blend( record, second[ next ], weight ) );
		}
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
