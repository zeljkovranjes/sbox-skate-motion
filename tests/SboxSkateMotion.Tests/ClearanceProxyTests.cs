using System.Numerics;
using SkateMotion.Core.BoardModel;
using Xunit;

namespace SboxSkateMotion.Tests;

public class ClearanceProxyTests
{
	[Fact]
	public void ADenseWheelBecomesFewerTrianglesThatStayOnOrOutsideIt()
	{
		var (points, normals, triangles) = Wheel( 0.03f, 0.035f, 200, 12 );
		var (proxy, kept) = ClearanceProxy.Build( points, normals, triangles, 0.004f );
		Assert.True( kept.Length * 4 < triangles.Count, $"{kept.Length / 3} of {triangles.Count / 3} triangles kept" );
		// Every corner of the stand-in lies on or outside the tyre, never more than half a millimetre inside it.
		foreach ( var index in kept )
		{
			var point = proxy[ index ];
			var radial = MathF.Sqrt( point.X * point.X + point.Z * point.Z );
			var inside = MathF.Min( 0.03f - radial, 0.0175f - MathF.Abs( point.Y ) );
			Assert.True( inside < 0.0005f, $"{point} is {inside * 1000:F2} mm inside the wheel" );
		}
		// Its lowest point is the tyre's.
		var lowest = kept.Min( index => proxy[ index ].Z );
		Assert.InRange( lowest, -0.0302f, -0.0295f );
	}

	[Fact]
	public void TheTwoSidesOfAThinDeckStayApart()
	{
		// A 10 mm thick slab with each face finely divided: the cells never join its top to its underside.
		var points = new List<Vector3>();
		var normals = new List<Vector3>();
		var triangles = new List<int>();
		foreach ( var (z, up) in new[] { (0.005f, 1f), (-0.005f, -1f) } )
		{
			var first = points.Count;
			for ( var i = 0; i <= 40; i++ )
			{
				for ( var j = 0; j <= 10; j++ )
				{
					points.Add( new Vector3( i * 0.005f, j * 0.005f, z ) );
					normals.Add( new Vector3( 0f, 0f, up ) );
				}
			}
			for ( var i = 0; i < 40; i++ )
			{
				for ( var j = 0; j < 10; j++ )
				{
					var a = first + i * 11 + j;
					triangles.AddRange( up > 0f ? new[] { a, a + 11, a + 12, a, a + 12, a + 1 } : new[] { a, a + 12, a + 11, a, a + 1, a + 12 } );
				}
			}
		}
		var (proxy, kept) = ClearanceProxy.Build( points, normals, triangles, 0.008f );
		Assert.True( kept.Length < triangles.Count );
		Assert.Equal( 0.005f, kept.Max( index => proxy[ index ].Z ), 4 );
		Assert.Equal( -0.005f, kept.Min( index => proxy[ index ].Z ), 4 );
	}

	// A closed wheel about the Y axis: tread and two side faces, segments round and rings across.
	private static (List<Vector3> Points, List<Vector3> Normals, List<int> Triangles) Wheel( float radius, float width, int segments, int rings )
	{
		var points = new List<Vector3>();
		var normals = new List<Vector3>();
		var triangles = new List<int>();
		for ( var r = 0; r <= rings; r++ )
		{
			var y = -width * 0.5f + width * r / rings;
			for ( var s = 0; s < segments; s++ )
			{
				var angle = s * MathF.Tau / segments;
				var radial = new Vector3( MathF.Cos( angle ), 0f, MathF.Sin( angle ) );
				points.Add( radial * radius + new Vector3( 0f, y, 0f ) );
				normals.Add( radial );
			}
		}
		for ( var r = 0; r < rings; r++ )
		{
			for ( var s = 0; s < segments; s++ )
			{
				var a = r * segments + s;
				var b = r * segments + (s + 1) % segments;
				triangles.AddRange( new[] { a, b + segments, b, a, a + segments, b + segments } );
			}
		}
		foreach ( var side in new[] { -1f, 1f } )
		{
			var centre = points.Count;
			points.Add( new Vector3( 0f, side * width * 0.5f, 0f ) );
			normals.Add( new Vector3( 0f, side, 0f ) );
			var rim = side < 0f ? 0 : rings * segments;
			for ( var s = 0; s < segments; s++ )
			{
				var a = rim + s;
				var b = rim + (s + 1) % segments;
				triangles.AddRange( side < 0f ? new[] { centre, a, b } : new[] { centre, b, a } );
			}
		}
		return (points, normals, triangles);
	}
}
