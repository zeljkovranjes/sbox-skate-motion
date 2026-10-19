using System;
using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class GrindSurfaceDirectionsTests
{
	[Theory]
	[InlineData( 0u )]
	[InlineData( 0x80000000u )]
	public void FailedQueryPreservesEveryVectorEvenWhenClassificationIsInvalid( uint flags )
	{
		var previous = new GrindSurfaceContact( new( 1f, 2f, 3f ), GrindSurfaceKind.Ledge, false,
			new( 4f, 5f, 6f ), new( 7f, 8f, 9f ), new( 10f, 11f, 12f ), new( 13f, 14f, 15f ) );
		var result = Create().Evaluate( false, Surface() with { Flags = flags }, default, default, previous );
		Assert.Equal( previous with { Kind = GrindSurfaceKind.Blocked, IsFallback = true }, result );
	}

	[Fact]
	public void InvalidSuccessfulQueryReplacesTheEntireContactFrame()
	{
		var result = Create().Evaluate( true, Surface() with { Flags = 0x80000000u }, default, default, default );
		Assert.Equal( new GrindSurfaceContact( Vector3.UnitY, GrindSurfaceKind.ThinRail, true,
			Vector3.UnitX, Vector3.UnitX, -Vector3.UnitX, Vector3.UnitX ), result );
		Assert.Equal( 0x80000000u, BitConverter.SingleToUInt32Bits( result.SecondDirection.Y ) );
	}

	[Theory]
	[InlineData( -1f, -1f )]
	[InlineData( 0f, 1f )]
	[InlineData( 1f, 1f )]
	public void ThinRailChoosesReferenceSideWithStrictPositiveTest( float referenceZ, float expectedZ )
	{
		var result = Create( 0f, MathF.PI ).Evaluate( true, Surface(), new( 0f, 0f, referenceZ ), default, default );
		Assert.Equal( new Vector3( 0f, 0f, expectedZ ), result.FirstDirection );
		Assert.Equal( result.FirstDirection, result.SecondDirection );
		Assert.Equal( -Vector3.UnitZ, result.Side );
	}

	[Fact]
	public void ThinRailReferenceIsRelativeToTheSuppliedPoint()
	{
		var evaluator = Create();
		var offset = new Vector3( 5f, 7f, 9f );
		Assert.Equal( evaluator.Evaluate( true, Surface(), Vector3.UnitZ, default, default ),
			evaluator.Evaluate( true, Surface(), offset + Vector3.UnitZ, offset, default ) );
	}

	[Theory]
	[InlineData( -1f, -1f )]
	[InlineData( 0f, 1f )]
	[InlineData( 1f, 1f )]
	public void WideRailUsesHighSideAndOpposesItsTwoDirections( float highZ, float secondZ )
	{
		var surface = Surface() with { Kind = GrindSurfaceKind.WideRail, HighSide = new( 0f, 0f, highZ ) };
		var result = Create( 0f, 0f ).Evaluate( true, surface, new( 0f, 0f, -highZ ), default, default );
		Assert.Equal( new Vector3( 0f, 0f, secondZ ), result.SecondDirection );
		Assert.Equal( -result.SecondDirection, result.FirstDirection );
		Assert.Equal( surface.HighSide, result.HighSide );
	}

	[Theory]
	[InlineData( 1, -1f )]
	[InlineData( 2, 1f )]
	[InlineData( 3, 1f )]
	public void LedgeAndBlockedSidesFollowTheSelectedNormal( int kind, float sideZ )
	{
		var surface = Surface() with { Kind = (GrindSurfaceKind)kind, FirstAngleLimit = 0f, SecondAngleLimit = 0f };
		var result = Create().Evaluate( true, surface, default, default, default );
		Assert.Equal( -Vector3.UnitY, result.Normal );
		Assert.Equal( new Vector3( 0f, 0f, sideZ ), result.Side );
		Assert.Equal( surface.Kind, result.Kind );
		Assert.False( result.IsFallback );
	}

	[Fact]
	public void ContactDirectionsRetainNonunitGeometry()
	{
		var surface = Surface() with { Up = Vector3.UnitY * 2f, Direction = Vector3.UnitX * 3f };
		var result = Create( 0f, MathF.PI ).Evaluate( true, surface, Vector3.UnitZ, default, default );
		Assert.Equal( Vector3.UnitZ * 6f, result.FirstDirection );
		Assert.Equal( -Vector3.UnitZ * 6f, result.Side );
		Assert.Equal( surface.Up, result.Normal );
	}

	private static GrindSurfaceClassification Surface() => new( default, default, default,
		Vector3.UnitY, Vector3.UnitX, -Vector3.UnitZ, 2f, 4f, GrindSurfaceKind.ThinRail, 0u, 0u, 0u );

	private static GrindSurfaceDirections Create( float first = .3f, float second = .7f )
	{
		var math = new LimbSolverMath( [ -1f / 720f, 1f / 24f, -.5f ],
			[ -1f / 5040f, 1f / 120f, -1f / 6f ], [ 0f, 0f, 0f, 0f, 0f, 0f, 0f, MathF.PI / 2f ],
			2f / MathF.PI, MathF.PI / 2f, 0f, MathF.PI );
		return new( math, first, second );
	}
}
