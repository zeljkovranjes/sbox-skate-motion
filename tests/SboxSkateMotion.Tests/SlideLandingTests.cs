using System;
using System.Numerics;
using SkateMotion.Core;
using SkateMotion.Core.Grinding;
using SkateMotion.Core.Vehicle;
using Xunit;

namespace SboxSkateMotion.Tests;

public class SlideLandingTests
{
	private const float Step = 1f / 120f;
	private const float Gravity = 9.8f;

	// A rail along +x, its top 0.4 m up.
	private static readonly RailSegment[] Rail = { new( new Vector3( 0f, 0f, 0.4f ), new Vector3( 10f, 0f, 0.4f ) ) };

	[Theory]
	[InlineData( 60f, true, 1f )]
	[InlineData( -60f, true, -1f )]
	[InlineData( 20f, true, -1f )]
	[InlineData( 60f, false, -1f )]
	public void ABoardTurnedNearerAcrossARailThanAlongItsTravelLinesUpAcrossTheRail( float degrees, bool rail, float sign )
	{
		// Travelling along the rail, turned 60 degrees in the air: coming down on the rail it turns on round to lie across
		// it (a slide); turned only 20 degrees, or with no rail below, it turns back in line with its travel.
		var turn = Quaternion.CreateFromAxisAngle( Vector3.UnitZ, degrees * MathF.PI / 180f );
		var body = new BoardBody( new Vector3( 1f, 0.05f, 0.9f ), turn, new Vector3( 4f, 0f, -1f ), Vector3.Zero, Vector3.One );
		var change = new AirControl().Step( body, 0f, Gravity, Step, AirControl.MaximumRate, rail ? Vector3.UnitX : null );
		Assert.Equal( sign, MathF.Sign( change.Z ) );
	}

	[Theory]
	[InlineData( 0.1f, 0f )]
	[InlineData( 0.28f, 0.36f )]
	[InlineData( -0.27f, -0.36f )]
	public void ABoardComingDownAcrossARailIsMovedSoTheNearestOfItsMiddleNoseOrTailLandsOnIt( float beside, float landed )
	{
		// Turned across the rail and coming down this far beside it, the board is moved so the rail comes under its
		// middle (a boardslide) or, farther out, under its nose or tail (a noseslide or tailslide).
		var position = new Vector3( 2f, beside, 0.95f );
		var velocity = new Vector3( 4f, 0f, 0f );
		var assist = new GrindAirAssist();
		for ( var t = 0f; t < 3f && position.Z - 0.1f > 0.4f; t += Step )
		{
			var (move, change) = assist.Move( position, position.Z - 0.1f, velocity, Rail, Gravity, Step, GrindAirAssist.Total, Vector3.UnitY );
			velocity += change;
			position += velocity * Step + move;
			velocity.Z -= Gravity * Step;
		}
		Assert.InRange( position.Y, landed - 0.02f, landed + 0.02f );
	}

	[Fact]
	public void ABoardStillTurningIsMovedForTheWayItWillComeDown()
	{
		// Turned only 20 degrees but turning fast enough to be lined up across the rail: it is moved for a slide on its
		// end, not for a grind on its middle.
		var position = new Vector3( 2f, 0.15f, 0.95f );
		var velocity = new Vector3( 4f, 0f, 0f );
		var heading = new Vector3( MathF.Cos( 0.35f ), MathF.Sin( 0.35f ), 0f );
		var (move, _) = new GrindAirAssist().Move( position, position.Z - 0.1f, velocity, Rail, Gravity, Step,
			GrindAirAssist.Total, heading, 5f );
		var (still, _) = new GrindAirAssist().Move( position, position.Z - 0.1f, velocity, Rail, Gravity, Step,
			GrindAirAssist.Total, heading );
		// Its tail would come down just past the rail, so it is moved back a little (away from the rail), where a grind
		// would move its middle onto the rail.
		Assert.True( move.Y > 0f );
		Assert.True( still.Y < 0f );
	}

	[Fact]
	public void TheLandingRailIsKeptForTheRestOfTheJump()
	{
		var assist = new GrindAirAssist();
		Assert.Equal( Vector3.UnitX, assist.Landing( new Vector3( 2f, 0.2f, 1.2f ), 1.1f, new Vector3( 4f, 0f, 1f ), Rail, Gravity ) );
		// Just above the rail and still to its side, the move could no longer reach it, but the board comes down there.
		Assert.Null( GrindAirAssist.LandingRail( new Vector3( 2.5f, 0.3f, 0.52f ), 0.42f, new Vector3( 4f, 0f, -2f ), Rail, Gravity ) );
		Assert.Equal( Vector3.UnitX, assist.Landing( new Vector3( 2.5f, 0.3f, 0.52f ), 0.42f, new Vector3( 4f, 0f, -2f ), Rail, Gravity ) );
		assist.Reset();
		Assert.Null( assist.Landing( new Vector3( 2.5f, 0.3f, 0.52f ), 0.42f, new Vector3( 4f, 0f, -2f ), Rail, Gravity ) );
	}

	[Fact]
	public void ABoardAlongTheRailIsStillMovedOntoItsLine()
	{
		var position = new Vector3( 2f, 0.15f, 0.95f );
		var velocity = new Vector3( 4f, 0f, 0f );
		var assist = new GrindAirAssist();
		for ( var t = 0f; t < 3f && position.Z - 0.1f > 0.4f; t += Step )
		{
			var (move, change) = assist.Move( position, position.Z - 0.1f, velocity, Rail, Gravity, Step, GrindAirAssist.Total, Vector3.UnitX );
			velocity += change;
			position += velocity * Step + move;
			velocity.Z -= Gravity * Step;
		}
		Assert.InRange( position.Y, -0.02f, 0.02f );
	}

	[Fact]
	public void TheLandingRailIsTheOneTheFlightComesDownBeside()
	{
		Assert.Equal( Vector3.UnitX, GrindAirAssist.LandingRail( new Vector3( 2f, 0.2f, 1.2f ), 1.1f, new Vector3( 4f, 0f, 1f ), Rail, Gravity ) );
		// Too far to the side to reach, or heading across it, or past its end.
		Assert.Null( GrindAirAssist.LandingRail( new Vector3( 2f, 1.5f, 1.2f ), 1.1f, new Vector3( 4f, 0f, 1f ), Rail, Gravity ) );
		Assert.Null( GrindAirAssist.LandingRail( new Vector3( 2f, 0.2f, 1.2f ), 1.1f, new Vector3( 1f, 3f, 1f ), Rail, Gravity ) );
		Assert.Null( GrindAirAssist.LandingRail( new Vector3( 11f, 0.2f, 1.2f ), 1.1f, new Vector3( 4f, 0f, 1f ), Rail, Gravity ) );
	}
}
