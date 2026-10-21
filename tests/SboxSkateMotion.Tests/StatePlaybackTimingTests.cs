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
	public void AShortMovingClipEndsWhenItEndsEvenInsideItsFade()
	{
		// A five-frame clip (a push's contact) fading in over longer than it lasts must end on time, not loop back to its
		// start again and again until the fade is over.
		var skeleton = new ClipSkeleton( [ "root" ], [ -1 ], [ Vector3.Zero ], [ Quaternion.Identity ] );
		var moving = new AnimationClip( "moving", 60f, 5, [ 0 ], [ 0 ], [], [ 0 ], [ 5 ],
			[ Vector3.Zero, Vector3.UnitX, 2f * Vector3.UnitX, 3f * Vector3.UnitX, 4f * Vector3.UnitX ] );
		var clips = new ClipSet( skeleton, [ moving, Pose( "next" ) ] );
		var hold = new StateGraphDefinition
		{
			Name = "Hold",
			Play = new StatePlayDefinition { Clip = "moving", BlendTime = 0.2f }
		};
		hold.Transitions.Add( new StateTransitionDefinition
		{
			Target = "Next",
			Guard = new StateTestDefinition { Test = "clip-ending", Mask = 1, Within = 0.01f, Wait = false }
		} );
		var next = new StateGraphDefinition { Name = "Next", Active = false, Play = new StatePlayDefinition { Clip = "next", BlendTime = 0.2f } };
		var player = new StateGraphPlayer( clips, new StateGraphDefinition { Name = "Root", States = [ hold, next ] } );
		var pose = new SkeletonPose( 1 );
		var parameters = new AnimationParameters();
		var facts = new StateFacts();
		var time = 0f;
		while ( player.Graph.State == "Hold" && time < 0.3f )
		{
			player.Update( facts, parameters, 1f / 120f, pose );
			time += 1f / 120f;
		}
		Assert.Equal( "Next", player.Graph.State );
		Assert.True( time < 0.09f, $"ended after {time} s" );
	}

	[Fact]
	public void MotionKeepsGoingThroughAClipChange()
	{
		// One clip moving the root steadily hands over to another moving the same way: no frame, the change's own
		// included, may hold the root still.
		var skeleton = new ClipSkeleton( [ "root" ], [ -1 ], [ Vector3.Zero ], [ Quaternion.Identity ] );
		var first = new AnimationClip( "first", 60f, 5, [ 0 ], [ 0 ], [], [ 0 ], [ 5 ],
			Enumerable.Range( 0, 5 ).Select( f => f * Vector3.UnitX ).ToArray() );
		var second = new AnimationClip( "second", 60f, 21, [ 0 ], [ 0 ], [], [ 0 ], [ 21 ],
			Enumerable.Range( 0, 21 ).Select( f => (4f + f) * Vector3.UnitX ).ToArray() );
		var clips = new ClipSet( skeleton, [ first, second ] );
		var start = new StateGraphDefinition { Name = "First", Play = new StatePlayDefinition { Clip = "first", BlendTime = 0f } };
		start.Transitions.Add( new StateTransitionDefinition
		{
			Target = "Second",
			Guard = new StateTestDefinition { Test = "clip-ending", Mask = 1, Within = 0.01f, Wait = false }
		} );
		var then = new StateGraphDefinition { Name = "Second", Active = false, Play = new StatePlayDefinition { Clip = "second", BlendTime = 0.2f } };
		var player = new StateGraphPlayer( clips, new StateGraphDefinition { Name = "Root", States = [ start, then ] } );
		var pose = new SkeletonPose( 1 );
		var parameters = new AnimationParameters();
		var facts = new StateFacts();
		const float step = 1f / 120f;
		player.Update( facts, parameters, step, pose );
		var last = pose.Positions[ 0 ].X;
		var changed = false;
		for ( var frame = 0; frame < 24; frame++ )
		{
			player.Update( facts, parameters, step, pose );
			changed |= player.Graph.State == "Second";
			var moved = pose.Positions[ 0 ].X - last;
			last = pose.Positions[ 0 ].X;
			Assert.True( moved > 0.25f, $"frame {frame} ({player.Graph.State}) moved {moved}" );
		}
		Assert.True( changed );
	}

	[Fact]
	public void ABlendWeightChangingAsTheClipChangesDoesNotJumpThePose()
	{
		// A landing sets the impact on the same frame it leaves a grab's release, a blend over that impact: the change
		// must carry on from the pose shown, not the outgoing blend's pose at the new weights.
		var skeleton = new ClipSkeleton( [ "root" ], [ -1 ], [ Vector3.Zero ], [ Quaternion.Identity ] );
		Vector3[] Track( float start ) => Enumerable.Range( 0, 31 ).Select( f => (start + 0.1f * f) * Vector3.UnitX ).ToArray();
		var clips = new ClipSet( skeleton, [
			new AnimationClip( "soft", 60f, 31, [ 0 ], [ 0 ], [], [ 0 ], [ 31 ], Track( 0f ) ),
			new AnimationClip( "hard", 60f, 31, [ 0 ], [ 0 ], [], [ 0 ], [ 31 ], Track( 50f ) ),
			new AnimationClip( "land", 60f, 31, [ 0 ], [ 0 ], [], [ 0 ], [ 31 ], Track( 0f ) ) ] );
		clips.AddBlends( new Dictionary<string, BlendDefinition>
		{
			[ "release" ] = new()
			{
				Type = "blend", Parameter = "impact",
				Children = [ new BlendChildDefinition { Name = "soft", At = 0f }, new BlendChildDefinition { Name = "hard", At = 1f } ]
			}
		} );
		var release = new StateGraphDefinition { Name = "Release", Play = new StatePlayDefinition { Clip = "release", BlendTime = 0f } };
		release.Transitions.Add( new StateTransitionDefinition
		{
			Target = "Land",
			Guard = new StateTestDefinition { Test = "intent", Intent = "land", Mask = 1 }
		} );
		var land = new StateGraphDefinition { Name = "Land", Active = false, Play = new StatePlayDefinition { Clip = "land", BlendTime = 0.15f } };
		var player = new StateGraphPlayer( clips, new StateGraphDefinition { Name = "Root", States = [ release, land ] } );
		var pose = new SkeletonPose( 1 );
		var parameters = new AnimationParameters();
		var facts = new StateFacts();
		const float step = 1f / 120f;
		for ( var frame = 0; frame < 10; frame++ )
			player.Update( facts, parameters, step, pose );
		var last = pose.Positions[ 0 ].X;
		facts.Intents[ "land" ] = 1f;
		parameters.Set( "impact", 1f );
		for ( var frame = 0; frame < 20; frame++ )
		{
			player.Update( facts, parameters, step, pose );
			var moved = MathF.Abs( pose.Positions[ 0 ].X - last );
			last = pose.Positions[ 0 ].X;
			Assert.True( moved < 1f, $"frame {frame} ({player.Graph.State}) moved {moved}" );
		}
		Assert.Equal( "Land", player.Graph.State );
	}

	[Fact]
	public void AClipLeftAsItEndsStartsTheNextFromItsStartWhenKeepingPhase()
	{
		// A grab's start hands over to its dismount, which keeps the grab's phase, as the grab's start ends: the end test
		// can pass a moment short of the very end, and the dismount must then start from its own start, not from just short
		// of its end (from where it would wrap back to its start a frame later, swinging the body out and back).
		var skeleton = new ClipSkeleton( [ "root" ], [ -1 ], [ Vector3.Zero ], [ Quaternion.Identity ] );
		var grab = new AnimationClip( "grab", 60f, 31, [ 0 ], [ 0 ], [], [ 0 ], [ 31 ],
			Enumerable.Range( 0, 31 ).Select( f => (70f + f) * Vector3.UnitX ).ToArray() );
		var dismount = new AnimationClip( "dismount", 60f, 15, [ 0 ], [ 0 ], [], [ 0 ], [ 15 ],
			Enumerable.Range( 0, 15 ).Select( f => (100f + f) * Vector3.UnitX ).ToArray() );
		var clips = new ClipSet( skeleton, [ grab, dismount ] );
		var into = new StateGraphDefinition { Name = "Into", Play = new StatePlayDefinition { Clip = "grab", BlendTime = 0f } };
		into.Transitions.Add( new StateTransitionDefinition
		{
			Target = "Off",
			Guard = new StateTestDefinition { Test = "clip-ending", Mask = 1, Within = 0.01f, Wait = true }
		} );
		var off = new StateGraphDefinition
		{
			Name = "Off",
			Active = false,
			Play = new StatePlayDefinition { Clip = "dismount", BlendTime = 0.1f, KeepPhase = true }
		};
		var player = new StateGraphPlayer( clips, new StateGraphDefinition { Name = "Root", States = [ into, off ] } );
		var pose = new SkeletonPose( 1 );
		var parameters = new AnimationParameters();
		var facts = new StateFacts();
		// Steps that reach the grab's end test 0.005 s short of its end; the run stops inside the dismount's first pass.
		const float step = 0.0099f;
		player.Update( facts, parameters, step, pose );
		var last = pose.Positions[ 0 ].X;
		for ( var frame = 0; frame < 66; frame++ )
		{
			player.Update( facts, parameters, step, pose );
			var moved = pose.Positions[ 0 ].X - last;
			last = pose.Positions[ 0 ].X;
			Assert.True( moved > 0f && moved < 2f, $"frame {frame} ({player.Graph.State}) moved {moved}" );
		}
		Assert.Equal( "Off", player.Graph.State );
	}

	[Fact]
	public void AClipRunPastItsEndIsShownAtItsEndThatFrame()
	{
		// Stepping back from a drop-in the state leaves as its clip ends but on the frame after: the frame its clip runs past
		// its end must show that end, not the clip wrapped round to its start (the body and board jumped there and back).
		var skeleton = new ClipSkeleton( [ "root" ], [ -1 ], [ Vector3.Zero ], [ Quaternion.Identity ] );
		var moving = new AnimationClip( "moving", 60f, 15, [ 0 ], [ 0 ], [], [ 0 ], [ 15 ],
			Enumerable.Range( 0, 15 ).Select( f => f * Vector3.UnitX ).ToArray() );
		var clips = new ClipSet( skeleton, [ moving, Pose( "next" ) ] );
		var back = new StateGraphDefinition { Name = "Back", Play = new StatePlayDefinition { Clip = "moving", BlendTime = 0f } };
		back.Transitions.Add( new StateTransitionDefinition
		{
			Target = "Next",
			Guard = new StateTestDefinition
			{
				Op = "and",
				Items = [ new StateTestDefinition { Test = "clip-ending", Mask = 1, Within = 0f },
					new StateTestDefinition { Test = "intent", Intent = "go", Mask = 1 } ]
			}
		} );
		var next = new StateGraphDefinition { Name = "Next", Active = false, Play = new StatePlayDefinition { Clip = "next", BlendTime = 0.1f } };
		var player = new StateGraphPlayer( clips, new StateGraphDefinition { Name = "Root", States = [ back, next ] } );
		var pose = new SkeletonPose( 1 );
		var parameters = new AnimationParameters();
		var facts = new StateFacts();
		const float step = 1f / 90f;
		// 21 frames reach 0.233 s of the 0.2333 s clip; the 22nd runs past its end.
		for ( var frame = 0; frame < 22; frame++ )
			player.Update( facts, parameters, step, pose );
		Assert.Equal( "Back", player.Graph.State );
		Assert.True( pose.Positions[ 0 ].X > 13.5f, $"shown at {pose.Positions[ 0 ].X} the frame the clip ran out" );
	}

	[Fact]
	public void ACycleThatDoesNotEndWithItsClipGoesOnRound()
	{
		// A walking cycle has no way out waiting on its clip's end: the frame it runs past its end it goes on round, not held
		// at the end for a frame (which showed as a stall and a step every cycle).
		var skeleton = new ClipSkeleton( [ "root" ], [ -1 ], [ Vector3.Zero ], [ Quaternion.Identity ] );
		var moving = new AnimationClip( "moving", 60f, 15, [ 0 ], [ 0 ], [], [ 0 ], [ 15 ],
			Enumerable.Range( 0, 15 ).Select( f => f * Vector3.UnitX ).ToArray() );
		var clips = new ClipSet( skeleton, [ moving ] );
		var cycle = new StateGraphDefinition { Name = "Cycle", Play = new StatePlayDefinition { Clip = "moving", BlendTime = 0f } };
		var player = new StateGraphPlayer( clips, new StateGraphDefinition { Name = "Root", States = [ cycle ] } );
		var pose = new SkeletonPose( 1 );
		var parameters = new AnimationParameters();
		var facts = new StateFacts();
		for ( var frame = 0; frame < 22; frame++ )
			player.Update( facts, parameters, 1f / 90f, pose );
		Assert.True( pose.Positions[ 0 ].X < 1f, $"shown at {pose.Positions[ 0 ].X} the frame the cycle went round" );
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
