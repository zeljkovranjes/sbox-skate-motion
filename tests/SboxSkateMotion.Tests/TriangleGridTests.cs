using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using SkateMotion.Core.Collision;
using Xunit;

namespace SboxSkateMotion.Tests;

public class TriangleGridTests
{
	// A map-like shape: a wide grid floor of small triangles, a few huge ones across it and some slivers.
	private static MeshTriangle[] Shape( int seed )
	{
		var random = new Random( seed );
		var triangles = new List<MeshTriangle>();
		for ( var y = 0; y < 60; y++ )
		{
			for ( var x = 0; x < 60; x++ )
			{
				var corner = new Vector3( x * 0.5f, y * 0.5f, random.NextSingle() * 0.05f );
				triangles.Add( new( corner, corner + new Vector3( 0.5f, 0f, 0f ), corner + new Vector3( 0.5f, 0.5f, 0f ), 0 ) );
				triangles.Add( new( corner, corner + new Vector3( 0.5f, 0.5f, 0f ), corner + new Vector3( 0f, 0.5f, 0f ), 0 ) );
			}
		}
		triangles.Add( new( new( -10f, -10f, 1f ), new( 40f, -10f, 1f ), new( 15f, 40f, 1f ), 0 ) );
		triangles.Add( new( new( 0f, 0f, -5f ), new( 30f, 30f, 5f ), new( 0f, 30f, 0f ), 0 ) );
		for ( var i = 0; i < 50; i++ )
		{
			var a = new Vector3( random.NextSingle() * 30f, random.NextSingle() * 30f, random.NextSingle() * 3f );
			triangles.Add( new( a, a + new Vector3( 0.001f, 0f, 0f ), a + new Vector3( 0f, 0.001f, 0f ), 0 ) );
		}
		return triangles.ToArray();
	}

	private static bool Overlaps( MeshTriangle triangle, Vector3 minimum, Vector3 maximum )
	{
		var low = Vector3.Min( triangle.A, Vector3.Min( triangle.B, triangle.C ) );
		var high = Vector3.Max( triangle.A, Vector3.Max( triangle.B, triangle.C ) );
		return !(low.X > maximum.X || low.Y > maximum.Y || low.Z > maximum.Z
			|| minimum.X > high.X || minimum.Y > high.Y || minimum.Z > high.Z);
	}

	[Fact]
	public void AQueryReturnsExactlyTheTrianglesAWholeWalkWouldKeepInTheSameOrder()
	{
		var triangles = Shape( 3 );
		var grid = TriangleGrid.Build( triangles );
		Assert.NotNull( grid );
		var random = new Random( 7 );
		var found = new List<int>();
		for ( var query = 0; query < 400; query++ )
		{
			var centre = new Vector3( random.NextSingle() * 40f - 5f, random.NextSingle() * 40f - 5f, random.NextSingle() * 6f - 3f );
			var half = new Vector3( random.NextSingle() * 3f, random.NextSingle() * 3f, random.NextSingle() * 2f );
			// Every so often a box reaching over the whole shape.
			if ( query % 50 == 0 )
				half = new Vector3( 60f );
			grid.Query( centre - half, centre + half, found );
			var expected = Enumerable.Range( 0, triangles.Length ).Where( i => Overlaps( triangles[ i ], centre - half, centre + half ) );
			Assert.Equal( expected, found );
		}
	}

	[Fact]
	public void SmallShapesAreWalkedWhole()
	{
		Assert.Null( TriangleGrid.Build( Shape( 1 ).Take( TriangleGrid.MinimumTriangles - 1 ).ToArray() ) );
	}

	[Fact]
	public void SweepsHitTheSameWithTheGridAsWithout()
	{
		var triangles = Shape( 5 );
		var minimum = triangles.Aggregate( new Vector3( float.MaxValue ), ( m, t ) => Vector3.Min( m, Vector3.Min( t.A, Vector3.Min( t.B, t.C ) ) ) );
		var maximum = triangles.Aggregate( new Vector3( -float.MaxValue ), ( m, t ) => Vector3.Max( m, Vector3.Max( t.A, Vector3.Max( t.B, t.C ) ) ) );
		var whole = new MeshQueryGeometry( triangles, minimum, maximum );
		var gridded = new MeshQueryGeometry( triangles, minimum, maximum, TriangleGrid.Build( triangles ) );
		var transform = Matrix4x4.CreateRotationZ( .3f ) * Matrix4x4.CreateTranslation( 2f, -1f, .5f );
		Matrix4x4.Invert( transform, out var inverse );
		MeshQueryInstance Instance( MeshQueryGeometry geometry ) =>
			new( geometry, transform, inverse, geometry.Minimum, geometry.Maximum, -1, 0, 1 );
		var query = new MeshWorldQuery( 1e-8f, -.00001f, 2.938735877055719e-39f, 1.52587890625e-5f, 1e-6f );
		var surfaces = new MeshQueryFilter( 0, 0, 0, 0, true, true );
		var random = new Random( 11 );
		var hits = 0;
		for ( var sweep = 0; sweep < 300; sweep++ )
		{
			var start = new Vector3( random.NextSingle() * 30f, random.NextSingle() * 30f, random.NextSingle() * 4f - 1f );
			var end = start + new Vector3( random.NextSingle() - .5f, random.NextSingle() - .5f, -random.NextSingle() * 3f );
			var request = new MeshQueryRequest( start, end, .1f, -1, 0, surfaces );
			var walked = query.Evaluate( request, new[] { Instance( whole ) } );
			Assert.Equal( walked, query.Evaluate( request, new[] { Instance( gridded ) } ) );
			if ( walked.HitCount != 0 )
				hits++;
		}
		// Most sweeps meet the shape, so the comparison covers real hits, not only misses.
		Assert.True( hits > 100, $"{hits} of 300 sweeps hit" );
	}
}
