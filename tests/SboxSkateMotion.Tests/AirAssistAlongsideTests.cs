using System;
using System.Numerics;
using SkateMotion.Core.Grinding;
using Xunit;

namespace SboxSkateMotion.Tests;

public class AirAssistAlongsideTests
{
	private const float Step = 1f / 240f;
	private const float Gravity = 9.8f;

	// A rail along +x, its top 0.4 m up.
	private static readonly RailSegment[] Rail = { new( new Vector3( 0f, 0f, 0.4f ), new Vector3( 10f, 0f, 0.4f ) ) };

	[Fact]
	public void TheMoveSaysHowFarItComesDownShortOfTheRail()
	{
		// 0.3 m beside the rail with 0.2 m of move in the jump: it closes 0.2 of it and comes down 0.1 short.
		var assist = new GrindAirAssist();
		var (move, _) = assist.Move( new Vector3( 1f, 0.3f, 1.1f ), 1f, new Vector3( 4f, 0f, 0f ), Rail, Gravity, Step, 0.2f );
		Assert.NotEqual( Vector3.Zero, move );
		Assert.Equal( 0.1f, assist.Short, 3 );
		Assert.Equal( 0.2f, assist.Covering, 3 );
		// Within reach of the move, nothing is left over.
		var near = new GrindAirAssist();
		near.Move( new Vector3( 1f, 0.15f, 1.1f ), 1f, new Vector3( 4f, 0f, 0f ), Rail, Gravity, Step, 0.2f );
		Assert.Equal( 0f, near.Short );
	}

	[Fact]
	public void TheLockOnSteersOnlyWhatTheMoveLeavesIt()
	{
		// Falling toward the rail 0.08 m beside it: steered across all of it, across part of it, or none of it when the
		// move closes the whole way.
		var position = new Vector3( 1f, 0.08f, 0.7f );
		var velocity = new Vector3( 4f, 0f, -0.5f );
		var all = GrindAssist.VelocityChange( position, 0.6f, velocity, Rail, Gravity, Step );
		var part = GrindAssist.VelocityChange( position, 0.6f, velocity, Rail, Gravity, Step, covered: 0.05f );
		var none = GrindAssist.VelocityChange( position, 0.6f, velocity, Rail, Gravity, Step, covered: 0.08f );
		Assert.True( all.Y < 0f, $"steered {all.Y} toward the rail" );
		Assert.True( part.Y < 0f && part.Y > all.Y, $"steered {part.Y} for the rest, {all.Y} for all" );
		Assert.Equal( 0f, none.Y, 5 );
	}
}
