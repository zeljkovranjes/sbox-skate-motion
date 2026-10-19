using System;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class PoseWeightNormalizerTests
{
	[Fact]
	public void ReturnsTheTotalAndLeavesRawWeightsUnchanged()
	{
		var raw = new[] { 2f, 3f, 5f };
		var normalized = new float[ 3 ];
		Assert.Equal( 10f, PoseWeightNormalizer.Normalize( raw, normalized ) );
		Assert.Equal( new[] { .2f, .3f, .5f }, normalized );
		Assert.Equal( new[] { 2f, 3f, 5f }, raw );
	}

	[Fact]
	public void SignedWeightsAreNotClamped()
	{
		var normalized = new float[ 2 ];
		Assert.Equal( 2f, PoseWeightNormalizer.Normalize( new[] { -1f, 3f }, normalized ) );
		Assert.Equal( new[] { -.5f, 1.5f }, normalized );
	}

	[Fact]
	public void ZeroWeightsRetainUndefinedNormalization()
	{
		var normalized = new float[ 2 ];
		Assert.Equal( 0f, PoseWeightNormalizer.Normalize( new[] { 0f, 0f }, normalized ) );
		Assert.All( normalized, value => Assert.True( float.IsNaN( value ) ) );
	}

	[Fact]
	public void CancelledTotalProducesSignedInfinitiesAndUndefinedZero()
	{
		var normalized = new float[ 3 ];
		Assert.Equal( 0f, PoseWeightNormalizer.Normalize( new[] { 1f, -1f, 0f }, normalized ) );
		Assert.Equal( float.PositiveInfinity, normalized[ 0 ] );
		Assert.Equal( float.NegativeInfinity, normalized[ 1 ] );
		Assert.True( float.IsNaN( normalized[ 2 ] ) );
	}

	[Fact]
	public void TotalAccumulatesInInputOrder()
	{
		var raw = new[] { 16777216f, 1f, -16777216f, 1f };
		var normalized = new float[ 4 ];
		Assert.Equal( 1f, PoseWeightNormalizer.Normalize( raw, normalized ) );
		Assert.Equal( raw, normalized );
	}

	[Fact]
	public void MultiplicationUsesOneRoundedReciprocal()
	{
		var normalized = new float[ 2 ];
		PoseWeightNormalizer.Normalize( new[] { 3f, 4f }, normalized );
		Assert.Equal( 0x3edb6db8u, BitConverter.SingleToUInt32Bits( normalized[ 0 ] ) );
		Assert.NotEqual( 3f / 7f, normalized[ 0 ] );
	}

	[Fact]
	public void OverflowingTotalProducesZeroForFiniteWeights()
	{
		var normalized = new float[ 2 ];
		Assert.Equal( float.PositiveInfinity, PoseWeightNormalizer.Normalize( new[] { 3e38f, 3e38f }, normalized ) );
		Assert.Equal( new[] { 0f, 0f }, normalized );
	}

	[Fact]
	public void InPlaceNormalizationReadsTheTotalBeforeWriting()
	{
		var weights = new[] { 2f, 6f };
		Assert.Equal( 8f, PoseWeightNormalizer.Normalize( weights, weights ) );
		Assert.Equal( new[] { .25f, .75f }, weights );
	}

	[Theory]
	[InlineData( 0, 0 )]
	[InlineData( 1, 2 )]
	[InlineData( 2, 1 )]
	public void InvalidCountsAreRejectedBeforeOutputChanges( int rawCount, int outputCount )
	{
		var output = new float[ outputCount ];
		Array.Fill( output, 9f );
		Assert.Throws<ArgumentException>( () => PoseWeightNormalizer.Normalize( new float[ rawCount ], output ) );
		Assert.All( output, value => Assert.Equal( 9f, value ) );
	}
}
