using System;
using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class LimbRotationSolverTests
{
	[Fact]
	public void TranslatedSegmentMovesItsOriginWithoutChangingAxes()
	{
		var source = new Vector3( 2f, 3f, -1f );
		var target = new Vector3( -4f, 1f, 5f );
		Assert.True( CreateSolver().TrySolve( source, source + Vector3.UnitY, Vector3.UnitZ,
			target, target + Vector3.UnitY, Vector3.UnitZ, out var result ) );
		Assert.Equal( target, result.Transform( source ) );
		Assert.Equal( target + Vector3.UnitY, result.Transform( source + Vector3.UnitY ) );
		Assert.Equal( LimbRigidTransform.Identity.Rotation, result.Rotation );
	}

	[Fact]
	public void QuarterTurnRotatesAllAxesAndMapsTheSegmentOrigin()
	{
		var source = new Vector3( 2f, 3f, -1f );
		var target = new Vector3( -4f, 1f, 5f );
		Assert.True( CreateSolver().TrySolve( source, source + Vector3.UnitY, Vector3.UnitZ,
			target, target - Vector3.UnitX, Vector3.UnitZ, out var result ) );
		Assert.Equal( target, result.Transform( source ) );
		Assert.Equal( Vector3.UnitY, result.Rotation.X );
		Assert.Equal( -Vector3.UnitX, result.Rotation.Y );
		Assert.Equal( Vector3.UnitZ, result.Rotation.Z );
	}

	[Fact]
	public void OpposedHintsPreserveTheRequestedTwist()
	{
		Assert.True( CreateSolver().TrySolve( Vector3.Zero, Vector3.UnitY, Vector3.UnitZ,
			Vector3.Zero, Vector3.UnitY, -Vector3.UnitZ, out var result ) );
		Assert.Equal( -Vector3.UnitX, result.Rotation.X );
		Assert.Equal( Vector3.UnitY, result.Rotation.Y );
		Assert.Equal( -Vector3.UnitZ, result.Rotation.Z );
	}

	[Fact]
	public void DifferentSegmentLengthsDoNotScaleTheRotation()
	{
		Assert.True( CreateSolver().TrySolve( Vector3.Zero, Vector3.UnitY * 2f, Vector3.UnitZ * 3f,
			Vector3.Zero, Vector3.UnitY * .5f, Vector3.UnitZ * 4f, out var result ) );
		Assert.Equal( LimbRigidTransform.Identity, result );
	}

	[Theory]
	[InlineData( true )]
	[InlineData( false )]
	public void DegenerateSourceOrTargetReturnsIdentity( bool sourceFails )
	{
		var sourceHint = sourceFails ? Vector3.UnitY : Vector3.UnitZ;
		var targetHint = sourceFails ? Vector3.UnitZ : Vector3.UnitY;
		Assert.False( CreateSolver().TrySolve( Vector3.Zero, Vector3.UnitY, sourceHint,
			Vector3.One, Vector3.One + Vector3.UnitY, targetHint, out var result ) );
		Assert.Equal( LimbRigidTransform.Identity, result );
	}

	[Fact]
	public void NonfiniteSolvedPositionIsRejected()
	{
		Assert.False( CreateSolver().TrySolve( Vector3.Zero, Vector3.UnitY, Vector3.UnitZ,
			Vector3.Zero, new Vector3( float.NaN, 1f, 0f ), Vector3.UnitZ, out var result ) );
		Assert.Equal( LimbRigidTransform.Identity, result );
	}

	[Fact]
	public void AxisThresholdUsesStrictGreaterThan()
	{
		var solver = new LimbRotationSolver( .125f, .001f );
		Assert.False( solver.TrySolve( Vector3.Zero, Vector3.UnitY, Vector3.UnitZ * .125f,
			Vector3.Zero, Vector3.UnitY, Vector3.UnitZ, out _ ) );
		Assert.True( solver.TrySolve( Vector3.Zero, Vector3.UnitY, Vector3.UnitZ * .25f,
			Vector3.Zero, Vector3.UnitY, Vector3.UnitZ, out _ ) );
	}

	[Fact]
	public void FrameProductThresholdCanRejectOtherwiseValidAxes()
	{
		Assert.False( new LimbRotationSolver( .001f, 1f ).TrySolve(
			Vector3.Zero, Vector3.UnitY, Vector3.UnitZ,
			Vector3.Zero, Vector3.UnitY, Vector3.UnitZ, out var result ) );
		Assert.Equal( LimbRigidTransform.Identity, result );
	}

	[Fact]
	public void InvalidThresholdsAreRejected()
	{
		Assert.Throws<ArgumentException>( () => new LimbRotationSolver( 0f, .001f ) );
		Assert.Throws<ArgumentException>( () => new LimbRotationSolver( .001f, float.NaN ) );
	}

	private static LimbRotationSolver CreateSolver() => new( .0001f, .001f );
}
