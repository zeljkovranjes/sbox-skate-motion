using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class AnimationChannelPlaybackTests
{
	[Fact]
	public void OverlayCompletionDoesNotReplaceBaseCompletion()
	{
		var baseClock = Clock( 2f );
		var overlay = Clock( .5f );
		var blend = Envelope();
		var channel = new AnimationChannelPlayback( baseClock, overlay, blend );
		var result = new AnimationPlaybackCompletion( 7, false, -1f, -1f );
		channel.Advance( 1f, 0f, ref result );
		Assert.Equal( new AnimationPlaybackCompletion( 7, false, -1f, 1f ), result );
		Assert.True( blend.Finished );
		Assert.Equal( 1f, baseClock.Position );
		Assert.Equal( 1f, overlay.Position );
	}

	[Fact]
	public void EntryHoldUsesTheAdvancedClockButThePreviousWeight()
	{
		var baseClock = Clock( 2f );
		var overlay = Clock( 1f );
		var blend = Envelope();
		blend.StartEnvelope( .5f, 0f, 0x80000000 );
		var channel = new AnimationChannelPlayback( baseClock, overlay, blend );
		var result = AnimationPlaybackCompletion.Empty;
		channel.Advance( .25f, 0f, ref result );
		Assert.Equal( 0f, overlay.Position );
		Assert.Equal( 0f, blend.State.Weight );
		channel.Advance( .25f, 0f, ref result );
		Assert.Equal( .25f, overlay.Position );
		Assert.Equal( .5f, blend.State.Weight );
		Assert.Equal( .5f, baseClock.Position );
	}

	[Fact]
	public void HeldExitFreezesOverlayWhileBaseKeepsAdvancing()
	{
		var baseClock = Clock( 2f );
		var overlay = Clock( 1f );
		var blend = Envelope();
		blend.Stop( .5f, true );
		var channel = new AnimationChannelPlayback( baseClock, overlay, blend );
		var result = AnimationPlaybackCompletion.Empty;
		channel.Advance( .25f, 0f, ref result );
		Assert.Equal( 0f, overlay.Position );
		Assert.Equal( .25f, baseClock.Position );
		Assert.Equal( .25f, blend.State.BlendOutElapsed );
	}

	[Fact]
	public void QueriesAndSeekUseTheBaseWithoutChangingOverlayOrEnvelope()
	{
		var baseClock = Clock( 2f );
		var overlay = Clock( 1f );
		overlay.Seek( .5f );
		var blend = Envelope();
		var before = blend.State;
		var channel = new AnimationChannelPlayback( baseClock, overlay, blend );
		channel.Seek( 1.5f );
		Assert.Equal( 2f, channel.Duration );
		Assert.Equal( 1.5f, channel.Elapsed );
		Assert.Equal( .5f, overlay.Position );
		Assert.Equal( before, blend.State );
	}

	[Fact]
	public void BothPhaseControlledChildrenReceiveTheSamePhase()
	{
		var baseClock = Clock( 2f, true );
		var overlay = Clock( 1f, true );
		var blend = Envelope();
		var channel = new AnimationChannelPlayback( baseClock, overlay, blend );
		var initial = new AnimationPlaybackCompletion( 3, true, .25f, .75f );
		var result = initial;
		channel.Advance( .1f, .5f, ref result );
		Assert.Equal( 1f, baseClock.Position );
		Assert.Equal( .5f, overlay.Position );
		Assert.Equal( initial, result );
		Assert.False( blend.Finished );
	}

	[Fact]
	public void TransitionRetainsPreviousAtExactFadeEndpoint()
	{
		var transition = new AnimationTransitionPlayback( Clock( 2f ), Clock( 1f ),
			.5f, AnimationTransitionMode.Both );
		var result = AnimationPlaybackCompletion.Empty;
		transition.Advance( .5f, 0f, ref result );
		Assert.False( transition.CanReleasePrevious );
		transition.Advance( .001f, 0f, ref result );
		Assert.True( transition.CanReleasePrevious );
		transition.RestartBlend();
		Assert.False( transition.CanReleasePrevious );
	}

	private static AnimationChannelBlend Envelope() => new( new AnimationChannelBlendState(
		0f, 1f, 0f, 0f, 0f, 0f, 0f, 0f, 0 ) );

	private static AnimationClipClock Clock( float seconds, bool phaseControlled = false )
		=> new( seconds * 30f + 1f, 30f, 1f, false, phaseControlled );
}
