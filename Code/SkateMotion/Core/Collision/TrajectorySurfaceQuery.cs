namespace SkateMotion.Core.Collision;

using Vector3 = System.Numerics.Vector3;
using static SkateMotion.Core.Animation.LimbSolverMath;
using static SkateMotion.Core.Collision.MeshQueryMath;

/// <summary>Collects at most sixty-four nearby faces and selects their approach-compatible normal.</summary>
internal sealed class TrajectorySurfaceQuery
{
	private readonly Vector3[] _normals = new Vector3[ 64 ];
	private readonly bool[] _accepted = new bool[ 64 ];
	private readonly TrajectoryQuerySettings _settings;

	internal TrajectorySurfaceQuery( TrajectoryQuerySettings settings ) => _settings = settings;

	internal Vector3 Evaluate( TrajectoryQueryRequest request, Vector3 position, int sample, MeshQueryInstance[] instances )
	{
		var count = Collect( position, request.Radius, request.Filter, instances );
		var velocity = AddScaled( request.Acceleration, sample * _settings.SampleInterval, request.Velocity );
		var selected = Vector3.Zero;
		var greatest = -1f;
		var accepted = 0;
		for ( var i = 0; i < count; i++ )
		{
			var normal = _normals[ i ];
			_accepted[ i ] = normal.Y > _settings.GroundNormalY || Dot( normal, velocity ) <= 0f;
			if ( !_accepted[ i ] )
				continue;
			accepted++;
			var score = Dot( normal, Vector3.UnitY );
			if ( score > greatest )
			{
				selected = normal;
				greatest = score;
			}
		}
		if ( accepted == 0 )
			return Vector3.UnitY;

		var sum = Vector3.Zero;
		for ( var i = 0; i < count; i++ )
		{
			if ( _accepted[ i ] && Dot( selected, _normals[ i ] ) > .5f )
				sum += _normals[ i ];
		}
		var squared = Dot( sum, sum );
		var inverse = InverseSquareRoot( squared );
		var length = squared == 0f ? 0f : MultiplyAdd( squared, inverse, 0f );
		return length > _settings.MinimumNormalLength ? Scale( sum, inverse ) : Vector3.Zero;
	}

	private int Collect( Vector3 position, float radius, int filter, MeshQueryInstance[] instances )
	{
		var minimum = position - new Vector3( radius );
		var maximum = position + new Vector3( radius );
		var center = Scale( minimum + maximum, .5f );
		var extent = Scale( maximum - minimum, .5f );
		var count = 0;
		foreach ( var instance in instances )
		{
			if ( filter != -1 && instance.Filter != -1 && filter != instance.Filter )
				continue;
			var localCenter = TransformPoint( center, instance.InverseTransform );
			var localExtent = TransformExtent( extent, instance.InverseTransform );
			var localMinimum = localCenter - localExtent;
			var localMaximum = localCenter + localExtent;
			if ( !Overlaps( instance.Minimum, instance.Maximum, localMinimum, localMaximum ) )
				continue;
			// Nearby faces use the instance filter only, independently of the contact's exclusion masks.
			foreach ( var triangle in instance.Geometry.Triangles )
			{
				var low = Minimum( triangle.A, Minimum( triangle.B, triangle.C ) );
				var high = Maximum( triangle.A, Maximum( triangle.B, triangle.C ) );
				if ( !Overlaps( low, high, localMinimum, localMaximum ) )
					continue;
				var a = TransformPoint( triangle.A, instance.Transform );
				var b = TransformPoint( triangle.B, instance.Transform );
				var c = TransformPoint( triangle.C, instance.Transform );
				if ( !TriangleBoundsQuery.Intersects( minimum, maximum, a, b, c ) )
					continue;
				if ( count == _normals.Length )
					return count;
				_normals[ count++ ] = Normalize( Cross( b - a, c - a ) );
			}
		}
		return count;
	}
}
