namespace SkateMotion.Core.Collision;

using Vector3 = System.Numerics.Vector3;
using static SkateMotion.Core.Animation.LimbSolverMath;
using static SkateMotion.Core.Collision.MeshQueryMath;

/// <summary>Tests a triangle against a closed box using edge axes and the face plane.</summary>
internal static class TriangleBoundsQuery
{
	internal static bool Intersects( Vector3 minimum, Vector3 maximum, Vector3 a, Vector3 b, Vector3 c )
	{
		var center = Scale( minimum + maximum, .5f );
		var extent = Scale( maximum - minimum, .5f );
		a -= center;
		b -= center;
		c -= center;
		var first = b - a;
		var second = c - b;
		var third = a - c;

		if ( SeparatesX( first, a, c, extent, true )
			|| SeparatesY( first, a, c, extent )
			|| SeparatesZ( first, b, c, extent )
			|| SeparatesX( second, a, c, extent, false )
			|| SeparatesY( second, a, c, extent )
			|| SeparatesZ( second, a, b, extent )
			|| SeparatesX( third, a, b, extent, false )
			|| SeparatesY( third, a, b, extent )
			|| SeparatesZ( third, b, c, extent ) )
			return false;

		var low = Minimum( a, Minimum( b, c ) );
		var high = Maximum( a, Maximum( b, c ) );
		if ( low.X > extent.X || low.Y > extent.Y || low.Z > extent.Z
			|| -extent.X > high.X || -extent.Y > high.Y || -extent.Z > high.Z )
			return false;

		var normal = Cross( first, second );
		var positive = new Vector3( normal.X > 0f ? extent.X : -extent.X,
			normal.Y > 0f ? extent.Y : -extent.Y, normal.Z > 0f ? extent.Z : -extent.Z );
		var offset = -Dot( normal, a );
		return offset + Dot( normal, positive ) >= 0f && offset + Dot( normal, -positive ) <= 0f;
	}

	private static bool SeparatesX( Vector3 edge, Vector3 a, Vector3 b, Vector3 extent, bool separateRadiusProducts )
	{
		var first = Product( edge.Z, a.Y ) - Product( edge.Y, a.Z );
		var second = Product( edge.Z, b.Y ) - Product( edge.Y, b.Z );
		var radius = Product( MathF.Abs( edge.Y ), extent.Z );
		// The first axis rounds both products before adding; later axes fuse the second product.
		radius = separateRadiusProducts
			? Product( MathF.Abs( edge.Z ), extent.Y ) + radius
			: MultiplyAdd( MathF.Abs( edge.Z ), extent.Y, radius );
		return Separates( first, second, radius );
	}

	private static bool SeparatesY( Vector3 edge, Vector3 a, Vector3 b, Vector3 extent )
	{
		var first = Product( edge.X, a.Z ) - Product( edge.Z, a.X );
		var second = Product( edge.X, b.Z ) - Product( edge.Z, b.X );
		var radius = MultiplyAdd( MathF.Abs( edge.Z ), extent.X, Product( MathF.Abs( edge.X ), extent.Z ) );
		return Separates( first, second, radius );
	}

	private static bool SeparatesZ( Vector3 edge, Vector3 a, Vector3 b, Vector3 extent )
	{
		var first = Product( edge.Y, a.X ) - Product( edge.X, a.Y );
		var second = Product( edge.Y, b.X ) - Product( edge.X, b.Y );
		var radius = MultiplyAdd( MathF.Abs( edge.Y ), extent.X, Product( MathF.Abs( edge.X ), extent.Y ) );
		return Separates( first, second, radius );
	}

	private static bool Separates( float first, float second, float radius )
	{
		var minimum = first > second ? second : first;
		var maximum = first > second ? first : second;
		return minimum > radius || -radius > maximum;
	}

	private static float Product( float a, float b ) => MultiplyAdd( a, b, 0f );
}
