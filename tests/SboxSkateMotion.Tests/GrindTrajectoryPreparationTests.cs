using System;
using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class GrindTrajectoryPreparationTests
{
	private static GrindEdgeSelectionCurve FlatCurve( float gain = 1f ) =>
		new( [ -3, -2, -1, 0, 1, 2, 3, 4 ], [ gain, gain, gain, gain, gain, gain, gain, gain ] );

	private static GrindTrajectoryPreparation Preparation( float gain = 1f ) =>
		new( FlatCurve( gain ), new GrindContactCorrection( .25f ) );

	private static GrindTrajectoryState Selected => new(
		new Vector4( 1, 2, 3, 4 ), new Vector4( 5, 6, 7, 8 ),
		new Vector4( 9, 10, 11, 12 ), new Vector4( 13, 14, 15, 16 ) );

	[Theory]
	[InlineData( int.MinValue )]
	[InlineData( -1 )]
	[InlineData( 0 )]
	public void NonpositiveSamplesCopyEntireSelectedState( int sample )
	{
		Assert.Equal( Selected, Preparation().Prepare( Selected, new Vector4( float.NaN ),
			new Vector4( float.NaN ), new Vector4( float.NaN ), new Vector4( float.NaN ), sample, 0f ) );
	}

	[Fact]
	public void AnchoringAndDesiredDisplacementBothContributeToVelocity()
	{
		var result = Preparation().Prepare( Selected, new Vector4( 0, 1, 0, 2 ),
			new Vector4( 2, 4, 6, 8 ), new Vector4( 2, 7, 6, 8 ), new Vector4( 0, 1, 0, 0 ), 2, 100f );
		Assert.Equal( new Vector4( 2, 4, 6, 8 ), result.Position );
		Assert.Equal( new Vector4( 3, 8, 1, 12 ), result.Velocity );
		Assert.Equal( Selected.Acceleration, result.Acceleration );
		Assert.Equal( Selected.Auxiliary, result.Auxiliary );
	}

	[Theory]
	[InlineData( 1f )]
	[InlineData( -1f )]
	public void ProjectionUsesAbsoluteSpatialDistanceAndIgnoresFourthLane( float sign )
	{
		var result = Preparation().Prepare( Selected, new Vector4( 0, 1, 0, 2 ), Selected.Position,
			Selected.Position + new Vector4( 0, 3, 0, 1000 ), new Vector4( 0, sign, 0, 1000 ), 4, 100f );
		Assert.Equal( new Vector4( 5, 9, 7, 14 ), result.Velocity );
	}

	[Theory]
	[InlineData( 1f, 7f, 10f )]
	[InlineData( -1f, 5f, 6f )]
	[InlineData( 0f, 6f, 8f )]
	public void SignedSpeedLimitScalesFourthLaneWithSpatialCorrection( float limit, float y, float w )
	{
		var result = Preparation().Prepare( Selected, new Vector4( 0, 1, 0, 2 ), Selected.Position,
			Selected.Position + new Vector4( 0, 3, 0, 0 ), new Vector4( 0, 1, 0, 0 ), 4, limit );
		Assert.Equal( new Vector4( 5, y, 7, w ), result.Velocity );
	}

	[Fact]
	public void CurveGainIsEvaluatedAtDirectionY()
	{
		var curve = new GrindEdgeSelectionCurve( [ -3, -2, -1, 0, 1, 2, 3, 4 ], [ -6, -4, -2, 0, 2, 4, 6, 8 ] );
		var result = new GrindTrajectoryPreparation( curve, new GrindContactCorrection( .25f ) ).Prepare(
			Selected, new Vector4( 3, .5f, -2, 4 ), Selected.Position,
			Selected.Position + new Vector4( 0, 2, 0, 0 ), new Vector4( 0, 1, 0, 0 ), 4, 100f );
		Assert.Equal( new Vector4( 11, 7, 3, 16 ), result.Velocity );
	}

	[Fact]
	public void PositionRetainsRoundedSubtractThenAdd()
	{
		var selected = Selected with { Position = new Vector4( 16777216f, 0, 0, 0 ) };
		var result = Preparation().Prepare( selected, Vector4.Zero, new Vector4( -1, 0, 0, 0 ),
			Vector4.Zero, Vector4.Zero, 4, 0f );
		Assert.Equal( Vector4.Zero, result.Position );
	}

	[Fact]
	public void OrderedCurveContinuesSearchingWithUnorderedLastKnot()
	{
		var curve = new GrindEdgeSelectionCurve( [ -3, -2, -1, 0, 1, 2, 3, float.NaN ], [ -6, -4, -2, 0, 2, 4, 6, 8 ] );
		Assert.Equal( 1f, curve.EvaluateOrdered( .5f ) );
		Assert.Equal( 8f, curve.Evaluate( .5f ) );
		Assert.Equal( -6f, curve.EvaluateOrdered( float.NaN ) );
	}

	[Theory]
	[InlineData( -10f, -6f )]
	[InlineData( 4f, 8f )]
	[InlineData( 10f, 8f )]
	public void OrderedCurveClampsFiniteEndpointsAndCopiesKnots( float value, float expected )
	{
		float[] inputs = [ -3, -2, -1, 0, 1, 2, 3, 4 ];
		float[] outputs = [ -6, -4, -2, 0, 2, 4, 6, 8 ];
		var curve = new GrindEdgeSelectionCurve( inputs, outputs );
		inputs[ 0 ] = -100;
		outputs[ 0 ] = 100;
		outputs[ 7 ] = -100;
		Assert.Equal( expected, curve.EvaluateOrdered( value ) );
	}
}
