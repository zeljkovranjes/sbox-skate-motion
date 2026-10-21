using SkateMotion.Core.Vehicle;
using Xunit;

namespace SboxSkateMotion.Tests;

public class DeckLeanTests
{
	private const float Frame = 1f / 60f;

	[Fact]
	public void FullSteerDipsTheDeckIntoTheTurnUpToTheTrucksTwist()
	{
		var left = Settle( 1f, 4f );
		var right = Settle( -1f, 4f );
		Assert.InRange( left, DeckLean.MaximumRoll * 0.9f, DeckLean.MaximumRoll );
		Assert.Equal( -left, right, 5 );
	}

	[Fact]
	public void HalfSteerLeansLittleAndSpeedLeansLess()
	{
		var full = Settle( 1f, 4f );
		Assert.InRange( Settle( 0.5f, 4f ), full * 0.1f, full * 0.35f );
		Assert.True( Settle( 1f, 20f ) < full * 0.7f );
		Assert.Equal( 0f, Settle( 0.04f, 4f ), 5 );
	}

	[Fact]
	public void AManualLeansLessAndPushingEasesItDown()
	{
		var half = Settle( 0.5f, 4f );
		Assert.InRange( Settle( 0.5f, 4f, DeckLeanMode.Manual ) / half, SteeringResponse.ManualShare - 0.02f, SteeringResponse.ManualShare + 0.02f );
		Assert.True( Settle( 1f, 4f, pushing: true ) < Settle( 1f, 4f ) * 0.95f );
	}

	[Fact]
	public void TheLeanEasesInAndOutWithoutSnapping()
	{
		var lean = new DeckLean();
		var previous = 0f;
		var previousChange = 0f;
		for ( var frame = 0; frame < 180; frame++ )
		{
			// A stick slammed over, held, then let go.
			var steer = frame is >= 10 and < 90 ? 1f : 0f;
			var roll = lean.Update( steer, 5f, false, DeckLeanMode.Carving, Frame );
			var change = roll - previous;
			Assert.True( MathF.Abs( change ) <= 0.07f, $"frame {frame} moved {change} rad" );
			Assert.True( MathF.Abs( change - previousChange ) <= 0.05f, $"frame {frame} changed pace by {change - previousChange}" );
			Assert.True( roll >= -1e-4f, $"frame {frame} overshot to {roll}" );
			previous = roll;
			previousChange = change;
		}
		Assert.InRange( previous, 0f, 0.002f );
	}

	[Fact]
	public void TheLeanIsTheSameAtAnyFrameRate()
	{
		Assert.Equal( Run( 1f / 30f ), Run( 1f / 144f ), 2 );
	}

	[Fact]
	public void LevelSettlesFlatAndTheAirLetsGoSlowly()
	{
		var lean = new DeckLean();
		for ( var frame = 0; frame < 60; frame++ )
			lean.Update( 1f, 4f, false, DeckLeanMode.Carving, Frame );
		var carving = lean.Roll;
		var air = new DeckLean();
		for ( var frame = 0; frame < 60; frame++ )
			air.Update( 1f, 4f, false, DeckLeanMode.Carving, Frame );
		for ( var frame = 0; frame < 30; frame++ )
		{
			lean.Update( 1f, 4f, false, DeckLeanMode.Level, Frame );
			air.Update( 1f, 4f, false, DeckLeanMode.Airborne, Frame );
		}
		Assert.True( lean.Roll < carving * 0.1f );
		// Half a second off the ground keeps more than half the lean.
		Assert.InRange( air.Roll, carving * 0.5f, carving );
		// Back on the ground with the stick let go the trucks take up the level deck over a moment, not at once.
		var before = air.Roll;
		var after = air.Update( 0f, 4f, false, DeckLeanMode.Carving, Frame );
		Assert.True( before - after < before * 0.2f );
		for ( var frame = 0; frame < 30; frame++ )
			after = air.Update( 0f, 4f, false, DeckLeanMode.Carving, Frame );
		Assert.True( after < before * 0.05f );
	}

	private static float Settle( float steer, float speed, DeckLeanMode mode = DeckLeanMode.Carving, bool pushing = false )
	{
		var lean = new DeckLean();
		var roll = 0f;
		for ( var frame = 0; frame < 120; frame++ )
			roll = lean.Update( steer, speed, pushing, mode, Frame );
		return roll;
	}

	private static float Run( float step )
	{
		var lean = new DeckLean();
		for ( var time = 0f; time < 0.25f; time += step )
			lean.Update( 1f, 5f, false, DeckLeanMode.Carving, step );
		return lean.Roll;
	}
}
