using System.Collections.Generic;
using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class ClipLoopingTests
{
	[Theory]
	[InlineData( false, true, 10f )]
	[InlineData( true, true, 2.5f )]
	[InlineData( true, false, 10f )]
	public void ClipPermissionAndCallerBothControlWrapping( bool permitted, bool requested, float expected )
	{
		var skeleton = Skeleton();
		var clip = Clip( "clip", permitted );
		var pose = new SkeletonPose( 1 );
		pose.Sample( clip, skeleton, 1.25f, requested );
		Assert.Equal( expected, pose.Positions[ 0 ].X, 4 );
	}

	[Fact]
	public void MixedBlendKeepsIndependentLeafLooping()
	{
		var skeleton = Skeleton();
		var clips = new ClipSet( skeleton, [ Clip( "once", false ), Clip( "loop", true ) ] );
		clips.AddBlends( new Dictionary<string, BlendDefinition> { [ "blend" ] = new() { Parameter = "mix", Children =
			[ new() { Name = "once", At = 0f }, new() { Name = "loop", At = 1f } ] } } );
		var parameters = new AnimationParameters();
		parameters.Set( "mix", .5f );
		var pose = new SkeletonPose( 1 );
		pose.Sample( clips.Find( "blend" ), skeleton, 1.25f, true, parameters );
		Assert.Equal( 6.25f, pose.Positions[ 0 ].X, 4 );
	}

	[Fact]
	public void NonloopingLeafHoldsEndpointWhileStateFadeStillActive()
	{
		var skeleton = Skeleton();
		var clips = new ClipSet( skeleton, [ Clip( "once", false ) ] );
		clips.AddBlends( new Dictionary<string, BlendDefinition> { [ "blend" ] = new() { Children = [ new() { Name = "once" } ] } } );
		var graph = new StateGraphDefinition { Name = "Root", Play = new() { Clip = "blend", BlendTime = 2f } };
		var player = new StateGraphPlayer( clips, graph );
		var pose = new SkeletonPose( 1 );
		var parameters = new AnimationParameters();
		var facts = new StateFacts();
		var previous = 0f;
		for ( var frame = 0; frame < 210; frame++ )
		{
			player.Update( facts, parameters, 1f / 90f, pose );
			Assert.True( pose.Positions[ 0 ].X >= previous - .0001f );
			previous = pose.Positions[ 0 ].X;
		}
		Assert.Equal( 10f, pose.Positions[ 0 ].X, 4 );
	}

	[Fact]
	public void BlendKeepsItsPhaseWhenItsWeightsChangeItsLength()
	{
		// A one-second and a two-second cycle; after several cycles the weights move to the longer one.
		var skeleton = Skeleton();
		var clips = new ClipSet( skeleton, [ Clip( "short", true ), Clip( "long", true, 2f ) ] );
		clips.AddBlends( new Dictionary<string, BlendDefinition> { [ "blend" ] = new() { Parameter = "mix", Children =
			[ new() { Name = "short", At = 0f }, new() { Name = "long", At = 1f } ] } } );
		var player = new StateGraphPlayer( clips, new StateGraphDefinition { Name = "Root", Play = new() { Clip = "blend" } } );
		var pose = new SkeletonPose( 1 );
		var parameters = new AnimationParameters();
		var facts = new StateFacts();
		for ( var frame = 0; frame < 53; frame++ )
			player.Update( facts, parameters, 0.1f, pose );
		Assert.Equal( 2f, pose.Positions[ 0 ].X, 3 );

		// Both clips stay at the same point of their cycle (a fifth of the way) rather than jumping ahead.
		parameters.Set( "mix", 1f );
		player.Update( facts, parameters, 0.0001f, pose );
		Assert.Equal( 2f, pose.Positions[ 0 ].X, 1 );
	}

	private static ClipSkeleton Skeleton() => new( [ "root" ], [ -1 ], [ Vector3.Zero ], [ Quaternion.Identity ] );

	private static AnimationClip Clip( string name, bool looping, float duration = 1f ) =>
		new( name, duration, 2, [ 0 ], [ 0 ], [], [ 0 ], [ 2 ], [ Vector3.Zero, Vector3.UnitX * 10f ] ) { Looping = looping };
}
