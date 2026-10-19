namespace SkateMotion.Core.Collision;

using Vector3 = System.Numerics.Vector3;
using static SkateMotion.Core.Animation.LimbSolverMath;
using static SkateMotion.Core.Collision.MeshQueryMath;
using Scalar = SkateMotion.Core.Collision.CollisionScalarMath;

/// <summary>Tests the padded mesh bounds with the refined local segment interval.</summary>
internal sealed class MeshBoundsQuery
{
	private readonly float _padding;

	internal MeshBoundsQuery( float padding ) => _padding = padding;

	internal bool Intersects( Vector3 start, Vector3 end, float radius, Vector3 minimum, Vector3 maximum )
	{
		var halfDelta = Scale( end - start, .5f );
		var absoluteHalf = Vector3.Abs( halfDelta );
		var scale = Maximum( Maximum( Vector3.Abs( minimum ), Vector3.Abs( maximum ) ), Vector3.Abs( start ) );
		var extension = Maximum( absoluteHalf, Scale( scale, _padding ) ) - absoluteHalf;
		var padding = AddScaled( Maximum( Vector3.Abs( start ), Vector3.Abs( end ) ), _padding, new( radius ) ) + extension;
		var shift = new Vector3( MultiplyAdd( halfDelta.X >= 0f ? 1f : -1f, extension.X, 0f ),
			MultiplyAdd( halfDelta.Y >= 0f ? 1f : -1f, extension.Y, 0f ),
			MultiplyAdd( halfDelta.Z >= 0f ? 1f : -1f, extension.Z, 0f ) );
		start -= shift;
		end += shift;
		var delta = end - start;
		var inverse = new Vector3( Reciprocal( delta.X ), Reciprocal( delta.Y ), Reciprocal( delta.Z ) );
		var low = Product( inverse, (minimum - padding) - start );
		var high = Product( inverse, (maximum + padding) - start );
		var near = Minimum( low, high );
		var far = Maximum( low, high );
		var entry = SelectMaximum( SelectMaximum( near.X, near.Y ), near.Z );
		var exit = SelectMinimum( SelectMinimum( far.X, far.Y ), far.Z );
		entry = -entry >= 0d ? 0d : entry;
		exit = Scalar.Difference( 1d, exit ) >= 0d ? exit : 1d;
		return entry < exit;
	}

	private static Vector3 Product( Vector3 a, Vector3 b ) => new(
		MultiplyAdd( a.X, b.X, 0f ), MultiplyAdd( a.Y, b.Y, 0f ), MultiplyAdd( a.Z, b.Z, 0f ) );

	private static double SelectMaximum( double a, double b ) => Scalar.Difference( a, b ) >= 0d ? a : b;

	private static double SelectMinimum( double a, double b ) => Scalar.Difference( a, b ) >= 0d ? b : a;
}
