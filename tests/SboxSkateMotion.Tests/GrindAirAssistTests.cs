using System;
using System.Numerics;
using SkateMotion.Core.Grinding;
using Xunit;

namespace SboxSkateMotion.Tests;

public class GrindAirAssistTests
{
	private const float Step = 1f / 120f;
	private const float Gravity = 9.8f;

	// A rail along +x, its top 0.4 m up.
	private static readonly RailSegment[] Rail = { new( new Vector3( 0f, 0f, 0.4f ), new Vector3( 10f, 0f, 0.4f ) ) };

	// Flies a board (its trucks 0.1 m below its middle) from here until its trucks come down to the rail's height,
	// applying the assist each step; returns where it came down, the most it moved in a step and in all.
	private static (Vector3 Landed, float MostInAStep, float Moved) Fly( Vector3 position, Vector3 velocity )
	{
		var assist = new GrindAirAssist();
		var most = 0f;
		var moved = 0f;
		for ( var t = 0f; t < 3f && (velocity.Z > 0f || position.Z - 0.1f > 0.4f); t += Step )
		{
			var (move, change) = assist.Move( position, position.Z - 0.1f, velocity, Rail, Gravity, Step );
			most = MathF.Max( most, move.Length() );
			moved += move.Length();
			velocity += change;
			position += velocity * Step + move;
			velocity.Z -= Gravity * Step;
		}
		return (position, most, moved);
	}

	[Fact]
	public void AnOllieComingDownJustBesideARailLandsOnIt()
	{
		// Popped 0.15 m beside the rail at 4 m/s and 3.3 m/s up: it comes down on the rail's line.
		var (landed, most, moved) = Fly( new Vector3( 1f, 0.15f, 0.1f ), new Vector3( 4f, 0f, 3.3f ) );
		Assert.True( MathF.Abs( landed.Y ) < 0.01f, $"came down {landed.Y} m off the rail" );
		Assert.True( most <= GrindAirAssist.PerSecond * Step + 1e-5f );
		Assert.True( moved <= GrindAirAssist.Total + 1e-4f );
	}

	[Fact]
	public void ItTakesAwayTheSpeedAcrossTheRail()
	{
		// Drifting toward the rail as well: it comes down on the rail's line and still, not sliding over it.
		var assist = new GrindAirAssist();
		var position = new Vector3( 1f, 0.15f, 0.95f );
		var velocity = new Vector3( 4f, -0.6f, 0f );
		for ( var t = 0f; t < 1f && position.Z - 0.1f > 0.4f; t += Step )
		{
			var (move, change) = assist.Move( position, position.Z - 0.1f, velocity, Rail, Gravity, Step );
			velocity += change;
			position += velocity * Step + move;
			velocity.Z -= Gravity * Step;
		}
		Assert.True( MathF.Abs( position.Y ) < 0.02f, $"came down {position.Y} m off the rail" );
		Assert.True( MathF.Abs( velocity.Y ) < 0.05f, $"still moving {velocity.Y} m/s across it" );
	}

	[Fact]
	public void ItWaitsForTheTopOfTheJump()
	{
		// Above the rail but still rising, most of a second from coming down: nothing yet.
		var assist = new GrindAirAssist();
		Assert.Equal( Vector3.Zero, assist.Move( new Vector3( 1f, 0.15f, 0.6f ), 0.5f, new Vector3( 4f, 0f, 3f ), Rail, Gravity, Step ).Move );
		// At the top it starts.
		Assert.NotEqual( Vector3.Zero, assist.Move( new Vector3( 2f, 0.15f, 0.95f ), 0.85f, new Vector3( 4f, 0f, 0f ), Rail, Gravity, Step ).Move );
	}

	[Fact]
	public void RailsOutOfReachBehindOrAcrossAreLeftAlone()
	{
		var top = new Vector3( 4f, 0f, 0f );
		// Too far to the side for one jump's move.
		Assert.Equal( Vector3.Zero, new GrindAirAssist().Move( new Vector3( 2f, 0.5f, 0.95f ), 0.85f, top, Rail, Gravity, Step ).Move );
		// Coming down past the rail's end.
		Assert.Equal( Vector3.Zero, new GrindAirAssist().Move( new Vector3( 11f, 0.1f, 0.95f ), 0.85f, top, Rail, Gravity, Step ).Move );
		// Travelling across the rail rather than along it.
		Assert.Equal( Vector3.Zero, new GrindAirAssist().Move( new Vector3( 2f, 0.1f, 0.95f ), 0.85f, new Vector3( 2f, 3f, 0f ), Rail, Gravity, Step ).Move );
		// Already below the rail's height.
		Assert.Equal( Vector3.Zero, new GrindAirAssist().Move( new Vector3( 2f, 0.1f, 0.3f ), 0.2f, top, Rail, Gravity, Step ).Move );
	}

	[Fact]
	public void OneJumpMovesAtMostItsTotalUntilReset()
	{
		var assist = new GrindAirAssist();
		var moved = 0f;
		for ( var i = 0; i < 200; i++ )
			moved += assist.Move( new Vector3( 2f, 0.19f, 0.95f ), 0.85f, new Vector3( 4f, 0f, 0f ), Rail, Gravity, Step ).Move.Length();
		Assert.True( moved <= GrindAirAssist.Total + 1e-4f );
		Assert.Equal( Vector3.Zero, assist.Move( new Vector3( 2f, 0.19f, 0.95f ), 0.85f, new Vector3( 4f, 0f, 0f ), Rail, Gravity, Step ).Move );
		assist.Reset();
		Assert.NotEqual( Vector3.Zero, assist.Move( new Vector3( 2f, 0.15f, 0.95f ), 0.85f, new Vector3( 4f, 0f, 0f ), Rail, Gravity, Step ).Move );
	}
}
