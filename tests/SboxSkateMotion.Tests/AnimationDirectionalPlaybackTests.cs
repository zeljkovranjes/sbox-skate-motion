using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public sealed class AnimationDirectionalPlaybackTests
{
	private static AnimationClipClock Clip( int frames ) => new( frames, 30, 1, false, false );

	[Fact]
	public void AuthoredPositionsSelectNeighborsInSortedOrder()
	{
		var node = new AnimationDirectionalPlayback( new IAnimationRatePlaybackNode[] { Clip( 31 ), Clip( 61 ), Clip( 91 ) },
			new[] { 90f, -90f, 0f } );
		Assert.True( node.UpdateParameter( -45f, false ) );
		Assert.Equal( 1, node.FirstIndex );
		Assert.Equal( 2, node.SecondIndex );
		Assert.Equal( .5f, node.Weight );
		Assert.Equal( 2.5f, node.Duration );
		node.UpdateParameter( 0f, false );
		Assert.Equal( 1, node.FirstIndex );
		Assert.Equal( 2, node.SecondIndex );
		Assert.Equal( 1f, node.Weight );
	}

	[Fact]
	public void NormalizedInputUsesAuthoredRangeAndClampsAtEndpoints()
	{
		var node = new AnimationDirectionalPlayback( new IAnimationRatePlaybackNode[] { Clip( 31 ), Clip( 61 ) },
			new[] { -90f, 180f } );
		node.UpdateParameter( .5f, true );
		Assert.Equal( .5f, node.Weight );
		node.UpdateParameter( -1f, true );
		Assert.Equal( 0f, node.Weight );
		node.UpdateParameter( 2f, true );
		Assert.Equal( 1f, node.Weight );
	}

	[Fact]
	public void ParameterDeadbandPreservesSelectedClockAndMissingInputDoesNothing()
	{
		var node = new AnimationDirectionalPlayback( new IAnimationRatePlaybackNode[] { Clip( 31 ), Clip( 61 ) }, new[] { 0f, 1f } );
		Assert.False( node.UpdateParameter( .5f, false, false ) );
		Assert.Equal( 1f, node.Duration );
		Assert.True( node.UpdateParameter( 0f, false ) );
		Assert.False( node.UpdateParameter( .00001f, false ) );
		Assert.True( node.UpdateParameter( .0001f, false ) );
		Assert.True( node.UpdateParameter( .0001f, true ) );
	}

	[Fact]
	public void AdvancingUsesChildPhaseAndRescalesPreservedCompletionFields()
	{
		var first = Clip( 31 );
		var second = Clip( 91 );
		var node = new AnimationDirectionalPlayback( new IAnimationRatePlaybackNode[] { first, second }, new[] { 0f, 100f } );
		node.UpdateParameter( 50f, false );
		var result = new AnimationPlaybackCompletion( 9, true, .75f, 8f );
		node.Advance( .25f, 1f, ref result );
		Assert.Equal( .125f, first.Position );
		Assert.Equal( .375f, second.Position );
		Assert.Equal( .25f, node.Elapsed );
		Assert.Equal( new( 9, false, 1.5f, 1.75f ), result );
	}

	[Fact]
	public void SwitchingPairPreservesElapsedPhaseAndSeeksNewActiveChildren()
	{
		var first = Clip( 31 );
		var middle = Clip( 61 );
		var last = Clip( 121 );
		var node = new AnimationDirectionalPlayback( new IAnimationRatePlaybackNode[] { first, middle, last }, new[] { 0f, 1f, 2f } );
		node.UpdateParameter( 0f, false );
		node.Seek( .5f );
		node.UpdateParameter( 2f, false );
		Assert.Equal( 2f, node.Elapsed );
		Assert.Equal( 1f, middle.Position );
		Assert.Equal( 2f, last.Position );
		Assert.Equal( 2f, last.PreviousPosition );
	}

	[Fact]
	public void RateChangeUpdatesInactiveChildrenAndKeepsPairPhase()
	{
		var first = Clip( 31 );
		var middle = Clip( 61 );
		var last = Clip( 121 );
		var node = new AnimationDirectionalPlayback( new IAnimationRatePlaybackNode[] { first, middle, last }, new[] { 0f, 1f, 2f } );
		node.UpdateParameter( 0f, false );
		node.Seek( .5f );
		node.SetRate( 2f );
		Assert.Equal( .5f, node.Duration );
		Assert.Equal( .25f, node.Elapsed );
		Assert.Equal( .25f, first.Position );
		Assert.Equal( .5f, middle.Position );
		Assert.Equal( 2f, last.Rate );
		Assert.Equal( 2f, last.Duration );
	}

	[Fact]
	public void DuplicateSamplesHaveStableOrderAndZeroWeight()
	{
		var node = new AnimationDirectionalPlayback( new IAnimationRatePlaybackNode[] { Clip( 31 ), Clip( 61 ), Clip( 91 ) },
			new[] { 0f, 0f, 0f } );
		node.UpdateParameter( 0f, false );
		Assert.Equal( 0, node.FirstIndex );
		Assert.Equal( 1, node.SecondIndex );
		Assert.Equal( 0f, node.Weight );
		Assert.Equal( 1f, node.Duration );
	}

	[Fact]
	public void DirectionalCompletionHandsPairOvershootToQueuedClip()
	{
		var pair = new AnimationDirectionalPlayback( new IAnimationRatePlaybackNode[] { Clip( 31 ), Clip( 91 ) }, new[] { 0f, 1f } );
		pair.UpdateParameter( .5f, false );
		var next = Clip( 61 );
		var queue = new AnimationSequencePlayback( pair, next );
		var result = AnimationPlaybackCompletion.Empty;
		queue.Advance( 2.25f, 1f, ref result );
		Assert.True( queue.ActiveNext );
		Assert.Equal( .25f, next.Position );
		Assert.Equal( 1.75f, result.Remaining );
	}
}
