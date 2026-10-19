namespace SkateMotion.Core.Input;

using Vector2 = System.Numerics.Vector2;

/// <summary>Retained progress through one ordered gesture path.</summary>
internal struct GesturePatternNode
{
	internal uint Packed { get; private set; }
	internal float Error { get; private set; }
	internal bool Complete => (Packed & 0x40000000u) != 0;
	internal int Span => (int)((Packed >> 16) & 0x3ffu);
	internal int Count => (int)((Packed >> 6) & 0xfu);

	internal GesturePatternNode( int count )
	{
		Packed = ((uint)(count - 1) << 26) | ((uint)count << 6);
		Error = 0f;
	}

	internal void Reset()
	{
		Packed &= 0x3c0003ffu;
		Error = 0f;
	}

	internal void Advance( GesturePattern pattern, Vector2 sample, byte maximumGap )
	{
		if ( (Packed & 0x80000000u) == 0 )
		{
			var error = DistanceSquared( pattern[ 0 ], sample );
			if ( error > pattern.SquaredTolerance )
				return;
			Error = error;
			Packed = ((uint)(Count - 2) << 26) | (Packed & 0xc00003ffu) | 0x80010000u;
			return;
		}
		if ( Complete )
			return;

		var reverseIndex = (int)((Packed >> 26) & 0xfu);
		var pointIndex = pattern.Count - 1 - reverseIndex;
		var distance = DistanceSquared( pattern[ pointIndex ], sample );
		if ( distance <= pattern.SquaredTolerance )
		{
			var span = ((uint)(Span + 1) << 16) & 0x03ff0000u;
			Error += distance;
			if ( reverseIndex == 0 )
			{
				// The terminal hit retains the gap field; intermediate hits clear it.
				Packed = (Packed & 0xfc00ffffu) | span | 0x40000000u;
			}
			else
			{
				Packed = ((uint)(reverseIndex - 1) << 26) | span | (Packed & 0xc00003ffu);
			}
			return;
		}

		if ( Count - reverseIndex != 2 || DistanceSquared( pattern[ pointIndex - 1 ], sample ) > pattern.SquaredTolerance )
		{
			var gap = ((((Packed >> 10) & 0x3fu) + 1u) << 10) & 0x0000fc00u;
			var span = ((uint)(Span + 1) << 16) & 0x03ff0000u;
			Packed = gap | span | (Packed & 0xfc0003ffu);
		}
		if ( ((Packed >> 10) & 0x3fu) > maximumGap )
			Reset();
	}

	internal static float DistanceSquared( Vector2 left, Vector2 right )
	{
		var x = left.X - right.X;
		var y = left.Y - right.Y;
		var squareX = x * x;
		var squareY = y * y;
		return squareX + squareY;
	}
}
