using System;
using System.Numerics;
using SkateMotion.Core.Animation;
using SkateMotion.Core.Grinding;
using Xunit;

namespace SboxSkateMotion.Tests;

public class GrindEdgeContactSamplerTests
{
	[Fact]
	public void FallingTrajectoryProducesContactTimeAndPreservesProducerIndex()
	{
		var output = new GrindEdgeSample() with { EdgeIndex = 87 };
		Assert.True( Create().Sample( Motion(), Edge(), ref output ) );
		Assert.Equal( Vector3.Zero, output.Point );
		Assert.Equal( Vector3.Zero, output.TrajectoryPoint );
		Assert.Equal( Vector3.UnitX, output.Direction );
		Assert.Equal( Vector3.Zero, output.Tangent );
		Assert.Equal( 1f, output.Time );
		Assert.Equal( 8, output.SampleIndex );
		Assert.Equal( 87, output.EdgeIndex );
		Assert.True( output.IsValid );
	}

	[Fact]
	public void FailedCrossingPreservesEntirePreviousOutput()
	{
		var original = new GrindEdgeSample( Vector3.One, Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, 2f, 3f, 4f, 5, 6, true );
		var output = original;
		Assert.False( Create().Sample( Motion() with { Position = -Vector3.UnitY }, Edge(), ref output ) );
		Assert.Equal( original, output );
	}

	[Theory]
	[InlineData( -2f, true )]
	[InlineData( 0f, false )]
	[InlineData( 2f, false )]
	public void SingleRootMustBeInTheFuture( float verticalSpeed, bool expected )
	{
		var position = verticalSpeed == 0f ? Vector3.Zero : Vector3.UnitY;
		var motion = new GrindEdgeTrajectory( position, Vector3.UnitY * verticalSpeed, Vector3.UnitY * 2f, 0f );
		var output = default( GrindEdgeSample );
		Assert.Equal( expected, Create().Sample( motion, Edge(), ref output ) );
		if ( expected )
			Assert.Equal( 1f, output.Time );
	}

	[Fact]
	public void TwoRootPathCanRetainAPastCrossing()
	{
		var motion = Motion() with { Position = -Vector3.UnitY, Velocity = -Vector3.UnitY * 3f };
		var output = default( GrindEdgeSample );
		Assert.True( Create().Sample( motion, Edge(), ref output ) );
		Assert.InRange( output.Time, -.383f, -.381f );
		Assert.Equal( -3, output.SampleIndex );
	}

	[Fact]
	public void ZeroNormalAccelerationDoesNotUseALinearFallback()
	{
		var output = default( GrindEdgeSample );
		Assert.False( Create().Sample( Motion() with { Velocity = -Vector3.UnitY, Acceleration = Vector3.Zero }, Edge(), ref output ) );
	}

	[Theory]
	[InlineData( 4.49f, true )]
	[InlineData( 4.5f, false )]
	[InlineData( 4.51f, false )]
	public void EndpointExtensionUsesCappedEdgeVectorAndStrictBoundary( float x, bool expected )
	{
		var output = default( GrindEdgeSample );
		Assert.Equal( expected, Create().Sample( Motion() with { Position = new Vector3( x, 1f, 0f ) }, Edge(), ref output ) );
	}

	[Fact]
	public void LateralSeparationAndProjectedVelocityRemainSeparate()
	{
		var output = default( GrindEdgeSample );
		var motion = Motion() with { Position = new Vector3( 0f, 1f, .25f ), Velocity = Vector3.UnitX };
		Assert.True( Create().Sample( motion, Edge(), ref output ) );
		Assert.Equal( Vector3.UnitX, output.Point );
		Assert.Equal( new Vector3( 1f, 0f, .25f ), output.TrajectoryPoint );
		Assert.Equal( Vector3.UnitX, output.Tangent );
		Assert.Equal( .25f, output.Separation );
	}

	[Fact]
	public void RadiusAndContactOffsetMoveTheCrossingPlane()
	{
		var output = default( GrindEdgeSample );
		Assert.True( Create( offset: .25f ).Sample( Motion() with { Radius = .25f }, Edge(), ref output ) );
		Assert.InRange( output.Time, .70710f, .70711f );
		Assert.Equal( Vector3.Zero, output.Point );
		Assert.InRange( MathF.Abs( output.TrajectoryPoint.Y ), 0f, .000001f );
	}

	private static GrindEdgeTrajectory Motion() => new( Vector3.UnitY, Vector3.Zero, -Vector3.UnitY * 2f, 0f );

	private static RailSegment Edge() => new( -Vector3.UnitX * 4f, Vector3.UnitX * 4f );

	private static GrindEdgeContactSampler Create( float offset = 0f )
	{
		var math = new LimbSolverMath( [ -1f / 720f, 1f / 24f, -.5f ],
			[ -1f / 5040f, 1f / 120f, -1f / 6f ], [ 0f, 0f, 0f, 0f, 0f, 0f, 0f, MathF.PI / 2f ],
			2f / MathF.PI, MathF.PI / 2f, 0f, MathF.PI );
		return new( new LimbSignedAngle( math, 1e-8f, MathF.PI * 2f, 1f / (MathF.PI * 2f) ),
			new GrindEdgeSampleSettings( 2f, 1e-5f, .25f, offset, 8f, MathF.PI ) );
	}
}
