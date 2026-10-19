using System;
using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class LimbChainSolverTests
{
	private static readonly Vector3 Knee = new( .3f, -.4f, 0f );
	private static readonly Vector3 Ankle = new( 0f, -.8f, 0f );
	private static readonly LimbRigidTransform Upper = LimbRigidTransform.Identity;
	private static readonly LimbRigidTransform Lower = new( LimbRigidTransform.Identity.Rotation, Knee );

	[Fact]
	public void PositionFailureLeavesBonesAndClearsResidual()
	{
		var result = CreateSolver().Solve( Vector3.Zero, Vector3.Zero, Ankle, Vector3.One,
			Upper, Lower, 0f, 180f, 10f );
		Assert.False( result.Applied );
		Assert.Equal( Upper, result.Upper );
		Assert.Equal( Lower, result.Lower );
		Assert.Equal( Vector3.Zero, result.Residual );
	}

	[Fact]
	public void FullExtensionReportsTheUnreachedDisplacement()
	{
		var result = CreateSolver().Solve( Vector3.Zero, Knee, Ankle, new Vector3( 0f, -1.2f, 0f ),
			Upper, Lower, 0f, 180f, 10f );
		Assert.True( result.Applied );
		Assert.Equal( new Vector3( 0f, 1f, 0f ), result.Residual );
		Assert.InRange( Vector3.Distance( new Vector3( 0f, -.5f, 0f ), result.Lower.Translation ), 0f, 1e-7f );
		Assert.Equal( Vector3.Zero, result.Upper.Translation );
	}

	[Fact]
	public void ReachLimitContributesToResidualAgainstTheOriginalRequest()
	{
		var result = CreateSolver().Solve( Vector3.Zero, Knee, Ankle, new Vector3( 0f, -1.2f, 0f ),
			Upper, Lower, 0f, 180f, .5f );
		Assert.True( result.Applied );
		Assert.Equal( new Vector3( 0f, 1.5f, 0f ), result.Residual );
	}

	[Fact]
	public void FailedFramesKeepTheReachAdjustmentResidual()
	{
		var result = CreateSolver().Solve( Vector3.Zero, new Vector3( 0f, -.5f, 0f ),
			-Vector3.UnitY, -Vector3.UnitY, Upper, Lower, 0f, 180f, .7f );
		Assert.False( result.Applied );
		Assert.Equal( Upper, result.Upper );
		Assert.Equal( Lower, result.Lower );
		Assert.InRange( result.Residual.Y, 1.299999f, 1.300001f );
	}

	[Fact]
	public void MatrixDegeneracyDoesNotChangeTheSolveStatus()
	{
		var collapsed = new LimbRigidTransform( new LimbFrame( Vector3.Zero, Vector3.UnitY, Vector3.UnitZ ), Vector3.Zero );
		var result = CreateSolver().Solve( Vector3.Zero, Knee, Ankle, new Vector3( 0f, -1.2f, 0f ),
			collapsed, Lower, 0f, 180f, 10f );
		Assert.True( result.Applied );
		Assert.True( float.IsNaN( result.Upper.Rotation.X.X ) );
		Assert.Equal( new Vector3( 0f, 1f, 0f ), result.Residual );
	}

	[Fact]
	public void InvalidPlaneThresholdIsRejected()
	{
		Assert.Throws<ArgumentException>( () => new LimbChainSolver( CreatePositions(),
			new LimbRotationSolver( .0001f, .001f ), 0f ) );
	}

	private static LimbChainSolver CreateSolver() => new( CreatePositions(), new LimbRotationSolver( .0001f, .001f ), .0001f );

	private static LimbPositionSolver CreatePositions()
	{
		var math = new LimbSolverMath( new[] { -1f / 720f, 1f / 24f, -.5f },
			new[] { -1f / 5040f, 1f / 120f, -1f / 6f },
			new[] { 0f, 0f, 0f, 0f, 0f, 0f, 0f, MathF.PI / 2f },
			2f / MathF.PI, MathF.PI / 2f, 0f, MathF.PI );
		return new LimbPositionSolver( math, .0005f, MathF.PI / 180f, 180f / MathF.PI );
	}
}
