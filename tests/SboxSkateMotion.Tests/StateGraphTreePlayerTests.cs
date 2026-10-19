using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class StateGraphTreePlayerTests
{
	[Fact]
	public void ParentPlayPrecedesChildSequenceWhenBothEnterDuringOneUpdate()
	{
		var root = State( "Root", "parent", "play" );
		root.States.Add( State( "Child", "child", "sequence" ) );
		var player = new StateGraphTreePlayer( root, new Factory(), new[] { "parent", "child" } );
		var pose = new SkeletonPose( 1 );
		Assert.True( player.Update( new StateFacts(), .25f, 0f, pose, true, 1 ) );
		Assert.Equal( 2, player.Graph.PlayCount );
		Assert.Equal( "child", player.ClipName );
		Assert.Equal( 1, player.Owner.RetainedCount );
		Assert.Equal( 0f, pose.Positions[ 0 ].X );
	}

	[Fact]
	public void FirstEntryUsesCurrentFactsAndExpiryObservesThePreviousAdvance()
	{
		var into = State( "Into", "into", "play" );
		into.Guard = new() { Test = "intent", Intent = "push" };
		into.Interruptible = "false";
		into.Transitions.Add( new()
		{
			Target = "Out",
			Guard = new() { Test = "clip-ending", Within = 0f, Wait = false }
		} );
		var finish = State( "Out", "out", "play" );
		finish.Active = false;
		var graph = new StateGraphDefinition { Name = "Root", States = { into, finish, State( "Idle", "idle", "play" ) } };
		var player = new StateGraphTreePlayer( graph, new Factory(), new[] { "into", "out", "idle" } );
		var facts = new StateFacts();
		facts.Intents[ "push" ] = 1f;
		var pose = new SkeletonPose( 1 );
		Assert.Equal( AnimationPlaybackCompletion.Empty, player.Completion );
		Assert.True( player.Update( facts, .5f, 0f, pose, true, 1 ) );
		Assert.Equal( "Into", player.Graph.State );
		Assert.Equal( 0f, pose.Positions[ 0 ].X );
		Assert.Equal( .5f, player.Completion.Remaining );
		player.Update( facts, .5f, 0f, pose, true, 1 );
		Assert.Equal( "Into", player.Graph.State );
		// The block sampler quantizes the half-frame weight to 32767/65535.
		Assert.Equal( .49999237f, pose.Positions[ 0 ].X );
		Assert.Equal( 0f, player.Completion.Remaining );
		player.Update( facts, .1f, 0f, pose, true, 1 );
		Assert.Equal( "Out", player.Graph.State );
		Assert.Equal( 10f, pose.Positions[ 0 ].X );
		Assert.Equal( 1, player.Completion.Flags );
	}

	[Fact]
	public void QueuedSuccessorRetainsThePoseAndWaitGateUntilCleanupOnTheFollowingTick()
	{
		var start = State( "Start", "start", "play" );
		start.Transitions.Add( new() { Target = "Queued", Guard = new() { Test = "intent", Intent = "queue" } } );
		var queued = State( "Queued", "queued", "sequence" );
		queued.Active = false;
		queued.Interruptible = "false";
		queued.Transitions.Add( new() { Target = "End", Guard = new() { Test = "clip-ending", Within = 2f, Wait = true } } );
		var end = State( "End", "end", "play" );
		end.Active = false;
		var player = new StateGraphTreePlayer( new() { Name = "Root", States = { start, queued, end } },
			new Factory(), new[] { "start", "queued", "end" } );
		var pose = new SkeletonPose( 1 );
		var facts = new StateFacts();
		player.Update( facts, .5f, 0f, pose, true, 1 );
		facts.Intents[ "queue" ] = 1f;
		player.Update( facts, .75f, 0f, pose, true, 1 );
		Assert.Equal( "Queued", player.Graph.State );
		Assert.Equal( .49999237f, pose.Positions[ 0 ].X );
		Assert.True( player.Status.Transitioning );
		Assert.Equal( .25f, player.Owner.Elapsed );
		player.Update( facts, .1f, 0f, pose, true, 1 );
		Assert.Equal( "Queued", player.Graph.State );
		Assert.Equal( 10.249989f, pose.Positions[ 0 ].X );
		Assert.False( player.Status.Transitioning );
		player.Update( facts, .1f, 0f, pose, true, 1 );
		Assert.Equal( "End", player.Graph.State );
		Assert.Equal( 20f, pose.Positions[ 0 ].X );
	}

	private static StateGraphDefinition State( string name, string motion, string transition ) => new()
	{
		Name = name,
		Play = new() { Clip = motion, Transition = transition }
	};

	private sealed class Factory : IAnimationBasePoseTreeFactory
	{
		public IAnimationPlaybackTree Create( int storageSlot, int definition ) => new Tree( definition );
		public void Publish( IAnimationPosePlaybackNode root ) { }
	}

	private sealed class Tree : IAnimationPlaybackTree
	{
		internal Tree( int definition )
		{
			var clip = new RawAnimationClip( "motion", 1f, 1f, 2, 0, 0,
				new[] { 0, 0, 0 }, new[] { 2, 0, 0 }, new[]
				{
					new Vector4( definition * 10f, 0, 0, 1 ),
					new Vector4( definition * 10f + 1f, 0, 0, 1 )
				} );
			Root = new AnimationClipPlayback( clip, 0, new AnimationPoseSamplingContext( 1, false ) );
		}

		public IAnimationPlaybackNode Root { get; }
	}
}
