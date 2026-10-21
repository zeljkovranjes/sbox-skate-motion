using System;
using System.Numerics;
using SkateMotion.Core.Physics;
using Xunit;

namespace SboxSkateMotion.Tests;

public class BoardThrowTests
{
	[Fact]
	public void ATossGoesOnAheadAndUpWithTheRidersOwnSpeed()
	{
		// Facing along +Y and walking along +X: the toss adds its own forward and upward speed to the walk.
		var velocity = BoardThrow.Toss( new Vector3( 0f, 2f, 0.5f ), new Vector3( 1.5f, 0f, 0f ) );
		Assert.Equal( 1.5f, velocity.X, 4 );
		Assert.Equal( BoardThrow.TossForward, velocity.Y, 4 );
		Assert.Equal( BoardThrow.TossUp, velocity.Z, 4 );
		// Facing straight up or down there is no level way to throw: it goes forward along +X.
		Assert.Equal( BoardThrow.TossForward, BoardThrow.Toss( Vector3.UnitZ, Vector3.Zero ).X, 4 );
	}

	[Fact]
	public void AHeldBoardCarriesOnAsTheHandMovesIt()
	{
		var motion = new HeldBoardMotion();
		var turn = Quaternion.CreateFromAxisAngle( Vector3.UnitY, 3f / 60f );
		var rotation = Quaternion.Identity;
		for ( var frame = 0; frame < 30; frame++ )
		{
			motion.Add( new Vector3( 2f, 0f, -1f ) * (frame / 60f), rotation, 1f / 60f );
			rotation = Quaternion.Normalize( turn * rotation );
		}
		Assert.Equal( 2f, motion.Velocity.X, 2 );
		Assert.Equal( -1f, motion.Velocity.Z, 2 );
		Assert.Equal( 3f, motion.Spin.Y, 2 );
		motion.Reset();
		Assert.Equal( Vector3.Zero, motion.Velocity );
		// The first pose after a reset only starts the measure again.
		motion.Add( new Vector3( 50f, 0f, 0f ), Quaternion.Identity, 1f / 60f );
		Assert.Equal( Vector3.Zero, motion.Velocity );
	}

	[Fact]
	public void OneUnevenFrameDoesNotDecideTheThrow()
	{
		var motion = new HeldBoardMotion();
		for ( var frame = 0; frame < 30; frame++ )
			motion.Add( new Vector3( frame / 60f, 0f, 0f ), Quaternion.Identity, 1f / 60f );
		// A frame whose pose jumped a metre (a hitch) moves the measure only part of the way.
		motion.Add( new Vector3( 30f / 60f + 1f, 0f, 0f ), Quaternion.Identity, 1f / 60f );
		Assert.InRange( motion.Velocity.X, 1f, 60f * 0.5f );
	}

	[Fact]
	public void ACalledBoardIsQuickCloseByAndAboutASecondFromTheFarthest()
	{
		Assert.Equal( 0.13f, BoardReach.FlightSeconds( 0f ), 3 );
		Assert.Equal( 1f, BoardReach.FlightSeconds( BoardReach.Farthest ), 3 );
		Assert.Equal( 1f, BoardReach.FlightSeconds( 500f ), 3 );
		var previous = 0f;
		for ( var distance = 0.5f; distance <= BoardReach.Farthest; distance += 0.5f )
		{
			var seconds = BoardReach.FlightSeconds( distance );
			Assert.True( seconds > previous );
			previous = seconds;
		}
	}

	[Fact]
	public void TheFlightArcsOverTheGroundAndEndsInTheHand()
	{
		var from = new Vector3( 10f, 0f, 0f );
		var to = new Vector3( 0f, 0f, 1f );
		Assert.Equal( from, BoardReach.Along( from, to, 0f ) );
		Assert.Equal( to, BoardReach.Along( from, to, 1f ) );
		var middle = BoardReach.Along( from, to, 0.5f );
		// Halfway, it is above the straight way between the two, by its arc.
		Assert.True( middle.Z > 0.5f + 1f );
		Assert.True( BoardReach.Along( from, to, 0.5f ).Z <= 0.5f + 1.5f );
		// It leaves the ground and arrives at the hand without a jolt: no speed of its own at either end.
		const float step = 1e-4f;
		Assert.True( Vector3.Distance( BoardReach.Along( from, to, step ), from ) / step < 0.05f );
		Assert.True( Vector3.Distance( BoardReach.Along( from, to, 1f - step ), to ) / step < 0.05f );
	}
}
