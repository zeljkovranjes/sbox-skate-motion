using System;
using System.Numerics;
using SkateMotion.Core.Collision;
using Xunit;

namespace SboxSkateMotion.Tests;

public class TrajectoryWorldQueryTests
{
	private static TrajectoryQuerySettings Settings => new( 1f / 60f, .0001f, .7f, 1e-6f );
	private static TrajectoryQueryRequest Request => new( new( -1, 1, -1 ), new( 0, -1, 0 ), new( 0, -9.81f, 0 ),
		1, .1f, .25f, 1.2f, -1, 0, new( 0, 0, 0, 0, true, true ) );
	private static MeshTriangle Floor => new( new( -5, 0, -5 ), new( -5, 0, 5 ), new( 5, 0, -5 ), 3 );
	private static MeshTriangle Slope => new( new( -5, -3, -5 ), new( -5, -3, 5 ), new( 5, 3, -5 ), 4 );

	private static TrajectoryWorldQuery Create() => new( new( 1e-8f, -.00001f, 2.938735877055719e-39f,
		1.52587890625e-5f, 1e-6f ), Settings );

	private static MeshQueryInstance Instance( params MeshTriangle[] triangles )
	{
		var identity = Matrix4x4.Identity;
		identity.M44 = 0;
		return new( new( triangles, new( -5 ), new( 5 ) ), identity, identity, new( -5 ), new( 5 ), -1, 0, 123 );
	}

	[Fact]
	public void EmptyGeometryReturnsExplicitInvalidTimeAndSample()
	{
		var result = Create().Evaluate( Request, [] );
		Assert.Equal( -1, result.Sample );
		Assert.Equal( -1f, result.Time );
		Assert.Equal( Vector3.Zero, result.Position );
		Assert.Equal( Vector3.UnitY, result.Normal );
		Assert.Equal( Vector3.UnitY, result.SurfaceNormal );
		Assert.Equal( 0u, result.Identity );
	}

	[Fact]
	public void FallingTrajectoryFindsSurfaceAndItsMaterial()
	{
		var result = Create().Evaluate( Request, [Instance( Floor )] );
		Assert.InRange( result.Time, .3f, .4f );
		Assert.InRange( result.Sample, 18, 24 );
		Assert.InRange( MathF.Abs( result.Position.Y ), 0, 1e-5f );
		Assert.Equal( Vector3.UnitY, result.SurfaceNormal );
		Assert.Equal( (ushort)3, result.Material );
		Assert.Equal( 123u, result.Identity );
	}

	[Fact]
	public void ShortDurationDoesNotInventAContact()
	{
		Assert.Equal( -1, Create().Evaluate( Request with { Duration = .1f }, [Instance( Floor )] ).Sample );
	}

	[Fact]
	public void LinearMotionRetainsItsDistinctSampleScaling()
	{
		var result = Create().Evaluate( Request with { Acceleration = Vector3.Zero }, [Instance( Floor )] );
		Assert.Equal( 1, result.Sample );
		Assert.Equal( 1f / 60f, result.Time );
	}

	[Fact]
	public void ZeroDurationReturnsWithoutSampling()
	{
		Assert.Equal( -1, Create().Evaluate( Request with { Duration = 0 }, [Instance( Floor )] ).Sample );
	}

	[Theory]
	[InlineData( 0 )]
	[InlineData( -1 )]
	public void NonpositiveRadiusCannotCreateAStalledStepLoop( float radius )
	{
		Assert.Throws<ArgumentOutOfRangeException>( () => Create().Evaluate( Request with { Radius = radius }, [] ) );
	}

	[Fact]
	public void ContactRespectsInstanceAndMaterialExclusions()
	{
		var solver = Create();
		Assert.Equal( -1, solver.Evaluate( Request with { Filter = 7 }, [Instance( Floor ) with { Filter = 8 }] ).Sample );
		Assert.Equal( -1, solver.Evaluate( Request with { Exclusion = 1 }, [Instance( Floor ) with { Exclusion = 1 }] ).Sample );
		Assert.Equal( -1, solver.Evaluate( Request with { Surfaces = new( 8, 0, 0, 0, true, true ) }, [Instance( Floor )] ).Sample );
	}

	[Fact]
	public void QueryReuseDoesNotRetainThePreviousHit()
	{
		var solver = Create();
		Assert.True( solver.Evaluate( Request, [Instance( Floor )] ).Sample >= 0 );
		Assert.Equal( -1, solver.Evaluate( Request, [] ).Sample );
	}

	[Fact]
	public void NearbySurfaceCollectionUsesItsOwnFilterRules()
	{
		var surface = new TrajectorySurfaceQuery( Settings );
		var request = Request with { Exclusion = 1, Surfaces = new( uint.MaxValue, uint.MaxValue, uint.MaxValue, uint.MaxValue, false, false ) };
		var instance = Instance( Slope ) with { Exclusion = 1 };
		var result = surface.Evaluate( request, Vector3.Zero, 0, [instance] );
		Assert.True( result.X < -.4f );
		Assert.True( result.Y > .8f );
		Assert.Equal( Vector3.UnitY, surface.Evaluate( request with { Filter = 7 }, Vector3.Zero, 0, [instance with { Filter = 8 }] ) );
	}

	[Fact]
	public void NearbySurfaceCapacityCountsFacesBeforeNormalSelection()
	{
		var surface = new TrajectorySurfaceQuery( Settings );
		Vector3 Query( int slopes )
		{
			var faces = new MeshTriangle[ slopes + 1 ];
			Array.Fill( faces, Slope );
			faces[ slopes ] = Floor;
			return surface.Evaluate( Request, Vector3.Zero, 0, [Instance( faces )] );
		}
		var below = Query( 63 );
		var full = Query( 64 );
		Assert.True( below.Y > full.Y );
		Assert.Equal( full, Query( 65 ) );
	}

	[Fact]
	public void RecedingUndersideKeepsTheDefaultSurfaceDirection()
	{
		var face = new MeshTriangle( Floor.C, Floor.B, Floor.A, 3 );
		Assert.Equal( Vector3.UnitY, new TrajectorySurfaceQuery( Settings ).Evaluate( Request, Vector3.Zero, 0, [Instance( face )] ) );
	}
}
