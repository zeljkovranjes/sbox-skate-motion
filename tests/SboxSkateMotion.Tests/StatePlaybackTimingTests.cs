using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class StatePlaybackTimingTests
{
	[Fact]
	public void AHeldPoseLastsAsLongAsItsFadeBeforeEnding()
	{
		// A single-frame pose has no length; a transition taken when it ends must still show it first, through its fade.
		var skeleton = new ClipSkeleton( [ "root" ], [ -1 ], [ Vector3.Zero ], [ Quaternion.Identity ] );
		var clips = new ClipSet( skeleton, [ Pose( "pose" ), Pose( "next" ) ] );
		var hold = new StateGraphDefinition
		{
			Name = "Hold",
			Play = new StatePlayDefinition { Clip = "pose", BlendTime = 0.2f }
		};
		hold.Transitions.Add( new StateTransitionDefinition
		{
			Target = "Next",
			Guard = new StateTestDefinition { Test = "clip-ending", Mask = 1, Within = 0.02f, Wait = false }
		} );
		var next = new StateGraphDefinition { Name = "Next", Active = false, Play = new StatePlayDefinition { Clip = "next", BlendTime = 0.1f } };
		var player = new StateGraphPlayer( clips, new StateGraphDefinition { Name = "Root", States = [ hold, next ] } );
		var pose = new SkeletonPose( 1 );
		var parameters = new AnimationParameters();
		var facts = new StateFacts();
		for ( var frame = 0; frame < 3; frame++ )
			player.Update( facts, parameters, 0.05f, pose );
		Assert.Equal( "Hold", player.Graph.State );
		for ( var frame = 0; frame < 4; frame++ )
			player.Update( facts, parameters, 0.05f, pose );
		Assert.Equal( "Next", player.Graph.State );
	}

	[Fact]
	public void ANewClipDoesNotInheritTheEndingOfTheLastOne()
	{
		// Entering a state starts its clip; its children must not see the previous clip's last moments as this one's.
		var late = State( "Late", new StateTestDefinition { Test = "clip-ending", Mask = 1, Within = 0.1f }, null );
		var early = State( "Early", null, null );
		var go = State( "Go", new StateTestDefinition { Test = "intent", Intent = "go", Mask = 1 }, "go", late, early );
		var graph = new StateGraph( State( "Root", null, null, go, State( "Idle", null, "idle" ) ) );
		var facts = new StateFacts();
		graph.Update( facts, 0.1f, new AnimationPlaybackStatus( 10f, false ) );
		Assert.Equal( "Idle", graph.State );
		facts.Intents[ "go" ] = 1f;
		graph.Update( facts, 0.1f, new AnimationPlaybackStatus( 0.05f, false ) );
		Assert.Equal( "Go.Early", graph.State );
	}

	[Fact]
	public void AnUninterruptibleStateKeepsItsWholePath()
	{
		// While the current state may not be interrupted, no guarded state earlier anywhere above it takes over, though
		// the same takeover happens from an interruptible state.
		var jump = State( "Jump", null, null, State( "Crouch", null, "crouch" ) );
		jump.States[ 0 ].Interruptible = "false";
		var ground = State( "Ground", new StateTestDefinition { Test = "intent", Intent = "ground", Mask = 3 }, "ground" );
		var graph = new StateGraph( State( "Root", null, null, ground, jump ) );
		var facts = new StateFacts();
		graph.Update( facts, 0.1f );
		Assert.Equal( "Jump.Crouch", graph.State );
		facts.Intents[ "ground" ] = 1f;
		graph.Update( facts, 0.1f );
		Assert.Equal( "Jump.Crouch", graph.State );

		jump.States[ 0 ].Interruptible = null;
		var open = new StateGraph( State( "Root", null, null, ground, jump ) );
		facts.Intents.Remove( "ground" );
		open.Update( facts, 0.1f );
		facts.Intents[ "ground" ] = 1f;
		open.Update( facts, 0.1f );
		Assert.Equal( "Ground", open.State );
	}

	private static AnimationClip Pose( string name ) =>
		new( name, 30f, 1, [ 0 ], [ 0 ], [], [ 0 ], [ 1 ], [ Vector3.Zero ] );

	private static StateGraphDefinition State( string name, StateTestDefinition guard, string clip, params StateGraphDefinition[] children ) => new()
	{
		Name = name,
		Guard = guard,
		Play = clip is null ? null : new StatePlayDefinition { Clip = clip, BlendTime = 0f },
		States = children.ToList()
	};
}
