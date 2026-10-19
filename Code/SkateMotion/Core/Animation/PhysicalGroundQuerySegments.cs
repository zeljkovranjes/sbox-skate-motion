namespace SkateMotion.Core.Animation;

using Vector4 = System.Numerics.Vector4;
using M = LimbSolverMath;

internal static class PhysicalGroundQuerySegments
{
	internal const int CandidateWordStride = 16;
	internal const int SegmentWordStride = 24;

	/// <summary>
	/// Groups ordered query candidates into surface segments. Candidate positions occupy words 0..3.
	/// Segment words are category at 0, start at 4, end at 8, direction at 12, normal at 16 and length at 20.
	/// Padding and unused records retain their previous contents. All vectors share the physical query frame.
	/// </summary>
	internal static int Build( ReadOnlySpan<uint> candidates, int candidateCount,
		Vector4 queryAxis, Vector4 upAxis, Vector4 crossAxis, Span<uint> segments, out int lastNeutralEnd )
	{
		if ( candidateCount < 0 || candidateCount > candidates.Length / CandidateWordStride )
			throw new ArgumentOutOfRangeException( nameof( candidateCount ) );
		if ( segments.Length / SegmentWordStride < Math.Max( candidateCount - 1, 0 ) )
			throw new ArgumentException( "Segment storage must accommodate every candidate pair.", nameof( segments ) );

		var count = 0;
		lastNeutralEnd = 0;
		for ( var index = 1; index < candidateCount; index++ )
		{
			var start = Read( candidates, (index - 1) * CandidateWordStride );
			var end = Read( candidates, index * CandidateWordStride );
			var delta = end - start;
			var length = M.SquareRoot( Dot( delta, delta ) );
			if ( length < BitConverter.UInt32BitsToSingle( 0x38d1b717 ) )
				continue;
			var direction = Scale( delta, M.Reciprocal( length ) );
			var category = MathF.Abs( Dot( queryAxis, direction ) ) > .5f
				? 0u : Dot( upAxis, direction ) < 0f ? 2u : 1u;

			if ( count > 0 && category != 0 && segments[ (count - 1) * SegmentWordStride ] == category )
			{
				var offset = (count - 1) * SegmentWordStride;
				Write( segments, offset + 8, end );
				delta = end - Read( segments, offset + 4 );
				length = M.SquareRoot( Dot( delta, delta ) );
				segments[ offset + 20 ] = BitConverter.SingleToUInt32Bits( length );
				Write( segments, offset + 12, Scale( delta, M.Reciprocal( length ) ) );
				Write( segments, offset + 16, Normal( delta, crossAxis, upAxis ) );
				continue;
			}

			var next = count++ * SegmentWordStride;
			segments[ next ] = category;
			Write( segments, next + 4, start );
			Write( segments, next + 8, end );
			Write( segments, next + 12, direction );
			Write( segments, next + 16, Normal( delta, crossAxis, upAxis ) );
			segments[ next + 20 ] = BitConverter.SingleToUInt32Bits( length );
			if ( category == 0 )
				lastNeutralEnd = count;
		}
		return count;
	}

	private static Vector4 Normal( Vector4 delta, Vector4 crossAxis, Vector4 fallback )
	{
		var cross = new Vector4(
			M.SubtractProduct( crossAxis.Y, delta.Z, M.MultiplyAdd( delta.Y, crossAxis.Z, 0f ) ),
			M.SubtractProduct( crossAxis.Z, delta.X, M.MultiplyAdd( delta.Z, crossAxis.X, 0f ) ),
			M.SubtractProduct( crossAxis.X, delta.Y, M.MultiplyAdd( delta.X, crossAxis.Y, 0f ) ),
			M.SubtractProduct( crossAxis.W, delta.W, M.MultiplyAdd( delta.W, crossAxis.W, 0f ) ) );
		var squared = Dot( cross, cross );
		var inverse = M.InverseSquareRoot( squared );
		var length = squared == 0f ? 0f : M.MultiplyAdd( squared, inverse, 0f );
		return length > BitConverter.UInt32BitsToSingle( 0x358637bd ) ? Scale( cross, inverse ) : fallback;
	}

	private static float Dot( Vector4 left, Vector4 right ) =>
		(M.MultiplyAdd( left.X, right.X, 0f ) + M.MultiplyAdd( left.Y, right.Y, 0f ))
		+ M.MultiplyAdd( left.Z, right.Z, 0f );

	private static Vector4 Scale( Vector4 value, float factor ) => new(
		M.MultiplyAdd( value.X, factor, 0f ), M.MultiplyAdd( value.Y, factor, 0f ),
		M.MultiplyAdd( value.Z, factor, 0f ), M.MultiplyAdd( value.W, factor, 0f ) );

	private static Vector4 Read( ReadOnlySpan<uint> words, int offset ) => new(
		BitConverter.UInt32BitsToSingle( words[ offset ] ), BitConverter.UInt32BitsToSingle( words[ offset + 1 ] ),
		BitConverter.UInt32BitsToSingle( words[ offset + 2 ] ), BitConverter.UInt32BitsToSingle( words[ offset + 3 ] ) );

	private static void Write( Span<uint> words, int offset, Vector4 value )
	{
		words[ offset ] = BitConverter.SingleToUInt32Bits( value.X );
		words[ offset + 1 ] = BitConverter.SingleToUInt32Bits( value.Y );
		words[ offset + 2 ] = BitConverter.SingleToUInt32Bits( value.Z );
		words[ offset + 3 ] = BitConverter.SingleToUInt32Bits( value.W );
	}
}
