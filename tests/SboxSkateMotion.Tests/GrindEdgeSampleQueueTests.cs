using System;
using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class GrindEdgeSampleQueueTests
{
	[Fact]
	public void FarSamplesUseDistanceAndKeepFirstTie()
	{
		var queue = Create();
		queue.SetSamples( [ Sample( 0, 3f ), Sample( 1, 2f ), Sample( 2, 2f ) ] );
		var output = default( GrindEdgeSample );
		foreach ( var expected in new[] { 1, 2, 0 } )
		{
			Assert.True( queue.Next( 1f, ref output ) );
			Assert.Equal( expected, output.EdgeIndex );
		}
		Assert.Equal( 0, queue.RemainingCount );
	}

	[Fact]
	public void CloseSelectionPreventsFartherDistanceReplacement()
	{
		var queue = Create();
		queue.SetSamples( [ Sample( 0, .5f, 3f ), Sample( 1, 1f, 0f ) ] );
		var output = default( GrindEdgeSample );
		Assert.True( queue.Next( 1f, ref output ) );
		Assert.Equal( 0, output.EdgeIndex );
		Assert.Equal( 1, queue.RemainingCount );
	}

	[Fact]
	public void NegativeCurveMarginCanReplaceAnEqualAngle()
	{
		var queue = Create( margin: -.1f );
		queue.SetSamples( [ Sample( 0 ), Sample( 1 ) ] );
		var output = default( GrindEdgeSample );
		Assert.True( queue.Next( 1f, ref output ) );
		Assert.Equal( 1, output.EdgeIndex );
	}

	[Fact]
	public void ZeroMarginRequiresStrictAngleImprovement()
	{
		var queue = Create();
		queue.SetSamples( [ Sample( 0, angle: 2f ), Sample( 1, angle: 1f ), Sample( 2, angle: 1f ) ] );
		var output = default( GrindEdgeSample );
		Assert.True( queue.Next( 1f, ref output ) );
		Assert.Equal( 1, output.EdgeIndex );
	}

	[Fact]
	public void HeightDifferenceIsRelativeToTheLastCloseSelection()
	{
		var curve = new GrindEdgeSelectionCurve( [ -4f, -3f, -2f, -1f, 0f, 1f, 2f, 3f ],
			[ 1f, 1f, 1f, 1f, 0f, -1f, -1f, -1f ] );
		var queue = new GrindEdgeSampleQueue( curve, Settings() );
		queue.SetSamples( [ Sample( 0, angle: 1f, height: 10f ), Sample( 1, angle: 1.5f, height: 11f ),
			Sample( 2, angle: .75f, height: 10f ) ] );
		var output = default( GrindEdgeSample );
		Assert.True( queue.Next( 1f, ref output ) );
		Assert.Equal( 1, output.EdgeIndex );
	}

	[Fact]
	public void NonemptyFailedScanClearsOnlyValidityAndRetainsSamples()
	{
		var queue = Create();
		queue.SetSamples( [ Sample( 0, distance: 10f ) ] );
		var before = Sample( 77, height: 8f );
		var output = before;
		Assert.False( queue.Next( 1f, ref output ) );
		Assert.Equal( before with { IsValid = false }, output );
		Assert.Equal( 1, queue.RemainingCount );
	}

	[Fact]
	public void EmptyScanPreservesValidityAndOtherOutput()
	{
		var queue = Create();
		var before = Sample( 99 );
		var output = before;
		Assert.False( queue.Next( 1f, ref output ) );
		Assert.Equal( before, output );
	}

	[Fact]
	public void SampleValidityDoesNotDetermineSelectionSuccess()
	{
		var queue = Create();
		var sample = Sample( 41 ) with { IsValid = false };
		queue.SetSamples( [ sample ] );
		var output = default( GrindEdgeSample );
		Assert.True( queue.Next( 1f, ref output ) );
		Assert.Equal( sample, output );
	}

	[Fact]
	public void ReplacementCopiesInputAndDiscardsOldPendingSamples()
	{
		var queue = Create();
		var input = new[] { Sample( 1 ), Sample( 2 ) };
		queue.SetSamples( input );
		input[ 0 ] = Sample( 999 );
		var output = default( GrindEdgeSample );
		Assert.True( queue.Next( 1f, ref output ) );
		Assert.Equal( 1, output.EdgeIndex );
		queue.SetSamples( [] );
		Assert.False( queue.Next( 1f, ref output ) );
		Assert.Equal( 0, queue.RemainingCount );
	}

	[Fact]
	public void CurveInterpolatesAndCopiesAllEightKnots()
	{
		var inputs = new[] { -3f, -2f, -1f, 0f, 1f, 2f, 3f, 4f };
		var outputs = new[] { 6f, 4f, 2f, 0f, -2f, -4f, -6f, -8f };
		var curve = new GrindEdgeSelectionCurve( inputs, outputs );
		Array.Fill( inputs, 0f );
		Array.Fill( outputs, 0f );
		Assert.Equal( 6f, curve.Evaluate( -10f ) );
		Assert.Equal( -8f, curve.Evaluate( 10f ) );
		Assert.Equal( -5f, curve.Evaluate( 2.5f ) );
		Assert.Equal( 0f, curve.Evaluate( 0f ) );
	}

	[Fact]
	public void CapacityFailurePreservesPendingQueue()
	{
		var queue = Create();
		queue.SetSamples( [ Sample( 5 ) ] );
		Assert.Throws<ArgumentException>( () => queue.SetSamples( new GrindEdgeSample[ 41 ] ) );
		Assert.Equal( 1, queue.RemainingCount );
		Assert.Throws<ArgumentException>( () => new GrindEdgeSelectionCurve( new float[ 7 ], new float[ 8 ] ) );
	}

	private static GrindEdgeSelectionSettings Settings() => new( 10f, -20f, 4f );

	private static GrindEdgeSample Sample( int index, float distance = .5f, float angle = .5f, float height = 0f ) => new(
		new Vector3( index, height, 2f ), new Vector3( 3f, 4f, 5f ), Vector3.UnitX, Vector3.UnitZ,
		distance, .25f, angle, index + 8, index, true );

	private static GrindEdgeSampleQueue Create( float margin = 0f )
	{
		var outputs = new float[ 8 ];
		Array.Fill( outputs, margin );
		return new( new GrindEdgeSelectionCurve( [ -3f, -2f, -1f, 0f, 1f, 2f, 3f, 4f ], outputs ), Settings() );
	}
}
