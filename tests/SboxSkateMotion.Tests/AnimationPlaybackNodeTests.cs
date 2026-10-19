using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public sealed class AnimationPlaybackNodeTests
{
	private sealed class Leaf : IAnimationPlaybackNode
	{
		internal float Length;
		internal float Time;
		internal AnimationPlaybackCompletion Result = AnimationPlaybackCompletion.Empty;
		internal readonly List<string> Calls;
		private readonly string _name;

		internal Leaf( string name, List<string> calls )
		{
			_name = name;
			Calls = calls;
		}

		public float Duration
		{
			get
			{
				Calls.Add( _name + ".duration" );
				return Length;
			}
		}

		public float Elapsed
		{
			get
			{
				Calls.Add( _name + ".elapsed" );
				return Time;
			}
		}

		public void Advance( float delta, float speed, ref AnimationPlaybackCompletion completion )
		{
			Calls.Add( _name + ".advance" );
			Time += delta * speed;
			completion = Result;
		}

		public void Seek( float time )
		{
			Calls.Add( _name + ".seek" );
			Time = time;
		}
	}

	[Fact]
	public void QueueWaitsForCompletionAndPreservesUntouchedCallerFields()
	{
		var calls = new List<string>();
		var previous = new Leaf( "old", calls ) { Result = new( 0, false, .1f, .25f ) };
		var next = new Leaf( "new", calls ) { Length = 1f };
		var queue = new AnimationSequencePlayback( previous, next );
		var result = new AnimationPlaybackCompletion( 17, true, .75f, 9f );
		queue.Advance( .1f, 2f, ref result );
		Assert.Equal( new( 17, false, .75f, 1.25f ), result );
		Assert.Equal( new[] { "old.advance", "new.duration" }, calls );
		Assert.False( queue.ActiveNext );
	}

	[Fact]
	public void QueueForwardsUnusedDeltaOnceAndThenAdvancesOnlySuccessor()
	{
		var calls = new List<string>();
		var previous = new Leaf( "old", calls ) { Result = new( 0, true, .025f, 0f ) };
		var next = new Leaf( "new", calls ) { Result = new( 5, false, 0f, 1f ) };
		var queue = new AnimationSequencePlayback( previous, next );
		var result = AnimationPlaybackCompletion.Empty;
		queue.Advance( .1f, 2f, ref result );
		Assert.True( queue.ActiveNext );
		Assert.Equal( .05f, next.Time );
		Assert.Equal( next.Result, result );
		calls.Clear();
		queue.Advance( .1f, 2f, ref result );
		Assert.Equal( new[] { "new.advance" }, calls );
		Assert.Equal( .25f, next.Time );
	}

	[Fact]
	public void QueueClockQueriesReferToSuccessorBeforeActivation()
	{
		var calls = new List<string>();
		var queue = new AnimationSequencePlayback( new Leaf( "old", calls ),
			new Leaf( "new", calls ) { Length = 2f, Time = .5f } );
		Assert.Equal( 2f, queue.Duration );
		Assert.Equal( .5f, queue.Elapsed );
		Assert.Equal( new[] { "new.duration", "new.elapsed" }, calls );
	}

	[Theory]
	[InlineData( 0, true )]
	[InlineData( 1, false )]
	[InlineData( 4, true )]
	public void OrdinaryTransitionAdvancesChildrenInOrder( int mode, bool previousAdvances )
	{
		var calls = new List<string>();
		var previous = new Leaf( "old", calls );
		var next = new Leaf( "new", calls ) { Result = new( 3, true, .01f, 0f ) };
		var transition = new AnimationTransitionPlayback( previous, next, .25f, (AnimationTransitionMode)mode );
		var result = AnimationPlaybackCompletion.Empty;
		transition.Advance( .1f, 2f, ref result );
		Assert.Equal( previousAdvances ? new[] { "old.advance", "new.advance" } : new[] { "new.advance" }, calls );
		Assert.Equal( .1f, transition.BlendElapsed );
		Assert.Equal( .2f, next.Time );
		Assert.Equal( next.Result, result );
	}

	[Fact]
	public void PhaseMatchReadsUpdatedPreviousClockAndPreservesCallerResult()
	{
		var calls = new List<string>();
		var previous = new Leaf( "old", calls ) { Length = 2f, Time = .25f };
		var next = new Leaf( "new", calls ) { Length = 4f };
		var transition = new AnimationTransitionPlayback( previous, next, .25f, AnimationTransitionMode.MatchPhase );
		var result = new AnimationPlaybackCompletion( 9, true, .75f, 8f );
		var before = result;
		transition.Advance( .25f, 1f, ref result );
		Assert.Equal( 1f, next.Time );
		Assert.Equal( before, result );
		Assert.Equal( new[] { "old.advance", "old.duration", "old.elapsed", "old.duration", "new.duration", "new.seek" }, calls );
	}

	[Theory]
	[InlineData( 0f )]
	[InlineData( -1f )]
	public void PhaseMatchFallsBackToAdvancingWhenPreviousHasNoPositiveDuration( float duration )
	{
		var calls = new List<string>();
		var transition = new AnimationTransitionPlayback( new Leaf( "old", calls ) { Length = duration },
			new Leaf( "new", calls ), .25f, AnimationTransitionMode.MatchPhase );
		var result = AnimationPlaybackCompletion.Empty;
		transition.Advance( .1f, 1f, ref result );
		Assert.Equal( new[] { "old.advance", "old.duration", "new.advance" }, calls );
	}

	[Fact]
	public void TimeMatchCapsAtSuccessorDuration()
	{
		var calls = new List<string>();
		var next = new Leaf( "new", calls ) { Length = .5f };
		var transition = new AnimationTransitionPlayback( new Leaf( "old", calls ) { Time = 1f },
			next, .25f, AnimationTransitionMode.MatchTime );
		var result = AnimationPlaybackCompletion.Empty;
		transition.Advance( .1f, 1f, ref result );
		Assert.Equal( .5f, next.Time );
		Assert.Equal( new[] { "old.advance", "old.elapsed", "new.duration", "new.seek" }, calls );
	}

	[Theory]
	[InlineData( .04f, .01f, .25f )]
	[InlineData( .1f, .025f, .15625f )]
	[InlineData( .1f, .2f, 1f )]
	public void TransitionWeightUsesItsOwnCurveAndBoundaries( float duration, float delta, float weight )
	{
		var calls = new List<string>();
		var transition = new AnimationTransitionPlayback( new Leaf( "old", calls ), new Leaf( "new", calls ),
			duration, AnimationTransitionMode.Both );
		var result = AnimationPlaybackCompletion.Empty;
		transition.Advance( delta, 1f, ref result );
		Assert.Equal( weight, transition.Weight );
		transition.RestartBlend();
		Assert.Equal( 0f, transition.Weight );
	}
}
