using System;
using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class ClipChangeCarryTests
{
	private const float Step = 1f / 60f;

	[Fact]
	public void ABoneMovingIntoAStillClipCarriesItsSpeedAndSettlesOnIt()
	{
		// A bone moving at 30 a second is handed to a clip that holds it still just ahead: it carries on at the speed it
		// had rather than stopping in a frame (an arm swinging up through a pop, as a flip leaves the ground), its speed
		// changes smoothly, and it is on the new clip as the fade ends.
		var player = Player();
		var pose = new SkeletonPose( 1 );
		var facts = new StateFacts();
		var parameters = new AnimationParameters();
		for ( var frame = 0; frame < 18; frame++ )
			player.Update( facts, parameters, Step, pose );
		var before = pose.Positions[ 0 ].X;
		player.Update( facts, parameters, Step, pose );
		var speed = (pose.Positions[ 0 ].X - before) / Step;
		facts.Intents[ "stop" ] = 1f;
		var previous = pose.Positions[ 0 ].X;
		for ( var frame = 0; frame < 30; frame++ )
		{
			player.Update( facts, parameters, Step, pose );
			var now = (pose.Positions[ 0 ].X - previous) / Step;
			if ( frame == 0 )
				Assert.True( now > speed * 0.5f, $"{now:F1} a second the frame after the change, {speed:F1} before" );
			Assert.True( MathF.Abs( now - speed ) < 15f, $"speed {speed:F1} to {now:F1} at frame {frame} after the change" );
			speed = now;
			previous = pose.Positions[ 0 ].X;
		}
		Assert.Equal( 10f, pose.Positions[ 0 ].X, 3 );
	}

	// One bone moving along x at 30 a second for a second, then (on "stop") a clip holding it at 10, faded over 0.2 s.
	private static StateGraphPlayer Player()
	{
		var skeleton = new ClipSkeleton( [ "root" ], [ -1 ], [ Vector3.Zero ], [ Quaternion.Identity ] );
		var keys = new Vector3[ 31 ];
		for ( var frame = 0; frame < keys.Length; frame++ )
			keys[ frame ] = Vector3.UnitX * frame;
		var move = new AnimationClip( "move", 30f, 31, [ 0 ], [ 0 ], [], [ 0 ], [ 31 ], keys ) { Looping = false };
		var still = new AnimationClip( "still", 30f, 2, [ 0 ], [ 0 ], [], [ 0 ], [ 1 ], [ Vector3.UnitX * 10f ] );
		var definition = new StateGraphDefinition
		{
			Name = "Root",
			States =
			{
				new()
				{
					Name = "Move", Play = new() { Clip = "move", BlendTime = 0f },
					Transitions = { new() { Target = "Still", Guard = new() { Test = "intent", Intent = "stop" } } }
				},
				new() { Name = "Still", Active = false, Play = new() { Clip = "still", BlendTime = .2f } }
			}
		};
		return new StateGraphPlayer( new ClipSet( skeleton, [ move, still ] ), definition );
	}
}
