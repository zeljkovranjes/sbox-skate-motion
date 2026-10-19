using System;
using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class GrindCandidateNormalTests
{
	[Fact]
	public void TransverseVelocityTiltsTheDesiredNormal()
	{
		var normal = Create().Evaluate( Vector3.UnitY, Vector3.UnitX, new( 10, 0, 1 ), 1f, 180f );
		Near( Vector3.Normalize( new Vector3( 0, 1, -1 ) ), normal );
	}

	[Fact]
	public void AlongEdgeVelocityDoesNotTiltTheNormal()
	{
		Assert.Equal( Vector3.UnitY, Create().Evaluate( Vector3.UnitY, Vector3.UnitX,
			new Vector3( -50, 0, 0 ), 1f, 180f ) );
	}

	[Fact]
	public void ZeroTiltLimitKeepsTheContactNormalOnTheRotationPath()
	{
		Near( Vector3.UnitZ, Create().Evaluate( Vector3.UnitZ, Vector3.UnitX,
			Vector3.Zero, 0f, 0f ) );
	}

	[Theory]
	[InlineData( 30f, .5f )]
	[InlineData( -30f, -.5f )]
	public void SignedTiltLimitRotatesFromTheSuppliedNormal( float limit, float expectedY )
	{
		var result = Create().Evaluate( Vector3.UnitZ, Vector3.UnitX, Vector3.Zero, 0f, limit );
		Near( new Vector3( 0, expectedY, MathF.Sqrt( .75f ) ), result );
	}

	[Fact]
	public void ForwardProjectionUsesTheSuppliedNonunitDirection()
	{
		var direction = new Vector3( 1, 1, 0 );
		var expected = Vector3.Normalize( Vector3.UnitY - direction / MathF.Sqrt( 2f ) );
		Near( expected, Create().Evaluate( Vector3.UnitY, direction, Vector3.Zero, 0f, 180f ) );
	}

	[Fact]
	public void BackwardProjectionIsRetained()
	{
		Assert.Equal( Vector3.UnitY, Create().Evaluate( Vector3.UnitY,
			new Vector3( -1, -1, 0 ), Vector3.Zero, 0f, 180f ) );
	}

	[Fact]
	public void FullyRemovedNormalRemainsZero()
	{
		Assert.Equal( Vector3.Zero, Create().Evaluate( Vector3.UnitY, Vector3.UnitY,
			Vector3.Zero, 0f, 180f ) );
	}

	[Fact]
	public void VelocityCanCancelTheDesiredNormal()
	{
		Assert.Equal( Vector3.Zero, Create().Evaluate( Vector3.UnitY, Vector3.UnitX,
			Vector3.UnitY, 1f, 180f ) );
	}

	[Fact]
	public void DegenerateRotationAxisRetainsDesiredDirection()
	{
		Assert.Equal( Vector3.UnitY, Create().Evaluate( -Vector3.UnitY, Vector3.UnitX,
			Vector3.Zero, 0f, 0f ) );
	}

	[Theory]
	[InlineData( 0f )]
	[InlineData( 1f )]
	[InlineData( -1f )]
	public void ZeroAndVerticalEdgesUseUpFallback( float vertical )
	{
		Assert.Equal( Vector3.UnitY, Create().EdgeUp( new Vector3( 0, vertical, 0 ) ) );
	}

	[Fact]
	public void EdgeUpIsUnchangedWhenEndpointsAreReversed()
	{
		var solver = Create();
		var direction = new Vector3( 1, 1, 0 );
		Near( Vector3.Normalize( new Vector3( -1, 1, 0 ) ), solver.EdgeUp( direction ) );
		Assert.Equal( solver.EdgeUp( direction ), solver.EdgeUp( -direction ) );
	}

	[Fact]
	public void ReusingSolverDoesNotRetainThePriorTilt()
	{
		var solver = Create();
		solver.Evaluate( Vector3.UnitZ, Vector3.UnitX, Vector3.Zero, 1f, -30f );
		Assert.Equal( Vector3.UnitY, solver.Evaluate( Vector3.UnitY, Vector3.UnitX,
			Vector3.Zero, 0f, 180f ) );
	}

	private static void Near( Vector3 expected, Vector3 actual )
	{
		Assert.InRange( Vector3.Distance( expected, actual ), 0f, .00002f );
	}

	private static GrindCandidateNormal Create()
	{
		var math = new LimbSolverMath( [ -1f / 720f, 1f / 24f, -.5f ],
			[ -1f / 5040f, 1f / 120f, -1f / 6f ], [ 0f, 0f, 0f, 0f, 0f, 0f, 0f, MathF.PI / 2f ],
			2f / MathF.PI, MathF.PI / 2f, 0f, MathF.PI );
		return new( math, .000001f, .0000152587890625f, MathF.PI / 180f,
			2f * MathF.PI, 1f / (2f * MathF.PI) );
	}
}
