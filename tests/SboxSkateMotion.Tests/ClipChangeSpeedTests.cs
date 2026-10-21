using System;
using System.Linq;
using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class ClipChangeSpeedTests
{
	[Theory]
	[InlineData( 60 )]
	[InlineData( 90 )]
	[InlineData( 144 )]
	public void SpeedingUpAtAClipChangeIsTakenUpOverTheFade( int rate )
	{
		// A clip moving at 1 in/s hands over to one moving at 10 in/s from where the first ended.
		var skeleton = new ClipSkeleton( [ "root" ], [ -1 ], [ Vector3.Zero ], [ Quaternion.Identity ] );
		var slow = Clip( "slow", 0f, 1f );
		var fast = Clip( "fast", 1f, 10f );
		var player = new StateGraphPlayer( new ClipSet( skeleton, [ slow, fast ] ), new StateGraphDefinition
		{
			Name = "Root",
			States =
			{
				new()
				{
					Name = "Slow", Play = new() { Clip = "slow", BlendTime = 0f },
					Transitions = { new() { Target = "Fast", Guard = new() { Test = "clip-ending", Within = .01f, Wait = false } } }
				},
				new() { Name = "Fast", Active = false, Play = new() { Clip = "fast", BlendTime = .2f } }
			}
		} );
		var facts = new StateFacts();
		var parameters = new AnimationParameters();
		var pose = new SkeletonPose( 1 );
		var step = 1f / rate;
		var speeds = new float[ rate * 2 ];
		var change = -1;
		var previous = 0f;
		for ( var frame = 0; frame < speeds.Length; frame++ )
		{
			player.Update( facts, parameters, step, pose );
			speeds[ frame ] = (pose.Positions[ 0 ].X - previous) / step;
			previous = pose.Positions[ 0 ].X;
			if ( change < 0 && player.ClipName == "fast" )
				change = frame;
			Assert.True( speeds[ frame ] > -0.0001f, $"moved back at frame {frame}" );
		}
		Assert.True( change > 1 );
		// The 9 in/s more is taken up over frames, never more than 60% of it in one, and the new clip's speed is reached
		// within the fade.
		for ( var frame = change; frame < change + rate / 5; frame++ )
			Assert.True( MathF.Abs( speeds[ frame ] - speeds[ frame - 1 ] ) < 0.6f * 9f,
				$"speed {speeds[ frame - 1 ]:0.0} to {speeds[ frame ]:0.0} at frame {frame - change} after the change" );
		Assert.True( speeds.Skip( change + rate / 5 ).Take( rate / 10 ).All( speed => MathF.Abs( speed - 10f ) < 0.5f ) );
	}

	// One second at 30 frames a second, from a start moving at a steady speed (inches per second).
	private static AnimationClip Clip( string name, float start, float speed )
	{
		var keys = Enumerable.Range( 0, 31 ).Select( frame => Vector3.UnitX * (start + speed * frame / 30f) ).ToArray();
		return new AnimationClip( name, 30f, 31, [ 0 ], [ 0 ], [], [ 0 ], [ 31 ], keys );
	}
}
