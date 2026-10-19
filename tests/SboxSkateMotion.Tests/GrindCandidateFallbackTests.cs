using System;
using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class GrindCandidateFallbackTests
{
	private static GrindCandidateFallback Create( float minimumLength = .01f, float maximumSquared = 100f,
		float missingDistance = 6f ) => new( minimumLength, maximumSquared, missingDistance,
		new( [ 0, 1, 2, 3, 4, 5, 6, 7 ], [ 0, 1, 2, 3, 4, 5, 6, 7 ] ) );

	private static GrindEdgeRecord Edge( Vector3 a, Vector3 b ) => new( new( a, b ), 0, 0 );

	[Fact]
	public void MissingEdgesUseConfiguredDistanceWithoutInspectingRegistry()
	{
		Assert.Equal( 6f, Create().Evaluate( false, Vector4.Zero, Vector4.Zero, null!, -1 ) );
	}

	[Fact]
	public void EmptyFoundRegistryUsesSquareRootOfInitialMinimum()
	{
		Assert.Equal( 3f, Create( maximumSquared: 9f ).Evaluate( true, Vector4.Zero, Vector4.Zero, [], 0 ) );
	}

	[Theory]
	[InlineData( -10f )]
	[InlineData( .5f )]
	[InlineData( 10f )]
	public void ProjectionExtendsBeyondBothEndpoints( float x )
	{
		Assert.Equal( 1f, Create().Evaluate( true, new( x, 1, 0, 0 ), Vector4.Zero,
			[ Edge( Vector3.Zero, Vector3.UnitX ) ], 1 ) );
	}

	[Theory]
	[InlineData( 0 )]
	[InlineData( 1 )]
	[InlineData( 2 )]
	[InlineData( 3 )]
	public void EachNegativeTimeLaneSelectsOrigin( int lane )
	{
		var time = new Vector4( 1 );
		time[ lane ] = -1;
		Assert.Equal( 2f, Create().Evaluate( true, new( .5f, 2, 0, 0 ), time,
			[ Edge( new( 0, 2, 0 ), new( 1, 2, 0 ) ) ], 1 ) );
	}

	[Fact]
	public void ZeroLengthEdgeUsesPointDistance()
	{
		Assert.Equal( 5f, Create().Evaluate( true, new( 3, 4, 0, 0 ), Vector4.Zero,
			[ Edge( Vector3.Zero, Vector3.Zero ) ], 1 ) );
	}

	[Fact]
	public void ShortEdgeChecksBothEndpoints()
	{
		Assert.Equal( 0f, Create( minimumLength: 1f ).Evaluate( true, new( .5f, 0, 0, 0 ), Vector4.Zero,
			[ Edge( Vector3.Zero, new( .5f, 0, 0 ) ) ], 1 ) );
	}

	[Fact]
	public void LengthEqualToThresholdDoesNotProject()
	{
		Assert.Equal( 2f, Create( minimumLength: 1f ).Evaluate( true, new( 3, 0, 0, 0 ), Vector4.Zero,
			[ Edge( Vector3.Zero, Vector3.UnitX ) ], 1 ) );
	}

	[Fact]
	public void OnlyActiveRegistryEntriesContribute()
	{
		var edges = new[] { Edge( new( 0, 3, 0 ), new( 1, 3, 0 ) ), Edge( Vector3.Zero, Vector3.UnitX ) };
		Assert.Equal( 3f, Create().Evaluate( true, Vector4.Zero, Vector4.Zero, edges, 1 ) );
		Assert.Equal( 0f, Create().Evaluate( true, Vector4.Zero, Vector4.Zero, edges, 2 ) );
	}

	[Fact]
	public void InitialMinimumCapsDistance()
	{
		Assert.Equal( 2f, Create( maximumSquared: 4f ).Evaluate( true, new( 0, 50, 0, 0 ), Vector4.Zero,
			[ Edge( Vector3.Zero, Vector3.UnitX ) ], 1 ) );
	}

	[Fact]
	public void PointFourthComponentDoesNotAffectSpatialDistance()
	{
		Assert.Equal( 2f, Create().Evaluate( true, new( .5f, 2, 0, 900f ), Vector4.Zero,
			[ Edge( Vector3.Zero, Vector3.UnitX ) ], 1 ) );
	}

	[Fact]
	public void DistanceBelowCurveRangeUsesFirstOutput()
	{
		var pass = new GrindCandidateFallback( .01f, 100f, 6f,
			new( [ 1, 2, 3, 4, 5, 6, 7, 8 ], [ 10, 20, 30, 40, 50, 60, 70, 80 ] ) );
		Assert.Equal( 10f, pass.Evaluate( true, Vector4.Zero, Vector4.Zero,
			[ Edge( Vector3.Zero, Vector3.UnitX ) ], 1 ) );
	}

	[Theory]
	[InlineData( -1 )]
	[InlineData( 2 )]
	public void InvalidActiveCountIsRejected( int count )
	{
		Assert.Throws<ArgumentOutOfRangeException>( () => Create().Evaluate( true, Vector4.Zero, Vector4.Zero,
			[ Edge( Vector3.Zero, Vector3.UnitX ) ], count ) );
	}
}
