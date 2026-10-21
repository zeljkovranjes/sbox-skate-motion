namespace SkateMotion.Core.Collision;

using Vector3 = System.Numerics.Vector3;
using Matrix4x4 = System.Numerics.Matrix4x4;
using static SkateMotion.Core.Animation.LimbSolverMath;
using static SkateMotion.Core.Collision.MeshQueryMath;

/// <summary>Dispatches a world segment through ordered mesh instances and their triangles.</summary>
internal sealed class MeshWorldQuery
{
	private readonly MeshTriangleQuery _triangle;
	private readonly MeshBoundsQuery _bounds;
	private readonly float _minimumMovement;
	private readonly System.Collections.Generic.List<int> _nearby = new();

	internal MeshWorldQuery( float minimumDeterminant, float boundaryTolerance, float featureEpsilon,
		float minimumMovement, float boundsPadding )
	{
		_triangle = new( minimumDeterminant, boundaryTolerance, featureEpsilon );
		_bounds = new( boundsPadding );
		_minimumMovement = minimumMovement;
	}

	internal MeshContact Evaluate( MeshQueryRequest request, MeshQueryInstance[] instances )
	{
		var identity = Matrix4x4.Identity;
		identity.M44 = 0f;
		var result = new MeshContact( Vector3.Zero, Vector3.UnitY, identity, float.MaxValue, 0, 0, 0 );
		var delta = request.End - request.Start;
		var movement = Vector3.Abs( delta );
		if ( !(movement.X > _minimumMovement || movement.Y > _minimumMovement || movement.Z > _minimumMovement) )
			return result;

		var minimum = Minimum( request.Start, request.End ) - new Vector3( request.Radius );
		var maximum = Maximum( request.Start, request.End ) + new Vector3( request.Radius );
		var center = Scale( maximum + minimum, .5f );
		var extent = Scale( maximum - minimum, .5f );
		foreach ( var instance in instances )
		{
			if ( request.Filter != -1 && instance.Filter != -1 && request.Filter != instance.Filter )
				continue;
			if ( (request.Exclusion & instance.Exclusion) != 0 )
				continue;
			var localCenter = TransformPoint( center, instance.InverseTransform );
			var localExtent = TransformExtent( extent, instance.InverseTransform );
			var localMinimum = localCenter - localExtent;
			var localMaximum = localCenter + localExtent;
			if ( !Overlaps( instance.Minimum, instance.Maximum, localMinimum, localMaximum ) )
				continue;
			var geometry = instance.Geometry;
			if ( !_bounds.Intersects( TransformPoint( request.Start, instance.InverseTransform ),
				TransformPoint( request.End, instance.InverseTransform ), request.Radius, geometry.Minimum, geometry.Maximum ) )
				continue;
			// A large shape's grid gives just the triangles whose bounds reach the sweep's, in order; the triangle test
			// skips every other one anyway.
			if ( geometry.Grid is { } grid )
			{
				grid.Query( localMinimum, localMaximum, _nearby );
				foreach ( var index in _nearby )
				{
					_triangle.Evaluate( request.Start, delta, request.Radius, localMinimum, localMaximum,
						geometry.Triangles[ index ], instance.Transform, instance.Identity, request.Surfaces, ref result );
				}
				continue;
			}
			foreach ( var triangle in geometry.Triangles )
			{
				_triangle.Evaluate( request.Start, delta, request.Radius, localMinimum, localMaximum,
					triangle, instance.Transform, instance.Identity, request.Surfaces, ref result );
			}
		}
		return result;
	}
}
