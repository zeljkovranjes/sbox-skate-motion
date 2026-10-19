namespace SkateMotion.Core.Animation;

/// <summary>Blends typed attribute values while retaining the first record metadata.</summary>
internal static class AnimationAttributeBlend
{
	internal static AnimationAttributeQueryResult Blend( AnimationAttributeQueryResult result,
		AnimationAttributeQueryResult other, float weight )
	{
		var firstWeight = 1f - weight;
		var constant = ( result.Flags & 2 ) != 0;
		var start = constant ? -1f : firstWeight * result.Start;
		var end = constant ? -1f : firstWeight * result.End;
		var payload = result.Payload;
		var next = other.Payload;
		switch ( result.Kind )
		{
			case 0:
			case 2:
				payload = payload with { A = BlendScalar( payload.A, next.A, firstWeight, weight ) };
				break;
			case 1:
				payload = payload with
				{
					A = BlendVectorLane( payload.A, next.A, firstWeight, weight ),
					B = BlendVectorLane( payload.B, next.B, firstWeight, weight ),
					C = BlendVectorLane( payload.C, next.C, firstWeight, weight ),
					D = BlendVectorLane( payload.D, next.D, firstWeight, weight )
				};
				break;
			case 3:
				payload = payload with { F = BlendScalar( payload.F, next.F, firstWeight, weight ) };
				break;
		}
		return result with
		{
			Payload = payload,
			Start = start + ( constant ? 0f : weight * other.Start ),
			End = end + ( constant ? 0f : weight * other.End )
		};
	}

	private static uint BlendScalar( uint first, uint second, float firstWeight, float secondWeight ) =>
		BitConverter.SingleToUInt32Bits( MathF.FusedMultiplyAdd( secondWeight,
			BitConverter.UInt32BitsToSingle( second ), firstWeight * BitConverter.UInt32BitsToSingle( first ) ) );

	private static uint BlendVectorLane( uint first, uint second, float firstWeight, float secondWeight ) =>
		BitConverter.SingleToUInt32Bits( MathF.FusedMultiplyAdd( BitConverter.UInt32BitsToSingle( second ), secondWeight,
			MathF.FusedMultiplyAdd( BitConverter.UInt32BitsToSingle( first ), firstWeight, 0f ) ) );
}
