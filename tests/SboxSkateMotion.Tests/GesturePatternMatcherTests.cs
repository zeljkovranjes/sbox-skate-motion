using SkateMotion.Core.Input;
using System;
using System.Numerics;
using Xunit;

namespace SboxSkateMotion.Tests;

public class GesturePatternMatcherTests
{
	private static readonly Vector2[] Path = [ new( 0f, 1f ), new( 1f, 0f ), new( 0f, -1f ) ];

	private static GesturePatternMatcher Create( byte gap = 10 )
		=> new( [ new GesturePattern( Path, 0.0625f ) ], gap );

	[Fact]
	public void FirstSamplePrimesHistoryWithoutAdvancingAPath()
	{
		var matcher = Create();
		Assert.False( matcher.Step( Path[ 0 ], 0, out _ ) );
		Assert.False( matcher.Step( Path[ 1 ], 0, out _ ) );
		Assert.False( matcher.Step( Path[ 2 ], 0, out _ ) );
		Assert.Equal( 0, matcher.Node( 0 ).Span );
	}

	[Fact]
	public void PathCompletesInOrderWithItsMeasuredError()
	{
		var matcher = Create();
		matcher.Step( Vector2.Zero, 0, out _ );
		matcher.Step( Path[ 0 ] + new Vector2( 0.125f, 0f ), 0, out _ );
		matcher.Step( Path[ 1 ], 0, out _ );
		Assert.True( matcher.Step( Path[ 2 ], 0, out var match ) );
		Assert.Equal( new GesturePatternMatch( 0, 1f, 0.015625f, 3f ), match );
	}

	[Fact]
	public void HoldingTheStartingCoordinateDoesNotAgeTheGesture()
	{
		var matcher = Create( 0 );
		matcher.Step( Vector2.Zero, 0, out _ );
		for ( var index = 0; index < 100; index++ )
			matcher.Step( Path[ 0 ], 0, out _ );
		matcher.Step( Path[ 1 ], 0, out _ );
		Assert.True( matcher.Step( Path[ 2 ], 0, out var match ) );
		Assert.Equal( 3f, match.Span );
	}

	[Fact]
	public void GapLimitIsInclusiveAndLaterMissResetsProgress()
	{
		var matcher = Create( 1 );
		matcher.Step( Vector2.Zero, 0, out _ );
		matcher.Step( Path[ 0 ], 0, out _ );
		matcher.Step( Path[ 1 ], 0, out _ );
		matcher.Step( new Vector2( -1f, 0f ), 0, out _ );
		Assert.Equal( 3, matcher.Node( 0 ).Span );
		matcher.Step( new Vector2( -1f, 0f ), 0, out _ );
		Assert.Equal( 0, matcher.Node( 0 ).Span );
		Assert.False( matcher.Step( Path[ 2 ], 0, out _ ) );
	}

	[Fact]
	public void IntermediateHitClearsConsecutiveMisses()
	{
		var matcher = Create( 1 );
		matcher.Step( Vector2.Zero, 0, out _ );
		matcher.Step( Path[ 0 ], 0, out _ );
		matcher.Step( new Vector2( -1f, 0f ), 0, out _ );
		matcher.Step( Path[ 1 ], 0, out _ );
		matcher.Step( new Vector2( -1f, 0f ), 0, out _ );
		Assert.True( matcher.Step( Path[ 2 ], 0, out var match ) );
		Assert.Equal( 5f, match.Span );
	}

	[Fact]
	public void EqualScoresKeepTheFirstPathInProfileOrder()
	{
		var pattern = new GesturePattern( Path, 0.0625f );
		var matcher = new GesturePatternMatcher( [ pattern, pattern ], 10 );
		matcher.Step( Vector2.Zero, 0, out _ );
		matcher.Step( Path[ 0 ], 0, out _ );
		matcher.Step( Path[ 1 ], 0, out _ );
		Assert.True( matcher.Step( Path[ 2 ], 0, out var match ) );
		Assert.Equal( 0, match.PatternIndex );
	}

	[Fact]
	public void CompletedGestureHoldsItsEndpointAndSkipsOneRecognitionUpdate()
	{
		var matcher = Create();
		matcher.Step( Vector2.Zero, 0, out _ );
		matcher.Step( Path[ 0 ], 0, out _ );
		matcher.Step( Path[ 1 ], 0, out _ );
		matcher.Step( Path[ 2 ], 0, out _ );
		Assert.Equal( -1, matcher.HeldPattern );
		Assert.False( matcher.Step( Path[ 2 ], 0, out _ ) );
		Assert.Equal( 0, matcher.HeldPattern );
		Assert.Equal( 0, matcher.Node( 0 ).Span );
		matcher.Step( Vector2.Zero, 0, out _ );
		Assert.Equal( -1, matcher.HeldPattern );
		matcher.Step( Path[ 0 ], 0, out _ );
		matcher.Step( Path[ 1 ], 0, out _ );
		Assert.True( matcher.Step( Path[ 2 ], 0, out _ ) );
	}

	[Theory]
	[InlineData( 0, 9, 0.1509434f )]
	[InlineData( 2, 3, 0.6666667f )]
	[InlineData( 2, 6, 0f )]
	public void TimingQualityUsesTheSelectedMode( int mode, int misses, float quality )
	{
		var matcher = Create( 63 );
		matcher.Step( Vector2.Zero, mode, out _ );
		matcher.Step( Path[ 0 ], mode, out _ );
		matcher.Step( Path[ 1 ], mode, out _ );
		for ( var index = 0; index < misses; index++ )
			matcher.Step( new Vector2( -1f, 0f ), mode, out _ );
		Assert.True( matcher.Step( Path[ 2 ], mode, out var match ) );
		Assert.Equal( quality, match.Quality, 6 );
	}

	[Fact]
	public void ToleranceIncludesItsExactBoundary()
	{
		var matcher = Create();
		matcher.Step( Vector2.Zero, 0, out _ );
		matcher.Step( Path[ 0 ] + new Vector2( 0.25f, 0f ), 0, out _ );
		matcher.Step( Path[ 1 ], 0, out _ );
		Assert.True( matcher.Step( Path[ 2 ], 0, out _ ) );
	}

	[Fact]
	public void ProfilePathsAreCopiedAndInvalidInputsAreRejected()
	{
		var points = new[] { Vector2.UnitY, -Vector2.UnitY };
		var pattern = new GesturePattern( points, 0f );
		points[ 0 ] = Vector2.Zero;
		Assert.Equal( Vector2.UnitY, pattern[ 0 ] );
		Assert.Throws<ArgumentOutOfRangeException>( () => new GesturePattern( [ Vector2.Zero ], 1f ) );
		Assert.Throws<ArgumentOutOfRangeException>( () => new GesturePattern( Path, float.NaN ) );
		Assert.Throws<ArgumentException>( () => Create().Step( new Vector2( float.NaN, 0f ), 0, out _ ) );
	}
}
