using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public sealed class AnimationClipClockTests
{
	[Fact]
	public void EndpointIsInclusiveAndPhaseDoesNotScaleOrdinaryDelta()
	{
		var clock = new AnimationClipClock( 31, 30, 1, false, false );
		var result = new AnimationPlaybackCompletion( 27, true, 8f, 9f );
		clock.Advance( 1f, 100f, ref result );
		Assert.Equal( 1f, clock.Position );
		Assert.Equal( new( 27, false, 8f, 0f ), result );
		clock.Advance( .25f, 0f, ref result );
		Assert.Equal( 1.25f, clock.Position );
		Assert.Equal( 1f, clock.Elapsed );
		Assert.Equal( new( 27, true, .25f, -.25f ), result );
	}

	[Fact]
	public void MultipleWrapsKeepFinalRemainderAndOriginalNegativeRemaining()
	{
		var clock = new AnimationClipClock( 31, 30, 1, true, false );
		var result = AnimationPlaybackCompletion.Empty;
		clock.Advance( 3.25f, 0f, ref result );
		Assert.Equal( .25f, clock.Position );
		Assert.Equal( 3u, clock.WrapCount );
		Assert.Equal( new( 0, true, .25f, -2.25f ), result );
		clock.Advance( .75f, 0f, ref result );
		Assert.Equal( 1f, clock.Position );
		Assert.Equal( 3u, clock.WrapCount );
		Assert.Equal( new( 0, false, .25f, 0f ), result );
	}

	[Fact]
	public void PhaseClockClampsReplacesWrapCountAndLeavesCompletionUntouched()
	{
		var clock = new AnimationClipClock( 61, 30, 1, true, true );
		var expected = new AnimationPlaybackCompletion( 5, true, 7f, 9f );
		var result = expected;
		clock.Advance( 100f, 1.5f, ref result );
		Assert.Equal( 2f, clock.Position );
		Assert.Equal( 0f, clock.PreviousPosition );
		clock.Advance( 100f, -.5f, ref result );
		Assert.Equal( 0f, clock.Position );
		Assert.Equal( 2f, clock.PreviousPosition );
		Assert.Equal( 1u, clock.WrapCount );
		clock.Advance( 100f, .25f, ref result );
		Assert.Equal( .5f, clock.Position );
		Assert.Equal( 0u, clock.WrapCount );
		Assert.Equal( expected, result );
	}

	[Fact]
	public void SeekPreservesNegativeTimeAndClearsWraps()
	{
		var clock = new AnimationClipClock( 31, 30, 1, true, false );
		var result = AnimationPlaybackCompletion.Empty;
		clock.Advance( 2.25f, 0f, ref result );
		clock.Seek( -.5f );
		Assert.Equal( -.5f, clock.Position );
		Assert.Equal( -.5f, clock.PreviousPosition );
		Assert.Equal( -.5f, clock.Elapsed );
		Assert.Equal( 0u, clock.WrapCount );
		clock.Advance( .25f, 0f, ref result );
		Assert.Equal( -.5f, clock.PreviousPosition );
	}

	[Fact]
	public void RateChangePreservesFramePositionForBothClockSamples()
	{
		var clock = new AnimationClipClock( 61, 30, .5f, true, true );
		var result = AnimationPlaybackCompletion.Empty;
		clock.Seek( 1f );
		clock.Advance( 0f, .75f, ref result );
		clock.SetRate( 2f );
		Assert.Equal( 2f, clock.Duration );
		Assert.Equal( 1.5f, clock.Position );
		Assert.Equal( .5f, clock.PreviousPosition );
		Assert.Equal( 2f, clock.Rate );
	}

	[Fact]
	public void SingleFrameNonLoopingClockHasZeroDuration()
	{
		var clock = new AnimationClipClock( 1, 30, 1, false, false );
		var result = AnimationPlaybackCompletion.Empty;
		clock.Advance( 0f, 0f, ref result );
		Assert.Equal( 0f, clock.Duration );
		Assert.False( result.Finished );
		clock.Advance( .25f, 0f, ref result );
		Assert.True( result.Finished );
		Assert.Equal( .25f, result.UnusedDelta );
	}

	[Fact]
	public void QueueUsesConcreteClipOvershootToStartSuccessorInSameUpdate()
	{
		var previous = new AnimationClipClock( 31, 30, 1, false, false );
		var next = new AnimationClipClock( 61, 30, 1, true, false );
		var queue = new AnimationSequencePlayback( previous, next );
		var result = AnimationPlaybackCompletion.Empty;
		queue.Advance( 1f, 1f, ref result );
		Assert.False( queue.ActiveNext );
		queue.Advance( .125f, 1f, ref result );
		Assert.True( queue.ActiveNext );
		Assert.Equal( .125f, next.Position );
		Assert.Equal( 1.875f, result.Remaining );
	}

	[Fact]
	public void PhaseTransitionSeeksConcreteSuccessorWithoutAdvancingItsClock()
	{
		var previous = new AnimationClipClock( 31, 30, 1, false, false );
		var next = new AnimationClipClock( 61, 30, 1, false, false );
		var transition = new AnimationTransitionPlayback( previous, next, .25f, AnimationTransitionMode.MatchPhase );
		var result = new AnimationPlaybackCompletion( 3, true, 5f, 7f );
		var expected = result;
		transition.Advance( .25f, 1f, ref result );
		Assert.Equal( .25f, previous.Position );
		Assert.Equal( .5f, next.Position );
		Assert.Equal( .5f, next.PreviousPosition );
		Assert.Equal( expected, result );
	}
}
