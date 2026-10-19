using System;
using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class GrindCandidateBiasTests
{
	private static GrindCandidateBiasInput Input( Vector4 time, int sample = 0 ) =>
		new( new( new Vector4( 0, 2, 0, 0 ), Vector4.Zero, Vector4.Zero, Vector4.Zero ), time, sample );

	[Theory]
	[InlineData( short.MinValue )]
	[InlineData( -1 )]
	[InlineData( 0 )]
	public void NonpositiveCountsPreserveScoresAndBias( short count )
	{
		float[] scores = [ 7 ];
		float[] biases = [ 3 ];
		new GrindCandidateBias( 0, 10 ).Apply( [ Input( Vector4.One ) ], count, 0f, scores, biases );
		Assert.Equal( 7f, scores[ 0 ] );
		Assert.Equal( 3f, biases[ 0 ] );
	}

	[Theory]
	[InlineData( -1, 5f, 0f )]
	[InlineData( 0, 15f, 10f )]
	[InlineData( 1, 15f, 10f )]
	public void MinimumSampleIndexIsInclusive( int sample, float expectedScore, float expectedBias )
	{
		float[] scores = [ 5 ];
		float[] biases = [ 0 ];
		new GrindCandidateBias( 0, 10 ).Apply( [ Input( Vector4.One, sample ) ], 1, 0f, scores, biases );
		Assert.Equal( expectedScore, scores[ 0 ] );
		Assert.Equal( expectedBias, biases[ 0 ] );
	}

	[Theory]
	[InlineData( 0 )]
	[InlineData( 1 )]
	[InlineData( 2 )]
	[InlineData( 3 )]
	public void NegativeTimeInAnyLanePreservesExistingBiasAndScore( int lane )
	{
		var time = Vector4.One;
		time[ lane ] = -1;
		float[] scores = [ 5 ];
		float[] biases = [ 3 ];
		new GrindCandidateBias( 0, 10 ).Apply( [ Input( time ) ], 1, 0f, scores, biases );
		Assert.Equal( 5f, scores[ 0 ] );
		Assert.Equal( 3f, biases[ 0 ] );
	}

	[Theory]
	[InlineData( 1.999f, 15f, 10f )]
	[InlineData( 2f, 5f, 0f )]
	[InlineData( 2.001f, 5f, 0f )]
	public void HeightThresholdIsStrict( float height, float expectedScore, float expectedBias )
	{
		float[] scores = [ 5 ];
		float[] biases = [ 0 ];
		new GrindCandidateBias( 0, 10 ).Apply( [ Input( Vector4.Zero ) ], 1, height, scores, biases );
		Assert.Equal( expectedScore, scores[ 0 ] );
		Assert.Equal( expectedBias, biases[ 0 ] );
	}

	[Fact]
	public void GeneratedBiasPersistsAndAccumulatesAcrossCalls()
	{
		var pass = new GrindCandidateBias( 0, 10 );
		var input = new[] { Input( Vector4.One ) };
		float[] scores = [ 5 ];
		float[] biases = [ 0 ];
		pass.Apply( input, 1, 0f, scores, biases );
		pass.Apply( input, 1, 100f, scores, biases );
		Assert.Equal( 25f, scores[ 0 ] );
		Assert.Equal( 10f, biases[ 0 ] );
	}

	[Theory]
	[InlineData( -3f, 2f )]
	[InlineData( 3f, 8f )]
	public void ExistingSignedBiasIsRetainedRegardlessOfHeight( float bias, float expected )
	{
		float[] scores = [ 5 ];
		float[] biases = [ bias ];
		new GrindCandidateBias( 0, 10 ).Apply( [ Input( Vector4.One ) ], 1, 100f, scores, biases );
		Assert.Equal( expected, scores[ 0 ] );
		Assert.Equal( bias, biases[ 0 ] );
	}

	[Fact]
	public void PredictionUsesTimeXWithVelocityAndHalfAcceleration()
	{
		var state = new GrindTrajectoryState( new Vector4( 0, 1, 0, 0 ),
			new Vector4( 0, 2, 0, 0 ), new Vector4( 0, 4, 0, 0 ), Vector4.Zero );
		float[] scores = [ 5 ];
		float[] biases = [ 0 ];
		new GrindCandidateBias( 0, 10 ).Apply( [ new( state, new Vector4( 2, 100, 0, 0 ), 0 ) ], 1, 12f, scores, biases );
		Assert.Equal( 15f, scores[ 0 ] );
	}

	[Fact]
	public void CountLimitsWhichScoresAndBiasesAreUpdated()
	{
		float[] scores = [ 5, 6 ];
		float[] biases = [ 0, 3 ];
		new GrindCandidateBias( 0, 10 ).Apply( [ Input( Vector4.Zero ), Input( Vector4.Zero ) ], 1, 0f, scores, biases );
		Assert.Equal( new float[] { 15, 6 }, scores );
		Assert.Equal( new float[] { 10, 3 }, biases );
	}

	[Fact]
	public void UnorderedTimeLaneRejectsWithoutChangingState()
	{
		float[] scores = [ 5 ];
		float[] biases = [ 3 ];
		new GrindCandidateBias( 0, 10 ).Apply( [ Input( new Vector4( 1, 1, 1, float.NaN ) ) ], 1, 0f, scores, biases );
		Assert.Equal( 5f, scores[ 0 ] );
		Assert.Equal( 3f, biases[ 0 ] );
	}

	[Fact]
	public void UnusedNegativeZeroBiasKeepsItsSign()
	{
		float[] scores = [ 5 ];
		float[] biases = [ -0f ];
		new GrindCandidateBias( 0, 10 ).Apply( [ Input( Vector4.Zero ) ], 1, 2f, scores, biases );
		Assert.Equal( int.MinValue, BitConverter.SingleToInt32Bits( biases[ 0 ] ) );
	}
}
