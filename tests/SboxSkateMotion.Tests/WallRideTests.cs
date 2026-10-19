using System;
using System.Numerics;
using SkateMotion.Core;
using SkateMotion.Core.Vehicle;
using Xunit;

namespace SboxSkateMotion.Tests;

public class WallRideTests
{
	private const float Step = 1f / 120f;

	// A wall at x = 2 facing back along -x, one metre up from the floor where the board will meet it.
	private static readonly WallSight Wall = new( new Vector3( 2f, 0f, 1f ), -Vector3.UnitX, 0.8f );

	private static BoardBody Body( Vector3 position, Quaternion rotation, Vector3 velocity, Vector3 spin ) =>
		new( position, rotation, velocity, spin, Vector3.One );

	// Flies the board's attitude toward the wall for the given time under the wall ride's turn; returns its up axis.
	private static (Vector3 Up, Vector3 Lift) Approach( WallRide ride, Vector3 velocity, float seconds )
	{
		var rotation = Quaternion.CreateFromAxisAngle( Vector3.UnitZ, MathF.Atan2( velocity.Y, velocity.X ) );
		var spin = Vector3.Zero;
		var position = new Vector3( 1.2f, 0f, 1f );
		var lift = Vector3.Zero;
		for ( var t = 0f; t < seconds; t += Step )
		{
			var (turn, given) = ride.Air( Body( position, rotation, velocity, spin ), Wall, Step );
			lift += given;
			spin += turn;
			if ( spin.LengthSquared() > 0f )
				rotation = Quaternion.Normalize( Quaternion.CreateFromAxisAngle( Vector3.Normalize( spin ), spin.Length() * Step ) * rotation );
			position += velocity * Step;
		}
		return (Vector3.Transform( Vector3.UnitZ, rotation ), lift);
	}

	[Fact]
	public void ABoardHeadingForAWallAtAGlancingAngleTurnsItsWheelsToIt()
	{
		var ride = new WallRide();
		var (up, lift) = Approach( ride, new Vector3( 2f, 6f, 0f ), 0.35f );
		Assert.True( ride.Active );
		Assert.True( Vector3.Dot( up, -Vector3.UnitX ) > 0.9f );
		Assert.Equal( WallRide.Boost( 0.8f ), lift.Z, 3 );
	}

	[Fact]
	public void HeadOnOrLowOrSlowWallsAreNotRidden()
	{
		Assert.False( Started( new Vector3( 6f, 1f, 0f ), Wall ) );
		Assert.False( Started( new Vector3( 2f, 6f, 0f ), Wall with { Height = 0.3f } ) );
		Assert.False( Started( new Vector3( 1f, 2f, 0f ), Wall ) );
		Assert.False( Started( new Vector3( 2f, 6f, 0f ), Wall with { Normal = Vector3.UnitZ } ) );
		// Coming down a steep ramp face is a landing on it, not a wall ride.
		Assert.False( Started( new Vector3( 1f, 1f, -7f ), Wall ) );
	}

	private static bool Started( Vector3 velocity, WallSight wall )
	{
		var ride = new WallRide();
		ride.Air( Body( new Vector3( 1.2f, 0f, 1f ), Quaternion.Identity, velocity, Vector3.Zero ), wall, Step );
		return ride.Active;
	}

	[Fact]
	public void OnTheWallItIsHeldThereUntilItSlowsOrTimeRunsOut()
	{
		var ride = new WallRide();
		Approach( ride, new Vector3( 2f, 6f, 0f ), 0.3f );
		var onWall = Quaternion.CreateFromAxisAngle( Vector3.UnitX, -MathF.PI / 2f );
		var body = Body( new Vector3( 1.95f, 0f, 1f ), Quaternion.Identity, new Vector3( 0f, 6f, 0f ), Vector3.Zero );
		var (hold, _, _) = ride.OnSurface( body, -Vector3.UnitX, Step );
		Assert.True( ride.Riding );
		Assert.True( hold.X > 0f );
		var away = Vector3.Zero;
		for ( var t = 0f; t < WallRide.RideTime + 0.1f; t += Step )
			away += ride.OnSurface( body, -Vector3.UnitX, Step ).Away;
		Assert.False( ride.Riding );
		// It steps off the wall once, away from it.
		Assert.Equal( WallRide.StepOff, Vector3.Dot( away, -Vector3.UnitX ), 3 );
		// Sliding down the wall ends a ride too.
		var sliding = new WallRide();
		Approach( sliding, new Vector3( 2f, 6f, 0f ), 0.3f );
		sliding.OnSurface( body, -Vector3.UnitX, Step );
		sliding.OnSurface( body with { Velocity = new Vector3( 0f, 6f, -2f ) }, -Vector3.UnitX, Step );
		Assert.False( sliding.Riding );
		// Off the wall it turns back upright, and the floor ends it.
		var leaving = ride.Air( Body( new Vector3( 1.5f, 0f, 0.5f ), onWall, new Vector3( 0f, 5f, -2f ), Vector3.Zero ), null, Step );
		Assert.NotEqual( Vector3.Zero, leaving.Spin );
		ride.OnSurface( body, Vector3.UnitZ, Step );
		Assert.False( ride.Active );
	}

	[Fact]
	public void TheLiftIsHalfAMetreASecondLowDownAndNothingFromAMetre()
	{
		Assert.Equal( 0.5f, WallRide.Boost( 0.6f ), 3 );
		Assert.Equal( 0f, WallRide.Boost( 1f ), 3 );
		Assert.Equal( 0.25f, WallRide.Boost( 0.8f ), 3 );
	}
}
