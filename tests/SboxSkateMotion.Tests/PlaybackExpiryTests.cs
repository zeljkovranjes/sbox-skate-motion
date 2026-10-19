using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class PlaybackExpiryTests
{
	[Theory]
	[InlineData( .01f, false, false, .01f, true, true )]
	[InlineData( .02f, false, false, .01f, true, false )]
	[InlineData( 0f, true, true, .01f, true, false )]
	[InlineData( 0f, true, false, .01f, false, true )]
	[InlineData( float.NaN, false, true, .01f, true, true )]
	[InlineData( float.NaN, false, false, .01f, true, true )]
	[InlineData( float.PositiveInfinity, false, false, .01f, true, false )]
	public void PlaybackExpiryPreservesTransitionGateAndFinishedFlag( float remaining, bool transitioning,
		bool finished, float within, bool wait, bool expected )
	{
		Assert.Equal( expected, new AnimationPlaybackStatus( remaining, transitioning, finished ).ExpiresWithin( within, wait ) );
	}

	[Fact]
	public void SuppliedPlaybackOverridesStateAgeForExpiry()
	{
		var graph = new StateGraph( Definition() );
		var facts = new StateFacts();
		facts.Intents[ "start" ] = 1f;
		graph.Update( facts, .01f );
		graph.ClipLength = .2f;
		graph.Update( facts, 10f, new AnimationPlaybackStatus( .005f, true ) );
		Assert.EndsWith( "Into", graph.State );
		graph.Update( facts, 0f, new AnimationPlaybackStatus( .005f, false ) );
		Assert.EndsWith( "Cycle", graph.State );
	}

	[Theory]
	[InlineData( 30 )]
	[InlineData( 60 )]
	[InlineData( 90 )]
	[InlineData( 144 )]
	public void IntroDoesNotWrapBeforeExpiryAcrossClockOffsets( int rate )
	{
		var skeleton = new ClipSkeleton( [ "root" ], [ -1 ], [ Vector3.Zero ], [ Quaternion.Identity ] );
		var intro = new AnimationClip( "intro", 30f, 7, [ 0 ], [ 0 ], [], [ 0 ], [ 7 ],
			[ Vector3.Zero, Vector3.UnitX, Vector3.UnitX * 2f, Vector3.UnitX * 3f,
				Vector3.UnitX * 4f, Vector3.UnitX * 5f, Vector3.UnitX * 6f ] );
		var hold = new AnimationClip( "hold", 30f, 2, [ 0 ], [ 0 ], [], [ 0 ], [ 1 ], [ Vector3.UnitX * 6f ] );
		var idle = new AnimationClip( "idle", 30f, 2, [ 0 ], [ 0 ], [], [ 0 ], [ 1 ], [ Vector3.Zero ] );
		var clips = new ClipSet( skeleton, [ intro, hold, idle ] );
		for ( var offset = 1; offset <= 100; offset++ )
		{
			var player = new StateGraphPlayer( clips, Definition() );
			var facts = new StateFacts();
			var parameters = new AnimationParameters();
			var pose = new SkeletonPose( 1 );
			for ( var frame = 0; frame < offset; frame++ )
				player.Update( facts, parameters, 1f / rate, pose );
			facts.Intents[ "start" ] = 1f;
			var previous = 0f;
			for ( var frame = 0; frame < rate; frame++ )
			{
				player.Update( facts, parameters, 1f / rate, pose );
				Assert.True( pose.Positions[ 0 ].X >= previous - .0001f,
					$"Pose wrapped at rate {rate}, offset {offset}, frame {frame}" );
				previous = pose.Positions[ 0 ].X;
			}
			Assert.EndsWith( "Cycle", player.Graph.State );
			Assert.Equal( 6f, pose.Positions[ 0 ].X, 4 );
		}
	}

	private static StateGraphDefinition Definition() => new()
	{
		Name = "Root",
		States =
		{
			new()
			{
				Name = "Action", Guard = new() { Test = "intent", Intent = "start" },
				States =
				{
					new()
					{
						Name = "Into", Interruptible = "false", Play = new() { Clip = "intro", BlendTime = .2f },
						Transitions = { new() { Target = "Cycle", Guard = new() { Test = "clip-ending", Within = .01f } } }
					},
					new() { Name = "Cycle", Active = false, Interruptible = "false", Play = new() { Clip = "hold", BlendTime = .2f } }
				}
			},
			new() { Name = "Idle", Play = new() { Clip = "idle", BlendTime = 0f } }
		}
	};
}
