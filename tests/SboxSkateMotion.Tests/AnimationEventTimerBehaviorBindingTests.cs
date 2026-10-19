using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public sealed class AnimationEventTimerBehaviorBindingTests
{
	[Fact]
	public void RetainedPhysicalResetSkipsClockWhileKickturnStillAdvances()
	{
		var movement = new AnimationPhysicalMovementState( default );
		var timers = new AnimationOwnerEventTimers( 2f, 9f );
		var reads = 0;
		var binding = new AnimationEventTimerBehaviorBinding( timers, movement, () => { reads++; return 0.125f; } );
		movement.Update( new( new( 702, 700 ), 0, false, false, false, false, false, default ) );
		binding.UpdateTeleport();
		Assert.Equal( 0, reads );
		Assert.Equal( 0, BitConverter.SingleToInt32Bits( timers.SinceTeleport ) );
		binding.UpdateKickturn();
		Assert.Equal( 1, reads );
		Assert.Equal( 2.125f, timers.SinceKickturn );
		movement.Update( new( new( 100, 100 ), 0, false, false, false, false, false, default ) );
		binding.UpdateTeleport();
		Assert.Equal( 2, reads );
		Assert.Equal( 0.125f, timers.SinceTeleport );
	}

	[Fact]
	public void StatelessLifecyclePreservesSharedClocksAndResetsAreIndependent()
	{
		var timers = new AnimationOwnerEventTimers( 7f, 11f );
		var binding = new AnimationEventTimerBehaviorBinding( timers,
			new AnimationPhysicalMovementState( default ), () => -0.25f );
		var lifecycle = binding.Bind();
		var instance = lifecycle.Create();
		Assert.Equal( -1, instance );
		lifecycle.Enter( instance );
		lifecycle.Transition();
		lifecycle.Exit( instance );
		Assert.Equal( 7f, timers.SinceKickturn );
		Assert.Equal( 11f, timers.SinceTeleport );
		binding.UpdateKickturn();
		binding.UpdateTeleport();
		Assert.Equal( 6.75f, timers.SinceKickturn );
		Assert.Equal( 10.75f, timers.SinceTeleport );
		timers.ResetKickturn();
		Assert.Equal( 0, BitConverter.SingleToInt32Bits( timers.SinceKickturn ) );
		Assert.Equal( 10.75f, timers.SinceTeleport );
	}
}
