using System;
using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class GrindSurfaceClassifierTests
{
	[Theory]
	[InlineData( 0, (int)GrindSurfaceKind.ThinRail )]
	[InlineData( 1, (int)GrindSurfaceKind.WideRail )]
	[InlineData( 2, (int)GrindSurfaceKind.WideRail )]
	[InlineData( 3, (int)GrindSurfaceKind.Blocked )]
	[InlineData( 5, (int)GrindSurfaceKind.Ledge )]
	[InlineData( 10, (int)GrindSurfaceKind.Ledge )]
	[InlineData( 32, (int)GrindSurfaceKind.Blocked )]
	[InlineData( 37, (int)GrindSurfaceKind.Blocked )]
	public void OrderedHitPatternsDetermineCrossSectionKind( int mask, int expected )
	{
		var hits = new GrindSurfaceHit[ 7 ];
		for ( var i = 0; i < hits.Length; i++ )
			if ( (mask & (1 << i)) != 0 )
				hits[ i ] = Hit();
		Assert.Equal( (GrindSurfaceKind)expected, Create().Classify( Section(), hits, 0u ).Kind );
	}

	[Theory]
	[InlineData( .4999f, (int)GrindSurfaceKind.Ledge )]
	[InlineData( .5f, (int)GrindSurfaceKind.WideRail )]
	[InlineData( .5001f, (int)GrindSurfaceKind.WideRail )]
	public void LedgeFractionUsesStrictComparison( float fraction, int expected )
	{
		var hits = new GrindSurfaceHit[ 7 ];
		hits[ 0 ] = Hit();
		hits[ 2 ] = Hit() with { Fraction = fraction };
		Assert.Equal( (GrindSurfaceKind)expected, Create().Classify( Section(), hits, 0u ).Kind );
	}

	[Theory]
	[InlineData( 0f, -.3f, .76f, true )]
	[InlineData( -.1f, -.3f, 1f, false )]
	[InlineData( .1f, -.3f, 1f, false )]
	[InlineData( 0f, -.4f, 1f, false )]
	[InlineData( 0f, -.2f, 1f, false )]
	[InlineData( 0f, -.3f, .75f, false )]
	[InlineData( -.3f, 0f, 1f, true )]
	public void CurbBoundsAndNormalAlignmentAreStrict( float firstHeight, float secondHeight, float alignment, bool expected )
	{
		var hits = new GrindSurfaceHit[ 7 ];
		hits[ 0 ] = Hit();
		hits[ 2 ] = Hit() with { Position = new Vector3( 0f, firstHeight, -.5f ), Normal = Vector3.UnitY * alignment };
		hits[ 3 ] = Hit() with { Position = new Vector3( 0f, secondHeight, .5f ) };
		var result = Create().Classify( Section(), hits, 0u );
		Assert.Equal( expected, (result.Flags & 0x40000000u) != 0 );
		if ( expected )
		{
			Assert.Equal( 4u, result.AudioSurface );
			Assert.Equal( 2u, result.PhysicalSurface );
		}
	}

	[Fact]
	public void FlagsReplaceOwnedBitsButRetainBlockedHistory()
	{
		var result = Create().Classify( Section(), new GrindSurfaceHit[ 7 ], uint.MaxValue );
		Assert.Equal( 0x27ffffffu, result.Flags );
		Assert.Equal( GrindSurfaceKind.ThinRail, result.Kind );
	}

	[Theory]
	[InlineData( -1.001f, true )]
	[InlineData( -1f, true )]
	[InlineData( -.999f, false )]
	public void DownwardHitAtDropBoundaryRetainsDropFlag( float height, bool expected )
	{
		var hits = new GrindSurfaceHit[ 7 ];
		hits[ 6 ] = Hit() with { Position = Vector3.UnitY * height };
		var result = Create().Classify( Section(), hits, 0u );
		Assert.Equal( expected, (result.Flags & 0x04000000u) != 0 );
	}

	[Fact]
	public void OptionalProjectionSubtractsTheOriginalDirection()
	{
		var hits = new GrindSurfaceHit[ 7 ];
		hits[ 6 ] = Hit() with { Normal = new Vector3( 1f, 0f, 1f ), Position = new Vector3( 1f, 0f, .5f ) };
		var result = Create().Classify( Section() with { Direction = Vector3.UnitX * 2f }, hits, 0u );
		Assert.Equal( 0u, result.Flags & 0x08000000u );
	}

	[Fact]
	public void EqualHeightMaterialTieUsesSecondHitAndNearPairHasPriority()
	{
		var hits = new GrindSurfaceHit[ 7 ];
		hits[ 0 ] = Hit() with { Surface = (2u << 7) | 5u };
		hits[ 1 ] = Hit() with { Surface = (8u << 7) | 6u };
		hits[ 2 ] = Hit() with { Surface = 0u, Position = Vector3.UnitY * 100f };
		var result = Create().Classify( Section(), hits, 0u );
		Assert.Equal( 6u, result.AudioSurface );
		Assert.Equal( 8u, result.PhysicalSurface );
		Assert.NotEqual( 0u, result.Flags & 0x10000000u );
	}

	[Fact]
	public void ThinRailUsesCenterAudioWithFixedPhysicalMaterial()
	{
		var hits = new GrindSurfaceHit[ 7 ];
		hits[ 4 ] = Hit() with { Surface = (9u << 7) | 17u };
		var result = Create().Classify( Section(), hits, 0u );
		Assert.Equal( 17u, result.AudioSurface );
		Assert.Equal( 4u, result.PhysicalSurface );
		Assert.Equal( 0u, result.Flags & 0x10000000u );
	}

	[Fact]
	public void MissingFarHitsUseProbeEndpointsAndRightOnlyHitFlipsHighSide()
	{
		var hits = new GrindSurfaceHit[ 7 ];
		hits[ 1 ] = Hit();
		var section = Section();
		var result = Create().Classify( section, hits, 0u );
		Assert.Equal( section.FirstFarEnd, result.FirstFarPoint );
		Assert.Equal( section.SecondFarEnd, result.SecondFarPoint );
		Assert.Equal( Vector3.UnitZ, result.HighSide );
	}

	[Fact]
	public void CoincidentFarPointsProduceFiniteZeroAngleLimits()
	{
		var section = Section() with { FirstFarEnd = Vector3.Zero, SecondFarEnd = Vector3.Zero };
		var result = Create().Classify( section, new GrindSurfaceHit[ 7 ], 0u );
		Assert.Equal( MathF.PI / 2f + .25f, result.FirstAngleLimit );
		Assert.Equal( (MathF.PI * 2f - MathF.PI / 2f) - .25f, result.SecondAngleLimit );
	}

	[Fact]
	public void MissingProbeSlotsAreRejectedBeforeClassification()
	{
		Assert.Throws<ArgumentException>( () => Create().Classify( Section(), new GrindSurfaceHit[ 6 ], 0u ) );
	}

	private static GrindSurfaceHit Hit() => new( true, new Vector3( 0f, -.3f, .5f ), Vector3.UnitY, .25f, 0u );

	private static GrindSurfaceSection Section() => new( Vector3.Zero, Vector3.UnitY, Vector3.UnitX,
		new Vector3( 0f, -.5f, -.5f ), new Vector3( 0f, -.5f, .5f ) );

	private static GrindSurfaceClassifier Create()
	{
		var math = new LimbSolverMath( [ -1f / 720f, 1f / 24f, -.5f ],
			[ -1f / 5040f, 1f / 120f, -1f / 6f ], [ 0f, 0f, 0f, 0f, 0f, 0f, 0f, MathF.PI / 2f ],
			2f / MathF.PI, MathF.PI / 2f, 0f, MathF.PI );
		return new( math, new GrindSurfaceClassificationSettings( .5f, 1e-6f, .8f, .75f,
			-.1f, .1f, -.4f, -.2f, MathF.PI / 2f, MathF.PI * 2f, .25f ) );
	}
}
