using System.Numerics;
using SkateMotion.Core.Animation;
using SkateMotion.Core.Physics;
using Xunit;

namespace SboxSkateMotion.Tests;

public class BoardRetrievalInitializerTests
{
	[Fact]
	public void OrdinaryNearbyRetrievalKeepsTheBoardAndUsesDistanceDuration()
	{
		var result = Create().Begin( 2, At( 2f ), new Vector3( 8f, 0f, 0f ), At( 0f ), Vector3.UnitY, false );
		Assert.Equal( At( 2f ), result.Source );
		Assert.Equal( 2f, result.Duration );
		Assert.False( result.Relocate );
	}

	[Theory]
	[InlineData( 4f, false )]
	[InlineData( 5f, true )]
	public void DistanceLimitUsesAStrictComparison( float position, bool relocate )
	{
		var result = Create().Begin( 2, At( position ), Vector3.Zero, At( 0f ), Vector3.UnitY, false );
		Assert.Equal( At( 4f ), result.Source );
		Assert.Equal( 4f, result.Duration );
		Assert.Equal( relocate, result.Relocate );
	}

	[Fact]
	public void RetainedPositionStateExpandsToTheLimitUsingThePreviousPosition()
	{
		var result = Create().Begin( 3, At( 9f ), new Vector3( -1f, 0f, 0f ), At( 0f ), Vector3.UnitY, false );
		Assert.Equal( At( -4f ), result.Source );
		Assert.Equal( 4f, result.Duration );
		Assert.True( result.Relocate );
	}

	[Fact]
	public void CoincidentRetainedPositionUsesTheSuppliedFallbackDirection()
	{
		var result = Create().Begin( 3, At( 9f ), Vector3.Zero, At( 0f ), Vector3.UnitY, false );
		Assert.Equal( new Vector3( 0f, 4f, 0f ), result.Source.Translation );
		Assert.True( result.Relocate );
	}

	[Fact]
	public void MountingUsesItsOwnDistanceAndDurationCaps()
	{
		var result = Create().Begin( 2, At( 9f ), Vector3.Zero, At( 0f ), Vector3.UnitY, true );
		Assert.Equal( At( 2f ), result.Source );
		Assert.Equal( .5f, result.Duration );
		Assert.True( result.Relocate );
	}

	private static LimbRigidTransform At( float x ) => LimbRigidTransform.Identity with
	{
		Translation = new Vector3( x, 0f, 0f )
	};

	private static BoardRetrievalInitializer Create() => new(
		new BoardCorrectionCurve( new[] { 0f, 1f, 2f, 3f, 4f, 5f, 6f, 7f },
			new[] { 0f, 1f, 2f, 3f, 4f, 5f, 6f, 7f } ), .125f, 4f, 2f, .5f );
}
