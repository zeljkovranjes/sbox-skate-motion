using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class PlacementLandingTests
{
	private const float Step = 1f / 90f;

	[Fact]
	public void AHardLandingOnFootGivesAtTheKneesAndSettles()
	{
		// Falling at 420 in/s in the air, then standing: the shown body does not stop dead in one frame, dips a little
		// and is back on its placement within half a second.
		var smoother = new PlacementSmoother();
		for ( var frame = 20; frame >= 0; frame-- )
			smoother.Update( new Vector3( 0f, 0f, 420f * Step * frame ), false, false, false, Vector3.UnitZ, Step );
		var before = smoother.Update( new Vector3( 0f, 0f, 0f ), false, true, false, Vector3.UnitZ, Step ).Z;
		var after = smoother.Update( new Vector3( 0f, 0f, 0f ), false, true, false, Vector3.UnitZ, Step ).Z;
		Assert.True( before < 0f && before > -3f, $"dips {before:0.00} in as it lands" );
		Assert.True( after < before, $"still moving down the frame after landing ({before:0.00} then {after:0.00})" );
		var shown = 0f;
		for ( var frame = 0; frame < 45; frame++ )
			shown = smoother.Update( new Vector3( 0f, 0f, 0f ), false, true, false, Vector3.UnitZ, Step ).Z;
		Assert.True( MathF.Abs( shown ) < 0.05f, $"{shown:0.000} in off after half a second" );
	}

	[Fact]
	public void AnOrdinaryLandingStopsWithTheFeet()
	{
		// Falling at 150 in/s (a short drop) stops at once, as before.
		var smoother = new PlacementSmoother();
		for ( var frame = 10; frame >= 0; frame-- )
			smoother.Update( new Vector3( 0f, 0f, 150f * Step * frame ), false, false, false, Vector3.UnitZ, Step );
		Assert.Equal( 0f, smoother.Update( Vector3.Zero, false, true, false, Vector3.UnitZ, Step ).Z, 3 );
		Assert.Equal( 0f, smoother.Update( Vector3.Zero, false, true, false, Vector3.UnitZ, Step ).Z, 3 );
	}
}
