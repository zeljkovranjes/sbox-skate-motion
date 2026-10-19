using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class AnimationChannelEnvelopeLifecycleTests
{
	[Fact]
	public void StartClearsClocksAndCompletionAndSelectsRequestedControlBits()
	{
		var blend = new AnimationChannelBlend( State() );
		blend.StartEnvelope( .25f, .5f, 0xd2000000 );
		Assert.Equal( new AnimationChannelBlendState(
			0f, 1f, 0f, .25f, 0f, .5f, 0f, 0f, 0xdb123456 ), blend.State );
	}

	[Theory]
	[InlineData( 0f )]
	[InlineData( -.25f )]
	public void NonpositiveExitDurationClearsAutomaticExitFlag( float duration )
	{
		var blend = new AnimationChannelBlend( State() );
		blend.StartEnvelope( .25f, duration, uint.MaxValue );
		Assert.Equal( 0xd3123456u, blend.State.Flags );
		Assert.Equal( duration, blend.State.BlendOutDuration );
	}

	[Fact]
	public void RejectedReplacementPreservesTheWholeEnvelope()
	{
		var initial = State();
		var blend = new AnimationChannelBlend( initial );
		Assert.False( blend.CanReplace( false ) );
		Assert.False( blend.TryReplaceEnvelope( .25f, 0f, 0u, false ) );
		Assert.Equal( initial, blend.State );
	}

	[Fact]
	public void RecoveryUsesTheOldExitClockBeforeResettingIt()
	{
		var initial = State();
		var blend = new AnimationChannelBlend( initial );
		Assert.True( blend.TryReplaceEnvelope( .5f, .75f, 0x52000000, true ) );
		Assert.Equal( initial with
		{
			RecoveryElapsed = .25f, RecoveryDuration = .5f,
			BlendOutElapsed = 0f, BlendOutDuration = .75f, Flags = 0xdf123456
		}, blend.State );
	}

	[Fact]
	public void NonfadingReplacementKeepsRecoveryHistoryAndDoesNotAdoptEntryHold()
	{
		var initial = State() with { BlendOutElapsed = 0f, Flags = 0x24123456 };
		var blend = new AnimationChannelBlend( initial );
		Assert.True( blend.CanReplace( false ) );
		Assert.True( blend.TryReplaceEnvelope( 3f, 0f, 0x80000000, false ) );
		Assert.Equal( initial with { BlendOutDuration = 0f, Flags = 0x04123456 }, blend.State );
	}

	[Fact]
	public void ReplacementPreservesCompletionUntilTheNextOverlayUpdate()
	{
		var blend = new AnimationChannelBlend( State() with { BlendOutElapsed = 0f } );
		Assert.True( blend.TryReplaceEnvelope( 0f, 0f, 0u, false ) );
		Assert.True( blend.Finished );
		blend.CompleteOverlayUpdate( false );
		Assert.False( blend.Finished );
	}

	private static AnimationChannelBlendState State() => new(
		.75f, .5f, .125f, .25f, .25f, .5f, .75f, 1f, 0xff123456 );
}
