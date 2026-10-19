using System;
using System.Numerics;
using SkateMotion.Core.Animation;
using SkateMotion.Core.Grinding;
using Xunit;

namespace SboxSkateMotion.Tests;

public class GrindSurfaceProbePlanTests
{
	[Fact]
	public void HorizontalEdgeBuildsOrderedCrossSectionAndOverheadSweep()
	{
		var plan = Create();
		plan.Build( Edge(), new Vector3( 1f, 5f, 6f ), 1f, false, Vector3.Zero );
		Assert.Equal( 6, plan.Count );
		Assert.Equal( Vector3.UnitX, plan.Center );
		Assert.Equal( Vector3.UnitY, plan.Up );
		Assert.Equal( Vector3.UnitX, plan.Direction );
		Assert.Equal( new GrindSurfaceProbe( new Vector3( 1f, .5f, -.25f ), new Vector3( 1f, -.5f, -.25f ), 0f ), plan.GetProbe( 0 ) );
		Assert.Equal( new GrindSurfaceProbe( new Vector3( 1f, .5f, .25f ), new Vector3( 1f, -.5f, .25f ), 0f ), plan.GetProbe( 1 ) );
		Assert.Equal( new GrindSurfaceProbe( new Vector3( 1f, 2f, -1f ), new Vector3( 1f, -2f, -1f ), 0f ), plan.GetProbe( 2 ) );
		Assert.Equal( new GrindSurfaceProbe( new Vector3( 1f, 2f, 1f ), new Vector3( 1f, -2f, 1f ), 0f ), plan.GetProbe( 3 ) );
		Assert.Equal( new GrindSurfaceProbe( new Vector3( 1f, .5f, 0f ), new Vector3( 1f, -.5f, 0f ), 0f ), plan.GetProbe( 4 ) );
		Assert.Equal( new GrindSurfaceProbe( new Vector3( 1f, .25f, -.25f ), new Vector3( 1f, .25f, .25f ), .125f ), plan.GetProbe( 5 ) );
	}

	[Fact]
	public void ReferenceProjectionIsNotClampedToEdgeEndpoints()
	{
		var plan = Create();
		plan.Build( Edge(), new Vector3( 20f, 5f, 6f ), 1f, false, Vector3.Zero );
		Assert.Equal( new Vector3( 20f, 0f, 0f ), plan.Center );
	}

	[Fact]
	public void ReversingTheEdgeSwapsCrossSectionSides()
	{
		var forward = Create();
		var reverse = Create();
		forward.Build( Edge(), Vector3.UnitY, 1f, false, Vector3.Zero );
		reverse.Build( new RailSegment( Edge().End, Edge().Start ), Vector3.UnitY, 1f, false, Vector3.Zero );
		Assert.Equal( -forward.Direction, reverse.Direction );
		Assert.Equal( forward.Up, reverse.Up );
		Assert.Equal( forward.GetProbe( 0 ), reverse.GetProbe( 1 ) );
		Assert.Equal( forward.GetProbe( 2 ), reverse.GetProbe( 3 ) );
	}

	[Fact]
	public void OptionalProbeUsesSuppliedOriginAndWorldDownOnInclines()
	{
		var plan = Create();
		var origin = new Vector3( 9f, 8f, 7f );
		plan.Build( new RailSegment( Vector3.Zero, new Vector3( 4f, 2f, 0f ) ), Vector3.One, 1f, true, origin );
		Assert.Equal( 7, plan.Count );
		Assert.Equal( new GrindSurfaceProbe( origin, origin - Vector3.UnitY * 3f, 0f ), plan.GetProbe( 6 ) );
		Assert.NotEqual( Vector3.UnitY, plan.Up );
	}

	[Fact]
	public void RebuildHidesOldOptionalAndRejectedProbes()
	{
		var plan = Create();
		plan.Build( Edge(), Vector3.Zero, 1f, true, Vector3.Zero );
		plan.Build( Edge(), Vector3.Zero, 1f, false, Vector3.Zero );
		Assert.Equal( 6, plan.Count );
		Assert.Throws<ArgumentOutOfRangeException>( () => plan.GetProbe( 6 ) );
		plan.Build( new RailSegment( Vector3.Zero, Vector3.UnitY ), Vector3.One, 1f, false, Vector3.Zero );
		Assert.Equal( 0, plan.Count );
		Assert.Equal( Vector3.One, plan.Center );
		Assert.Equal( Vector3.Zero, plan.Up );
		Assert.Throws<ArgumentOutOfRangeException>( () => plan.GetProbe( 0 ) );
	}

	[Theory]
	[InlineData( .99f, 0 )]
	[InlineData( 1f, 6 )]
	[InlineData( 1.01f, 6 )]
	public void DegeneracyGateUsesUnnormalizedUpLengthAndIncludesEquality( float length, int count )
	{
		var plan = Create( minimumUpLength: 1f );
		plan.Build( new RailSegment( Vector3.Zero, Vector3.UnitX * length ), Vector3.Zero, 1f, false, Vector3.Zero );
		Assert.Equal( count, plan.Count );
	}

	[Fact]
	public void ZeroLengthEdgeProducesNoQueries()
	{
		var plan = Create();
		plan.Build( new RailSegment( Vector3.One, Vector3.One ), Vector3.UnitZ, 1f, true, Vector3.Zero );
		Assert.Equal( 0, plan.Count );
		Assert.Equal( Vector3.Zero, plan.Direction );
	}

	[Fact]
	public void ZeroReachCollapsesOnlyFarProbes()
	{
		var plan = Create();
		plan.Build( Edge(), Vector3.Zero, 0f, false, Vector3.Zero );
		Assert.Equal( new GrindSurfaceProbe( Vector3.Zero, Vector3.Zero, 0f ), plan.GetProbe( 2 ) );
		Assert.Equal( plan.GetProbe( 2 ), plan.GetProbe( 3 ) );
		Assert.NotEqual( plan.GetProbe( 0 ).Start, plan.GetProbe( 0 ).End );
	}

	private static RailSegment Edge() => new( -Vector3.UnitX * 2f, Vector3.UnitX * 2f );

	private static GrindSurfaceProbePlan Create( float minimumUpLength = 1e-5f ) => new(
		new GrindSurfaceProbeSettings( 1e-6f, minimumUpLength, .25f, .5f, 2f, .125f, 3f ) );
}
