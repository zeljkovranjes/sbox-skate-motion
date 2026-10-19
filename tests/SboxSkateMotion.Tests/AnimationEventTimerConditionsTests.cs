using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public sealed class AnimationEventTimerConditionsTests
{
	[Fact]
	public void EnterAndExitResetSharedClockWhileTransitionPreservesIt()
	{
		var timers = new AnimationOwnerEventTimers( 3f, 8f );
		var binding = new AnimationEventTimerConditions( timers );
		var guard = binding.BindKickturn( new( ConditionComparisonMode.NotLess, 0.5f ) );
		var reset = binding.BindKickturnReset();
		var instance = reset.Create();
		Assert.Equal( -1, instance );
		Assert.True( guard( 4, false ) );
		reset.Enter( instance );
		Assert.False( guard( 4, true ) );
		timers.AdvanceKickturn( 0.5f );
		reset.Transition();
		Assert.True( guard( 8, false ) );
		reset.Exit( instance );
		Assert.False( guard( 8, true ) );
		Assert.Equal( 0, BitConverter.SingleToInt32Bits( timers.SinceKickturn ) );
		Assert.Equal( 8f, timers.SinceTeleport );
	}

	[Fact]
	public void GuardUsesExistingDecisionCacheWithoutRejectingNegativeElapsedValues()
	{
		var timers = new AnimationOwnerEventTimers( -2f, 1f );
		var guard = new AnimationEventTimerConditions( timers ).BindKickturn(
			new( ConditionComparisonMode.Greater, 1f, true ) );
		var cache = new ConditionResultCache( 1, ( _, state, _ ) => guard( (int)state, false ) );
		var decision = new ConditionDecisionEvaluator( [new( 0, -1, -2 )], [0], [0], cache.Evaluate );
		Assert.True( decision.Evaluate( 0, 1, 2, false ) );
		timers.ResetKickturn();
		Assert.True( decision.Evaluate( 0, 1, 2, true ) );
		cache.Invalidate();
		Assert.False( decision.Evaluate( 0, 1, 2, true ) );
	}
}
