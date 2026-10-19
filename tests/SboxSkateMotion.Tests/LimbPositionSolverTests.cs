using System;
using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class LimbPositionSolverTests
{
	private static readonly Vector3 Hip = Vector3.Zero;
	private static readonly Vector3 Knee = new( .3f, -.4f, 0f );
	private static readonly Vector3 Ankle = new( 0f, -.8f, 0f );

	[Fact]
	public void ExhaustedBudgetRestoresBothAnimatedPositions()
	{
		var result = CreateSolver().Solve( Hip, Knee, Ankle, new Vector3( 0f, -2f, 0f ), 0f, 180f, false, 0 );
		Assert.Equal( 2, result.Status );
		Assert.Equal( Knee, result.Knee );
		Assert.Equal( Ankle, result.Target );
	}

	[Fact]
	public void DegenerateSegmentPreservesTheRequestedTarget()
	{
		var target = new Vector3( .1f, -.7f, 0f );
		var result = CreateSolver().Solve( Hip, Hip, Ankle, target, 0f, 180f, false, 2 );
		Assert.Equal( 2, result.Status );
		Assert.Equal( Hip, result.Knee );
		Assert.Equal( target, result.Target );
	}

	[Fact]
	public void MaximumHalfTurnAllowsExactFullExtension()
	{
		var result = CreateSolver().Solve( Hip, Knee, Ankle, new Vector3( 0f, -2f, 0f ), 0f, 180f, false, 2 );
		Assert.Equal( 1, result.Status );
		Assert.Equal( new Vector3( 0f, -.5f, 0f ), result.Knee );
		Assert.Equal( new Vector3( 0f, -1f, 0f ), result.Target );
	}

	[Fact]
	public void TargetAdjustmentConsumesAnAttempt()
	{
		var solver = CreateSolver();
		var target = new Vector3( 0f, -2f, 0f );
		var exhausted = solver.Solve( Hip, Knee, Ankle, target, 0f, 120f, false, 1 );
		var solved = solver.Solve( Hip, Knee, Ankle, target, 0f, 120f, false, 2 );
		Assert.Equal( 2, exhausted.Status );
		Assert.Equal( Ankle, exhausted.Target );
		Assert.Equal( 0, solved.Status );
		Assert.InRange( solved.Target.Length(), .8f, .9f );
		Assert.InRange( solved.Knee.Length(), .49999f, .50001f );
	}

	[Fact]
	public void SingularDirectionRemainsVisibleToTheCallingRig()
	{
		var result = CreateSolver().Solve( Hip, Knee, Ankle, Hip, 0f, 180f, false, 2 );
		Assert.Equal( 0, result.Status );
		Assert.True( float.IsNaN( result.Knee.X ) );
		Assert.True( float.IsNaN( result.Knee.Y ) );
		Assert.True( float.IsNaN( result.Knee.Z ) );
		Assert.Equal( Hip, result.Target );
	}

	[Fact]
	public void ReflectionKeepsTheAnimatedBendOnItsOwnSide()
	{
		var solver = CreateSolver();
		var target = new Vector3( 0f, -.7f, 0f );
		var left = solver.Solve( Hip, Knee, Ankle, target, 0f, 180f, false, 2 );
		var right = solver.Solve( Hip, new Vector3( -Knee.X, Knee.Y, Knee.Z ), Ankle, target, 0f, 180f, false, 2 );
		Assert.Equal( 0, left.Status );
		Assert.Equal( left.Status, right.Status );
		Assert.True( left.Knee.X > 0f );
		Assert.True( right.Knee.X < 0f );
		Assert.Equal( left.Knee.X, -right.Knee.X );
		Assert.Equal( left.Knee.Y, right.Knee.Y );
	}

	[Fact]
	public void FusedArithmeticPreservesCancellationResidual()
	{
		var residual = LimbSolverMath.MultiplyAdd( 1f + MathF.Pow( 2f, -23f ), 1f - MathF.Pow( 2f, -23f ), -1f );
		Assert.Equal( -MathF.Pow( 2f, -46f ), residual );
	}

	[Fact]
	public void PolynomialInputsAreCopied()
	{
		var cosine = new[] { -1f / 720f, 1f / 24f, -.5f };
		var sine = new[] { -1f / 5040f, 1f / 120f, -1f / 6f };
		var arcCosine = new[] { 0f, 0f, 0f, 0f, 0f, 0f, 0f, MathF.PI / 2f };
		var math = new LimbSolverMath( cosine, sine, arcCosine, 2f / MathF.PI, MathF.PI / 2f, 0f, MathF.PI );
		var expected = math.SinCos( .5f );
		var expectedArc = math.ArcCosine( .5f );
		Array.Fill( cosine, 100f );
		Array.Fill( sine, 100f );
		Array.Fill( arcCosine, 100f );
		Assert.Equal( expected, math.SinCos( .5f ) );
		Assert.Equal( expectedArc, math.ArcCosine( .5f ) );
	}

	[Fact]
	public void InvalidPolynomialShapeIsRejected()
	{
		Assert.Throws<ArgumentException>( () => new LimbSolverMath( new float[ 2 ], new float[ 3 ],
			new float[ 8 ], 1f, 1f, 0f, MathF.PI ) );
	}

	[Fact]
	public void InvalidDistanceScaleIsRejected()
	{
		Assert.Throws<ArgumentException>( () => new LimbPositionSolver( CreateMath(), 0f, 1f, 1f ) );
	}

	private static LimbPositionSolver CreateSolver() => new( CreateMath(), .0005f, MathF.PI / 180f, 180f / MathF.PI );

	private static LimbSolverMath CreateMath()
	{
		// Independently authored Taylor and square-root profiles exercise the configurable math contract.
		return new LimbSolverMath( new[] { -1f / 720f, 1f / 24f, -.5f },
			new[] { -1f / 5040f, 1f / 120f, -1f / 6f },
			new[] { 0f, 0f, 0f, 0f, 0f, 0f, 0f, MathF.PI / 2f },
			2f / MathF.PI, MathF.PI / 2f, 0f, MathF.PI );
	}
}
