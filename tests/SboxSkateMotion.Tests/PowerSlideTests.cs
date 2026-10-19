using System;
using System.Numerics;
using SkateMotion.Core;
using SkateMotion.Core.Vehicle;
using Xunit;

namespace SboxSkateMotion.Tests;

public class PowerSlideTests
{
	private const float Step = 1f / 120f;

	// Integrates only the board's yaw (heading in radians) and spin under the slide's turn while it rolls along +x.
	private static (float Heading, PowerSlide Slide) Run( PowerSlide slide, float heading, float seconds, bool held, float steer, float speed = 5f )
	{
		var spin = 0f;
		for ( var t = 0f; t < seconds; t += Step )
		{
			var body = new BoardBody( Vector3.Zero, Quaternion.CreateFromAxisAngle( Vector3.UnitZ, heading ),
				new Vector3( speed, 0f, 0f ), new Vector3( 0f, 0f, spin ), Vector3.One );
			spin += slide.Step( body, Vector3.UnitZ, steer, held, true, Step ).Z;
			heading += spin * Step;
		}
		return (heading, slide);
	}

	[Fact]
	public void HoldingSlideTurnsTheBoardAcrossItsTravelToTheSteeringSide()
	{
		var (heading, slide) = Run( new PowerSlide(), 0f, 0.8f, true, -1f );
		Assert.Equal( -1, slide.Side );
		Assert.True( slide.Sliding );
		Assert.InRange( heading, MathF.PI / 2f - 0.1f, MathF.PI / 2f + 0.1f );
		var right = Run( new PowerSlide(), 0f, 0.8f, true, 1f );
		Assert.InRange( right.Heading, -MathF.PI / 2f - 0.1f, -MathF.PI / 2f + 0.1f );
	}

	[Fact]
	public void LettingGoTurnsBackInLineAndEnds()
	{
		var slide = new PowerSlide();
		var (heading, _) = Run( slide, 0f, 0.8f, true, -1f );
		(heading, _) = Run( slide, heading, 1f, false, 0f );
		Assert.False( slide.Sliding );
		Assert.InRange( heading, -0.1f, 0.1f );
	}

	[Fact]
	public void ATapRevertsTheBoardHalfWayRound()
	{
		var slide = new PowerSlide();
		var (heading, _) = Run( slide, 0f, 0.1f, true, 1f );
		(heading, _) = Run( slide, heading, 1.2f, false, 1f );
		Assert.False( slide.Sliding );
		Assert.InRange( MathF.Abs( heading ), MathF.PI - 0.1f, MathF.PI + 0.1f );
	}

	[Fact]
	public void NoSlideWhenTooSlowOrInTheAir()
	{
		var slow = Run( new PowerSlide(), 0f, 0.5f, true, 1f, 0.5f );
		Assert.False( slow.Slide.Sliding );
		var slide = new PowerSlide();
		var body = new BoardBody( Vector3.Zero, Quaternion.Identity, new Vector3( 5f, 0f, 0f ), Vector3.Zero, Vector3.One );
		Assert.Equal( Vector3.Zero, slide.Step( body, Vector3.UnitZ, 1f, true, false, Step ) );
		Assert.False( slide.Sliding );
	}
}
