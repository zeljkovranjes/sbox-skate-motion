using SkateMotion.Core.Grinding;

namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;
using static LimbSolverMath;

internal sealed partial class PhysicalHandSourceSelection
{
	private void ClipDepth( int index, float limit, bool upper )
	{
		if ( !Active[ index ] )
			return;
		var edge = Segments[ index ];
		var first = edge.Start.Z;
		var second = edge.End.Z;
		if ( upper ? first >= limit && second >= limit : limit >= first && limit >= second )
		{
			Active[ index ] = false;
			return;
		}
		var difference = second - first;
		if ( MinimumSquaredLength > MathF.Abs( difference ) )
			return;
		var fraction = MultiplyAdd( Reciprocal( difference ), limit - first, 0f );
		if ( !(fraction > 0f && 1f > fraction) )
			return;
		var point = CanonicalPoint( AddScaled( edge.End - edge.Start, fraction, edge.Start ) );
		Segments[ index ] = (upper ? second >= limit : limit >= second)
			? new RailSegment( edge.Start, point ) : new RailSegment( point, edge.End );
	}

	private void ClipSide( int index, Vector3 first, Vector3 second )
	{
		if ( !Active[ index ] )
			return;
		var edge = Segments[ index ];
		var direction = second - first;
		var x = direction.Y;
		var y = -direction.X;
		var inverse = InverseSquareRoot( MultiplyAdd( x, x, 0f ) + MultiplyAdd( y, y, 0f ) );
		x = MultiplyAdd( x, inverse, 0f );
		y = MultiplyAdd( y, inverse, 0f );
		var a = MultiplyAdd( edge.Start.X - first.X, x, 0f ) + MultiplyAdd( edge.Start.Y - first.Y, y, 0f );
		var b = MultiplyAdd( edge.End.X - first.X, x, 0f ) + MultiplyAdd( edge.End.Y - first.Y, y, 0f );
		if ( 0f > a && 0f > b )
		{
			Active[ index ] = false;
			return;
		}
		if ( a >= 0f && b >= 0f )
			return;
		var fraction = MultiplyAdd( Reciprocal( a - b ), a, 0f );
		var point = CanonicalPoint( AddScaled( edge.End - edge.Start, fraction, edge.Start ) );
		Segments[ index ] = 0f > a ? new RailSegment( point, edge.End ) : new RailSegment( edge.Start, point );
	}

	private static Vector3 CanonicalPoint( Vector3 point )
	{
		// Collapsed clipping boundaries retain canonical invalid coordinates in their scratch segments.
		var invalid = BitConverter.UInt32BitsToSingle( 0x7fc00000u );
		return new Vector3( float.IsNaN( point.X ) ? invalid : point.X,
			float.IsNaN( point.Y ) ? invalid : point.Y, float.IsNaN( point.Z ) ? invalid : point.Z );
	}
}
