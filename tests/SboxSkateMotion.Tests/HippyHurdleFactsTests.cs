using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class HippyHurdleFactsTests
{
	private const float Frame = 1f / 60f;

	[Fact]
	public void AHippyJumpTakingOffOutsideTheGraphsJumpComesInByItsHurdle()
	{
		var facts = new StateFacts();
		new RiderStateFacts().Fill( facts, new RiderAnimationInput { OffBoard = true, OffBoardSpeed = 1f }, false, Frame );
		Assert.Equal( "offboard", facts.Physics );
		Assert.Equal( 1f, facts.Facts[ "is-hippy-hurdling" ] );
	}

	[Fact]
	public void AHippyJumpTheGraphTookOffInFliesWithoutTheHurdle()
	{
		var facts = new StateFacts();
		new RiderStateFacts { GraphHippyJump = true }.Fill( facts, new RiderAnimationInput { OffBoard = true, OffBoardSpeed = 1f }, false, Frame );
		Assert.Equal( 0f, facts.Facts[ "is-hippy-hurdling" ] );
	}

	[Fact]
	public void OnTheBoardThereIsNoHurdle()
	{
		var facts = new StateFacts();
		new RiderStateFacts().Fill( facts, new RiderAnimationInput(), false, Frame );
		Assert.Equal( 0f, facts.Facts[ "is-hippy-hurdling" ] );
	}
}
