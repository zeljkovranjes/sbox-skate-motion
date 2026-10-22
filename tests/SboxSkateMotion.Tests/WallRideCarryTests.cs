using System.Numerics;
using SkateMotion.Core;
using SkateMotion.Core.Vehicle;
using Xunit;

namespace SboxSkateMotion.Tests;

public class WallRideCarryTests
{
	private const float Step = 1f / 120f;
	private const float Gravity = 9.81f;

	// A wall 0.3 m ahead along +x, facing back along -x, a metre up where the board meets it.
	private static readonly WallSight Wall = new( new Vector3( 0.3f, 0f, 1f ), -Vector3.UnitX, 1f );

	private static BoardBody Body( Vector3 velocity ) =>
		new( Vector3.Zero, Quaternion.Identity, velocity, Vector3.Zero, Vector3.One );

	// Turned to the wall in the air, then onto it, meeting it with this much speed up (or down).
	private static WallRide OnTheWall( float up, out Vector3 hold )
	{
		var ride = new WallRide();
		ride.Air( Body( new Vector3( 1.5f, 5f, 0f ) ), Wall, Step );
		hold = ride.OnSurface( Body( new Vector3( 0f, 5f, up ) ), -Vector3.UnitX, Step, Gravity ).Hold;
		return ride;
	}

	[Fact]
	public void ARideGoesOnFallingUpToJustUnderTwoMetresASecond()
	{
		var ride = OnTheWall( 0f, out _ );
		Assert.True( ride.Riding );
		ride.OnSurface( Body( new Vector3( 0f, 5f, -1.5f ) ), -Vector3.UnitX, Step, Gravity );
		Assert.True( ride.Riding );
		ride.OnSurface( Body( new Vector3( 0f, 5f, -1.95f ) ), -Vector3.UnitX, Step, Gravity );
		Assert.False( ride.Riding );
	}

	[Fact]
	public void TheWallCarriesHalfTheBoardsWeight()
	{
		var ride = OnTheWall( 0f, out _ );
		var hold = Vector3.Zero;
		for ( var t = 0f; t < WallRide.Catch + 0.02f; t += Step )
			hold = ride.OnSurface( Body( new Vector3( 0f, 5f, -0.5f ) ), -Vector3.UnitX, Step, Gravity ).Hold;
		Assert.True( ride.Riding );
		Assert.Equal( Gravity * WallRide.Carry, hold.Z, 3 );
		// Still pressed to the wall.
		Assert.True( hold.X > 0f );
	}

	[Fact]
	public void AsTheRideStartsTheWallTakesAwayTheFallItMetItWith()
	{
		OnTheWall( -1f, out var falling );
		OnTheWall( 0f, out var level );
		Assert.Equal( Gravity * WallRide.Carry, level.Z, 3 );
		// The metre a second of fall taken away over the catch, on top of the carried weight.
		Assert.True( falling.Z > level.Z + 1f / WallRide.Catch * 0.9f );
	}

	[Theory]
	[InlineData( 0f )]
	[InlineData( 0.4f )]
	[InlineData( -1f )]
	public void ARideArcsDownTheWallForAboutHalfASecond( float up )
	{
		// Met level, rising a little, or falling: the ride lasts about half a second, the board slowing its fall at the
		// start and then falling ever faster down the wall at half the weight (not hanging there) until it slides off.
		var ride = OnTheWall( up, out var hold );
		var velocity = up;
		var seconds = 0f;
		var falling = 0;
		var steps = 0;
		while ( ride.Riding && seconds < 2f )
		{
			var before = velocity;
			velocity += (hold.Z - Gravity) * Step;
			seconds += Step;
			// Past the catch, every step falls faster than the one before.
			if ( seconds > WallRide.Catch )
			{
				steps++;
				if ( velocity < before )
					falling++;
			}
			hold = ride.OnSurface( Body( new Vector3( 0f, 5f, velocity ) ), -Vector3.UnitX, Step, Gravity ).Hold;
		}
		Assert.InRange( seconds, 0.35f, 0.6f );
		Assert.True( velocity < -WallRide.SlideDown * 0.9f );
		Assert.Equal( steps, falling );
	}

	[Fact]
	public void ARideSlowedBelowTwoMetresASecondAlongTheWallEnds()
	{
		var ride = OnTheWall( 0f, out _ );
		ride.OnSurface( Body( new Vector3( 0f, 1.5f, 0f ) ), -Vector3.UnitX, Step, Gravity );
		Assert.False( ride.Riding );
	}
}
