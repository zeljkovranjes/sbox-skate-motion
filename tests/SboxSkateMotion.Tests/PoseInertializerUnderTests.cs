using System;
using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

// The follower given the pose under the one it follows (the clips' pose before the fits): what the pose under does
// itself goes through, and only what the fits add over it is eased.
public class PoseInertializerUnderTests
{
	private const float Step = 1f / 144f;

	[Fact]
	public void AFitHoldingABoneWhereThePoseUnderMovesAwayHoldsIt()
	{
		// A get-up: the hips are taken from the body standing still while the clip's hips under them set off at 3 m/s in a
		// frame and stop 0.1 s later. The hips shown stay where the fit holds them, fully or part way. Following only what the
		// fit changes, the change set off and stopped as sharply, was eased, and the hips swung out and back.
		foreach ( var weight in new[] { 1f, 0.6f } )
		{
			var follower = new PoseInertializer();
			var pose = new SkeletonPose( 1 );
			var under = new SkeletonPose( 1 );
			var held = new Vector3( 0f, 0f, 0.9f );
			for ( var frame = 0; frame < 144; frame++ )
			{
				var time = frame * Step;
				var clip = new Vector3( 3f * Math.Clamp( time - 0.2f, 0f, 0.1f ), 0f, 0.9f );
				var turn = Quaternion.CreateFromAxisAngle( Vector3.UnitZ, 20f * Math.Clamp( time - 0.2f, 0f, 0.1f ) );
				Set( under, clip, turn );
				var fitted = Vector3.Lerp( clip, held, weight );
				var fittedTurn = Quaternion.Slerp( turn, Quaternion.Identity, weight );
				Set( pose, fitted, fittedTurn );
				follower.Apply( pose, Step, under );
				Assert.True( Vector3.Distance( pose.Positions[ 0 ], fitted ) < 1e-4f,
					$"weight {weight} frame {frame}: {Vector3.Distance( pose.Positions[ 0 ], fitted ) * 39.37f:F2} in off" );
				Assert.True( Angle( pose.Rotations[ 0 ], fittedTurn ) < 1e-3f, $"weight {weight} frame {frame}" );
			}
		}
	}

	[Fact]
	public void TheClipsOwnKickGoesThroughAsItIs()
	{
		// A knee still, then turning at 20 rad/s from one frame to the next (a varial kickflip's front knee as the board
		// leaves the ground), with no fit changing it: shown as it is. Followed alone it is held back.
		var follower = new PoseInertializer();
		var alone = new PoseInertializer();
		var pose = new SkeletonPose( 1 );
		var under = new SkeletonPose( 1 );
		var single = new SkeletonPose( 1 );
		var lagged = 0f;
		for ( var frame = 0; frame < 72; frame++ )
		{
			var turn = Quaternion.CreateFromAxisAngle( Vector3.UnitX, frame < 20 ? 0f : 20f * (frame - 20) * Step );
			Set( under, new Vector3( 0f, 0f, 0.5f ), turn );
			Set( pose, new Vector3( 0f, 0f, 0.5f ), turn );
			Set( single, new Vector3( 0f, 0f, 0.5f ), turn );
			follower.Apply( pose, Step, under );
			alone.Apply( single, Step );
			Assert.True( Angle( pose.Rotations[ 0 ], turn ) < 1e-3f, $"frame {frame}" );
			lagged = MathF.Max( lagged, Angle( single.Rotations[ 0 ], turn ) );
		}
		Assert.True( lagged > 0.05f, $"followed alone, held back {lagged:F3} rad at most" );
	}

	[Fact]
	public void AFitMovingWithThePoseUnderCarriesItsKick()
	{
		// A fit holding a bone off the clip's by the same amount (a foot pressed onto the deck) carries a kick of the clip
		// through as it is.
		var follower = new PoseInertializer();
		var pose = new SkeletonPose( 1 );
		var under = new SkeletonPose( 1 );
		var correction = new Vector3( 0.05f, 0f, 0.02f );
		for ( var frame = 0; frame < 72; frame++ )
		{
			var place = new Vector3( 0f, frame < 20 ? 0f : 4f * (frame - 20) * Step, 0.5f );
			Set( under, place, Quaternion.Identity );
			Set( pose, place + correction, Quaternion.Identity );
			follower.Apply( pose, Step, under );
			Assert.True( Vector3.Distance( pose.Positions[ 0 ], place + correction ) < 1e-4f, $"frame {frame}" );
		}
	}

	[Fact]
	public void AFitThatJumpsIsStillEasedOver()
	{
		// The clip's pose still, a fit jumping 0.1 in a frame: eased over and settled on, as without the pose under.
		var follower = new PoseInertializer();
		var pose = new SkeletonPose( 1 );
		var under = new SkeletonPose( 1 );
		var shown = new System.Collections.Generic.List<float>();
		for ( var frame = 0; frame < 144; frame++ )
		{
			Set( under, new Vector3( 0f, 0f, 0.5f ), Quaternion.Identity );
			Set( pose, new Vector3( 0f, 0f, frame < 40 ? 0.5f : 0.6f ), Quaternion.Identity );
			follower.Apply( pose, Step, under );
			shown.Add( pose.Positions[ 0 ].Z );
		}
		Assert.True( shown[ 40 ] - 0.5f < 0.02f, $"{shown[ 40 ] - 0.5f:F3} of the jump shown in its frame" );
		for ( var frame = 41; frame < shown.Count; frame++ )
			Assert.True( shown[ frame ] <= 0.6f + 1e-5f, $"frame {frame} past the fit" );
		Assert.Equal( 0.6f, shown[ ^1 ], 3 );
	}

	[Fact]
	public void ThePoseUnderIsCarriedIntoATurnedFrameWithTheRoot()
	{
		// A root rolling at 2 m/s, its frame turned half round under it (a stance turned round), then kicked to 4 m/s the
		// next frame with no fit: the kick goes through as it is, the pose under's speed carried into the new frame.
		var follower = new PoseInertializer();
		var pose = new SkeletonPose( 1 );
		var under = new SkeletonPose( 1 );
		var turn = Quaternion.CreateFromAxisAngle( Vector3.UnitZ, MathF.PI );
		var shift = new Vector3( 0.3f, 0f, 0f );
		var place = new Vector3( 0f, 0f, 0.5f );
		for ( var frame = 0; frame < 60; frame++ )
		{
			if ( frame == 30 )
			{
				follower.Reframe( new[] { 0 }, turn, shift );
				place = Vector3.Transform( place, turn ) + shift;
			}
			// Along +X at 2 m/s in the old frame is along -X in the new; from frame 31 at 4 m/s.
			var speed = frame < 30 ? new Vector3( 2f, 0f, 0f ) : new Vector3( frame < 31 ? -2f : -4f, 0f, 0f );
			place += speed * Step;
			Set( under, place, Quaternion.Identity );
			Set( pose, place, Quaternion.Identity );
			follower.Apply( pose, Step, under );
			Assert.True( Vector3.Distance( pose.Positions[ 0 ], place ) < 1e-4f, $"frame {frame}" );
		}
	}

	private static void Set( SkeletonPose pose, Vector3 position, Quaternion rotation )
	{
		pose.Positions[ 0 ] = position;
		pose.Rotations[ 0 ] = rotation;
	}

	private static float Angle( Quaternion a, Quaternion b ) =>
		2f * MathF.Acos( Math.Clamp( MathF.Abs( Quaternion.Dot( Quaternion.Normalize( a ), Quaternion.Normalize( b ) ) ), 0f, 1f ) );
}
