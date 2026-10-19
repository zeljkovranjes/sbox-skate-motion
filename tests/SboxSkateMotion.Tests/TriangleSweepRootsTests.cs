using System;
using System.Numerics;
using SkateMotion.Core.Collision;
using Xunit;

namespace SboxSkateMotion.Tests;

public class TriangleSweepRootsTests
{
	private static readonly SweepRoot Seed = new( 7f, 9f );

	[Theory]
	[InlineData( false )]
	[InlineData( true )]
	public void InteriorStartProducesZeroOverOneEvenWithoutMotion( bool edge )
	{
		var root = Seed;
		var status = edge
			? TriangleSweepRoots.Edge( default, default, default, Vector3.UnitY, 1f, .5f, false, false, ref root )
			: TriangleSweepRoots.Point( default, default, default, .5f, ref root );
		Assert.Equal( SweepRootStatus.Hit, status );
		Assert.Equal( new SweepRoot( 0f, 1f ), root );
	}

	[Theory]
	[InlineData( false )]
	[InlineData( true )]
	public void RecedingMotionPreservesOutputAndReportsNotApproaching( bool edge )
	{
		var root = Seed;
		var status = edge
			? TriangleSweepRoots.Edge( default, -Vector3.UnitX, Vector3.UnitX, Vector3.UnitY, 1f, .5f, false, false, ref root )
			: TriangleSweepRoots.Point( default, -Vector3.UnitX, Vector3.UnitX, .5f, ref root );
		Assert.Equal( SweepRootStatus.NotApproaching, status );
		Assert.Equal( Seed, root );
	}

	[Theory]
	[InlineData( false )]
	[InlineData( true )]
	public void LateralMissPreservesOutputAndHasDistinctStatus( bool edge )
	{
		var root = Seed;
		var target = new Vector3( 1f, 0f, 2f );
		var status = edge
			? TriangleSweepRoots.Edge( default, Vector3.UnitX * 2f, target, Vector3.UnitY, 1f, .5f, false, false, ref root )
			: TriangleSweepRoots.Point( default, Vector3.UnitX * 2f, target, .5f, ref root );
		Assert.Equal( SweepRootStatus.Miss, status );
		Assert.Equal( Seed, root );
	}

	[Theory]
	[InlineData( false )]
	[InlineData( true )]
	public void ContactRootRetainsItsDenominator( bool edge )
	{
		var root = Seed;
		var status = edge
			? TriangleSweepRoots.Edge( default, Vector3.UnitX * 2f, Vector3.UnitX, Vector3.UnitY, 1f, .5f, false, false, ref root )
			: TriangleSweepRoots.Point( default, Vector3.UnitX * 2f, Vector3.UnitX, .5f, ref root );
		Assert.Equal( SweepRootStatus.Hit, status );
		Assert.Equal( new SweepRoot( 1f, 4f ), root );
	}

	[Theory]
	[InlineData( 1.5f, 0f )]
	[InlineData( 1f, .5f )]
	public void PointAcceptsAndEdgeRejectsContactAtSegmentEnd( float x, float z )
	{
		var pointRoot = Seed;
		var edgeRoot = Seed;
		var target = new Vector3( x, 0f, z );
		Assert.Equal( SweepRootStatus.Hit, TriangleSweepRoots.Point( default, Vector3.UnitX, target, .5f, ref pointRoot ) );
		Assert.Equal( new SweepRoot( 1f, 1f ), pointRoot );
		Assert.Equal( SweepRootStatus.Miss, TriangleSweepRoots.Edge( default, Vector3.UnitX, target,
			Vector3.UnitY, 1f, .5f, false, false, ref edgeRoot ) );
		Assert.Equal( Seed, edgeRoot );
	}

	[Fact]
	public void ExitModeChoosesTheLaterRoot()
	{
		var root = Seed;
		Assert.Equal( SweepRootStatus.Hit, TriangleSweepRoots.Edge( default, Vector3.UnitX * 2f, Vector3.UnitX,
			Vector3.UnitY, 1f, .5f, true, true, ref root ) );
		Assert.Equal( new SweepRoot( 3f, 4f ), root );
	}

	[Fact]
	public void ExitExactlyAtEndpointIsRejected()
	{
		var root = Seed;
		Assert.Equal( SweepRootStatus.Miss, TriangleSweepRoots.Edge( default, Vector3.UnitX, Vector3.UnitX * .5f,
			Vector3.UnitY, 1f, .5f, true, true, ref root ) );
		Assert.Equal( Seed, root );
	}

	[Fact]
	public void RequiredRootBypassesTheInteriorShortcut()
	{
		var root = Seed;
		Assert.Equal( SweepRootStatus.Hit, TriangleSweepRoots.Edge( default, Vector3.UnitX, Vector3.UnitX * .25f,
			Vector3.UnitY, 1f, .5f, false, true, ref root ) );
		Assert.Equal( new SweepRoot( -.25f, 1f ), root );
	}

	[Fact]
	public void NonunitEdgeRetainsItsScaleInBothRootTerms()
	{
		var root = Seed;
		Assert.Equal( SweepRootStatus.Hit, TriangleSweepRoots.Edge( default, Vector3.UnitX * 2f, Vector3.UnitX,
			Vector3.UnitY * 2f, 4f, .5f, false, false, ref root ) );
		Assert.Equal( new SweepRoot( 4f, 16f ), root );
	}

	[Fact]
	public void EnteringEdgeAtInitialContactRetainsNegativeZeroNumerator()
	{
		var root = Seed;
		Assert.Equal( SweepRootStatus.Hit, TriangleSweepRoots.Edge( default, Vector3.UnitX, Vector3.UnitX * .5f,
			Vector3.UnitY, 1f, .5f, false, false, ref root ) );
		Assert.Equal( 0x80000000u, BitConverter.SingleToUInt32Bits( root.Numerator ) );
	}
}
