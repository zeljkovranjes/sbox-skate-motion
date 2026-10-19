using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public sealed class AnimationChannelBlendTests
{
	private static AnimationChannelBlend Create( float blendIn = 0f, float blendOut = 0f, uint flags = 0 )
		=> new( new( 0f, 1f, 0f, blendIn, 0f, blendOut, 0f, 0f, flags ) );

	[Fact]
	public void EntryWeightUsesPreviousClockButPlaybackGateUsesAdvancedClock()
	{
		var layer = Create( .25f, flags: 0x80000000 );
		Assert.False( layer.Update( .125f, 1f, 0f ) );
		Assert.Equal( 0f, layer.State.Weight );
		Assert.True( layer.Update( .125f, 1f, 0f ) );
		Assert.Equal( .5f, layer.State.Weight );
		Assert.True( layer.Update( .125f, 1f, 0f ) );
		Assert.Equal( 1f, layer.State.Weight );
	}

	[Fact]
	public void AutomaticExitStartsStrictlyInsideRemainingWindow()
	{
		var layer = Create( blendOut: .25f, flags: 0x08000000 );
		layer.Update( .125f, 1f, .75f );
		Assert.Equal( 0f, layer.State.BlendOutElapsed );
		layer.Update( .125f, 1f, .875f );
		Assert.Equal( 1f, layer.State.Weight );
		Assert.False( layer.Finished );
		layer.Update( .125f, 1f, 1f );
		Assert.Equal( .5f, layer.State.Weight );
		Assert.True( layer.Finished );
	}

	[Fact]
	public void KeepPlayingPreventsAutomaticExitButAllowsExplicitStop()
	{
		var layer = Create( blendOut: .25f, flags: 0x10000000 );
		layer.Update( .1f, 1f, 1f );
		Assert.Equal( 0f, layer.State.BlendOutElapsed );
		layer.Stop( .5f, false );
		Assert.True( layer.Update( .1f, 1f, 1f ) );
		Assert.Equal( .1f, layer.State.BlendOutElapsed );
	}

	[Fact]
	public void HoldingExitWaitsForPublishedCompletionThenFreezesOverlay()
	{
		var layer = Create( blendOut: .25f, flags: 0x40000000 );
		Assert.True( layer.Update( .125f, 1f, 1f ) );
		layer.CompleteOverlayUpdate( true );
		Assert.False( layer.Update( .125f, 1f, 1f ) );
		Assert.Equal( 1f, layer.State.Weight );
		Assert.False( layer.Update( .125f, 1f, 1f ) );
		Assert.Equal( .5f, layer.State.Weight );
	}

	[Fact]
	public void ActiveExitDoesNotRestartWhenStopIsRepeated()
	{
		var layer = Create();
		layer.Stop( .25f, false );
		layer.Update( .125f, 1f, 0f );
		var before = layer.State;
		layer.Stop( 1f, true );
		Assert.Equal( before, layer.State );
	}

	[Fact]
	public void RecoveryStartsAtCurrentExitWeight()
	{
		var layer = Create();
		layer.Stop( .5f, false );
		layer.Update( .125f, 1f, 0f );
		layer.Recover( 1f );
		Assert.Equal( .75f, layer.State.RecoveryElapsed );
		Assert.Equal( 1f, layer.State.RecoveryDuration );
	}

	[Theory]
	[InlineData( 0u, false )]
	[InlineData( 0x04000000u, true )]
	[InlineData( 0x14000000u, false )]
	public void WithoutTimedExitFinishedDependsOnCompletionAndKeepPlaying( uint flags, bool expected )
	{
		Assert.Equal( expected, Create( flags: flags ).Finished );
	}

	[Fact]
	public void StopPreservesUnrelatedFlagsAndCompletionCanClear()
	{
		var layer = Create( flags: 0x95abcdef );
		layer.Stop( .25f, true );
		Assert.Equal( 0xfdabcdefu, layer.State.Flags );
		layer.CompleteOverlayUpdate( false );
		Assert.Equal( 0xf9abcdefu, layer.State.Flags );
	}

	[Theory]
	[InlineData( -1f, 0f )]
	[InlineData( 2f, 1f )]
	public void FinalWeightIsBounded( float target, float expected )
	{
		var layer = new AnimationChannelBlend( new( 0f, target, 0f, 0f, 0f, 0f, 0f, 0f, 0 ) );
		layer.Update( .1f, 1f, 0f );
		Assert.Equal( expected, layer.State.Weight );
	}
}
