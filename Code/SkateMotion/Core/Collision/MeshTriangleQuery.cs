namespace SkateMotion.Core.Collision;

using Vector3 = System.Numerics.Vector3;
using Matrix4x4 = System.Numerics.Matrix4x4;
using static SkateMotion.Core.Animation.LimbSolverMath;
using static SkateMotion.Core.Collision.MeshQueryMath;
using Scalar = SkateMotion.Core.Collision.CollisionScalarMath;

/// <summary>Transforms a mesh triangle, queries it and accumulates filtered contact data.</summary>
internal sealed class MeshTriangleQuery
{
	private readonly TriangleSweepQuery _triangle;

	internal MeshTriangleQuery( float minimumDeterminant, float boundaryTolerance, float featureEpsilon )
	{
		_triangle = new( minimumDeterminant, boundaryTolerance, featureEpsilon );
	}

	internal bool Evaluate( Vector3 start, Vector3 delta, float radius,
		Vector3 localMinimum, Vector3 localMaximum, MeshTriangle triangle,
		Matrix4x4 transform, uint identity, MeshQueryFilter filter, ref MeshContact result )
	{
		var minimum = Vector3.Min( triangle.A, Vector3.Min( triangle.B, triangle.C ) );
		var maximum = Vector3.Max( triangle.A, Vector3.Max( triangle.B, triangle.C ) );
		if ( minimum.X > localMaximum.X || minimum.Y > localMaximum.Y || minimum.Z > localMaximum.Z
			|| localMinimum.X > maximum.X || localMinimum.Y > maximum.Y || localMinimum.Z > maximum.Z )
			return false;

		var a = TransformPoint( triangle.A, transform );
		var b = TransformPoint( triangle.B, transform );
		var c = TransformPoint( triangle.C, transform );
		var contact = default(TriangleContact);
		if ( !_triangle.Evaluate( start, delta, a, b, c, radius, 0f, ref contact )
			|| filter.Excludes( triangle.Material ) )
			return false;

		// Mesh selection uses the decoded face normal even for edge or vertex contacts.
		var normal = Normalize( Cross( b - a, c - a ) );
		if ( !(filter.Front && filter.Back) && !(Dot( delta, normal ) >= 0f ? filter.Back : filter.Front) )
			return false;

		var fraction = Scalar.Widen( contact.Fraction );
		fraction = fraction <= 0d ? 0d : fraction;
		fraction = fraction <= 1d ? fraction : 1d;
		if ( Scalar.Widen( result.Fraction ) > fraction )
		{
			result = result with
			{
				Position = contact.Position,
				Normal = normal,
				Transform = transform,
				Fraction = Scalar.Narrow( fraction ),
				Material = triangle.Material,
				Identity = identity
			};
		}
		result = result with { HitCount = unchecked(result.HitCount + 1u) };
		return true;
	}
}
