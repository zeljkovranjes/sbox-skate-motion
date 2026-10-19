namespace SkateMotion.Core.Animation;

/// <summary>Emits each eligible clip record, including unconditional records and duplicates.</summary>
internal static class AnimationClipAttributeProducer
{
	internal static void Sample( ReadOnlySpan<AnimationClipAttribute> attributes,
		AnimationClipClock clock, float frameRate, AnimationEvaluatedAttributeBuffer output, byte requestedFlags )
	{
		foreach ( var attribute in attributes )
		{
			var unconditional = attribute.Start == -1f;
			var flags = unconditional ? (byte)4 : AnimationClipAttributeQuery.Classify( clock, attribute.Start, attribute.End );
			if ( !unconditional && ( (flags & requestedFlags & 0x1c) == 0 || (flags & requestedFlags & 3) == 0 ) )
				continue;
			var payload = attribute.Kind switch
			{
				0 => new AnimationAttributePayload( attribute.Payload.A, 0, 0, 0, 0, 0 ),
				1 => new AnimationAttributePayload( attribute.Payload.A, attribute.Payload.B, attribute.Payload.C, attribute.Payload.D, 0, 0 ),
				2 => new AnimationAttributePayload( BitConverter.SingleToUInt32Bits(
					attribute.Curve.Sample( frameRate * (clock.Position * clock.Rate) ) ), 0, 0, 0, 0, 0 ),
				3 => attribute.Payload,
				_ => default
			};
			output.Append( new AnimationAttributeQueryResult( payload, attribute.Name,
				(flags & 2) != 0 ? -1f : clock.Duration * attribute.Start,
				(flags & 2) != 0 ? -1f : clock.Duration * attribute.End, flags, attribute.Kind ) );
		}
	}
}
