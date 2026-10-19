using System;
using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class GrindSurfaceNormalTests
{
	[Fact]
	public void OpenHalfTurnIntervalPreservesSuppliedUpExactly()
	{
		var up = new Vector3( .2f, 3f, -.7f );
		Assert.Equal( up, Create().Evaluate( up, Vector3.Zero, 2f, 4f ) );
	}

	[Theory]
	[InlineData( 2f, MathF.PI )]
	[InlineData( MathF.PI, 4f )]
	[InlineData( MathF.PI, MathF.PI )]
	public void HalfTurnEndpointsDoNotUseTheOpenIntervalShortcut( float first, float second )
	{
		Assert.Equal( -Vector3.UnitY, Create().Evaluate( Vector3.UnitY, Vector3.Zero, first, second ) );
	}

	[Fact]
	public void ReversedSymmetricLimitsAverageToZeroRotation()
	{
		Assert.Equal( -Vector3.UnitY, Create().Evaluate( Vector3.UnitY, Vector3.UnitX, 2f, -2f ) );
	}

	[Fact]
	public void LimitsBelowHalfTurnChooseTheNearerUpperLimit()
	{
		var normal = Create().Evaluate( Vector3.UnitY, Vector3.UnitX, 0f, MathF.PI / 2f );
		Assert.InRange( Vector3.Distance( -Vector3.UnitZ, normal ), 0f, .00002f );
	}

	[Fact]
	public void LimitsAboveHalfTurnChooseTheNearerLowerLimit()
	{
		var normal = Create().Evaluate( Vector3.UnitY, Vector3.UnitX, MathF.PI * 1.5f, MathF.PI * 2f );
		Assert.InRange( Vector3.Distance( Vector3.UnitZ, normal ), 0f, .00002f );
	}

	[Fact]
	public void NonunitAxisIsNotNormalizedBeforeRotation()
	{
		var normal = Create().Evaluate( Vector3.UnitY, Vector3.UnitX * 2f, MathF.PI, MathF.PI );
		Assert.InRange( Vector3.Distance( Vector3.UnitY * 7f, normal ), 0f, .00001f );
	}

	[Fact]
	public void ZeroAxisRetainsTheNegatedInputOnTheRotationPath()
	{
		var up = new Vector3( 2f, -.5f, 1f );
		Assert.Equal( -up, Create().Evaluate( up, Vector3.Zero, 1f, 2f ) );
	}

	[Theory]
	[InlineData( 0f )]
	[InlineData( .5f )]
	[InlineData( -1f )]
	public void InactiveInvestigationHasExplicitInvalidStateAndFallbackGeometry( float reach )
	{
		var center = new Vector3( 2f, 3f, 4f );
		var result = GrindSurfaceClassifier.Inactive( center, reach );
		Assert.Equal( center, result.Center );
		Assert.Equal( center + Vector3.UnitX * reach, result.FirstFarPoint );
		Assert.Equal( center - Vector3.UnitX * reach, result.SecondFarPoint );
		Assert.Equal( Vector3.UnitZ, result.Up );
		Assert.Equal( Vector3.UnitZ, result.Direction );
		Assert.Equal( Vector3.UnitZ, result.HighSide );
		Assert.Equal( 0f, result.FirstAngleLimit );
		Assert.Equal( 0f, result.SecondAngleLimit );
		Assert.Equal( GrindSurfaceKind.Blocked, result.Kind );
		Assert.Equal( 3u, result.AudioSurface );
		Assert.Equal( 1u, result.PhysicalSurface );
		Assert.Equal( 0x80000000u, result.Flags );
	}

	private static GrindSurfaceNormal Create()
	{
		var math = new LimbSolverMath( [ -1f / 720f, 1f / 24f, -.5f ],
			[ -1f / 5040f, 1f / 120f, -1f / 6f ], [ 0f, 0f, 0f, 0f, 0f, 0f, 0f, MathF.PI / 2f ],
			2f / MathF.PI, MathF.PI / 2f, 0f, MathF.PI );
		return new( math );
	}
}
