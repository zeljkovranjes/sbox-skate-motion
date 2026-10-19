namespace SkateMotion.Core.Animation;

/// <summary>Ordered clip attribute lookup with playback-history filtering and typed value sampling.</summary>
internal static class AnimationClipAttributeQuery
{
	internal static bool TryGet( ReadOnlySpan<AnimationClipAttribute> attributes,
		AnimationClipClock clock, float frameRate, AnimationAttributeKey name,
		byte requestedFlags, ref AnimationAttributeQueryResult result )
	{
		foreach ( var attribute in attributes )
		{
			if ( attribute.Name != name )
				continue;
			var flags = Classify( clock, attribute.Start, attribute.End );
			if ( (flags & requestedFlags & 0x1c) == 0 || (flags & requestedFlags & 3) == 0 )
				continue;

			var payload = result.Payload;
			var source = attribute.Payload;
			switch ( attribute.Kind )
			{
				case 0:
					payload = payload with { A = source.A };
					break;
				case 1:
					payload = payload with { A = source.A, B = source.B, C = source.C, D = source.D };
					break;
				case 2:
					var frame = frameRate * (clock.Position * clock.Rate);
					payload = payload with { A = BitConverter.SingleToUInt32Bits( attribute.Curve.Sample( frame ) ) };
					break;
				case 3:
					payload = source;
					break;
			}

			var start = (flags & 2) != 0 ? -1f : clock.Duration * attribute.Start;
			var end = (flags & 2) != 0 ? -1f : clock.Duration * attribute.End;
			result = new AnimationAttributeQueryResult( payload, attribute.Name, start, end, flags, attribute.Kind );
			return true;
		}
		return false;
	}

	internal static byte Classify( AnimationClipClock clock, float start, float end )
	{
		if ( start == -1f )
			return 6;
		var startTime = start * clock.Duration;
		var endTime = end * clock.Duration;
		if ( clock.WrapCount == 0 )
		{
			if ( clock.PreviousPosition >= endTime )
				return 0x11;
		}
		else if ( clock.PreviousPosition < endTime && clock.Position < startTime )
		{
			return 9;
		}

		if ( clock.Position < startTime )
			return 0x11;
		return clock.Position <= endTime ? (byte)5 : (byte)9;
	}
}
