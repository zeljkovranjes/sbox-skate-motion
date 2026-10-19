using System;
using System.Numerics;
using SkateMotion.Core;
using SkateMotion.Core.Vehicle;
using Xunit;

namespace SboxSkateMotion.Tests;

public class BodyFlipTests
{
	private const float Step = 1f / 120f;

	// Integrates the deck's roll about its length (x) under the flip for a flight of the given length; returns the roll.
	private static float Fly( BodyFlip flip, float flight, Func<float, float> lean, bool grab = true, bool goofy = false )
	{
		var roll = 0f;
		var spin = 0f;
		for ( var t = 0f; t < flight; t += Step )
		{
			var body = new BoardBody( Vector3.Zero, Quaternion.CreateFromAxisAngle( Vector3.UnitX, roll ), Vector3.Zero,
				new Vector3( spin, 0f, 0f ), Vector3.One );
			spin += flip.Step( body, lean( t ), grab, goofy, flight - t, Step ).X;
			roll += spin * Step;
		}
		return roll;
	}

	[Fact]
	public void AHeldFlipComesRoundOnceByTheLanding()
	{
		var flip = new BodyFlip();
		var roll = Fly( flip, 1f, _ => 1f );
		Assert.Equal( 1, flip.Direction );
		Assert.InRange( MathF.Abs( roll ), 2f * MathF.PI - 0.1f, 2f * MathF.PI + 0.1f );
	}

	[Fact]
	public void FrontAndBackFlipTurnOppositeWaysAndGoofyMirrors()
	{
		var front = Fly( new BodyFlip(), 1f, _ => 1f );
		var back = Fly( new BodyFlip(), 1f, _ => -1f );
		var goofyFront = Fly( new BodyFlip(), 1f, _ => 1f, goofy: true );
		Assert.True( front > 0f );
		Assert.True( back < 0f );
		Assert.True( goofyFront < 0f );
	}

	[Fact]
	public void LettingGoStopsTheTurnAfterItsShortestTime()
	{
		var tapped = Fly( new BodyFlip(), 1f, t => t < 0.05f ? 1f : 0f );
		var held = Fly( new BodyFlip(), 1f, _ => 1f );
		Assert.True( tapped > 0.5f );
		Assert.True( tapped < held * 0.7f );
	}

	[Fact]
	public void NoFlipWithoutAGrabOrEnoughAir()
	{
		Assert.Equal( 0f, Fly( new BodyFlip(), 1f, _ => 1f, grab: false ), 3 );
		var flip = new BodyFlip();
		var body = new BoardBody( Vector3.Zero, Quaternion.Identity, Vector3.Zero, Vector3.Zero, Vector3.One );
		Assert.Equal( Vector3.Zero, flip.Step( body, 1f, true, false, BodyFlip.MinimumAir * 0.5f, Step ) );
		Assert.False( flip.Flipping );
	}

	[Fact]
	public void FlightLeftMatchesABallisticArc()
	{
		// Rising at 3 m/s from the take-off height comes back down after 2 * 3 / g.
		Assert.Equal( 6f / 9.8f, BodyFlip.FlightLeft( 0f, 3f, 9.8f ), 3 );
		Assert.True( BodyFlip.FlightLeft( 1f, 0f, 9.8f ) > 0.4f );
	}
}
