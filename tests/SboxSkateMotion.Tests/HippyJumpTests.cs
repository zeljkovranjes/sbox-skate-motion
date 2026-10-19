using System;
using System.Numerics;
using SkateMotion.Core.Vehicle;
using Xunit;

namespace SboxSkateMotion.Tests;

public class HippyJumpTests
{
	private const float Step = 1f / 120f;
	private const float Gravity = 9.8f;

	private static HippyJump.Outcome Hold( HippyJump jump, float seconds, Vector3 board )
	{
		for ( var t = 0f; t < seconds; t += Step )
			jump.Step( true, board, true, Gravity, Step );
		return jump.Step( false, board, true, Gravity, Step );
	}

	// Flies until the jump ends; returns how it ended, the highest the rider rose and how long it was off the board.
	private static (HippyJump.Outcome Outcome, float Apex, float Time) Fly( HippyJump jump, Func<float, Vector3> board, bool grounded = true )
	{
		var apex = 0f;
		for ( var t = 0f; t < 3f; t += Step )
		{
			var outcome = jump.Step( false, board( t ), grounded, Gravity, Step );
			apex = MathF.Max( apex, jump.Offset.Z );
			if ( outcome != HippyJump.Outcome.None )
				return (outcome, apex, t);
		}
		return (HippyJump.Outcome.None, apex, 3f);
	}

	[Fact]
	public void ALongerCrouchJumpsHigherWithinTheMeasuredRange()
	{
		var quick = new HippyJump();
		Assert.Equal( HippyJump.Outcome.Launched, Hold( quick, 0.05f, Vector3.Zero ) );
		var low = Fly( quick, _ => Vector3.Zero ).Apex;
		var full = new HippyJump();
		Hold( full, 1f, Vector3.Zero );
		var high = Fly( full, _ => Vector3.Zero ).Apex;
		Assert.InRange( low, 0.35f, 0.42f );
		Assert.InRange( high, HippyJump.HighApex - HippyJump.Standing - 0.02f, HippyJump.HighApex - HippyJump.Standing + 0.02f );
	}

	[Fact]
	public void TheRiderLandsBackOnABoardRollingSteadilyBeneath()
	{
		var jump = new HippyJump();
		var rolling = new Vector3( 5f, 0f, 0f );
		Hold( jump, 0.3f, rolling );
		Assert.True( jump.Flying );
		var (outcome, _, time) = Fly( jump, _ => rolling );
		Assert.Equal( HippyJump.Outcome.Landed, outcome );
		Assert.InRange( time, 0.5f, 0.8f );
		Assert.False( jump.Flying );
	}

	[Fact]
	public void ABoardStoppedUnderTheJumpIsMissed()
	{
		var jump = new HippyJump();
		Hold( jump, 0.3f, new Vector3( 5f, 0f, 0f ) );
		Assert.Equal( HippyJump.Outcome.Missed, Fly( jump, _ => Vector3.Zero ).Outcome );
	}

	[Fact]
	public void ABoardDroppingAwayIsFollowedDown()
	{
		var jump = new HippyJump();
		Hold( jump, 0.3f, new Vector3( 3f, 0f, 0f ) );
		// Rolling off a ledge just after the jump, the board falls for a moment and rolls on lower down: the rider comes
		// down after it, landing later than it would have on the flat.
		var flat = new HippyJump();
		Hold( flat, 0.3f, new Vector3( 3f, 0f, 0f ) );
		var onFlat = Fly( flat, _ => new Vector3( 3f, 0f, 0f ) );
		var dropping = Fly( jump, t => new Vector3( 3f, 0f, t is > 0.1f and < 0.3f ? -Gravity * (t - 0.1f) : 0f ) );
		Assert.Equal( HippyJump.Outcome.Landed, dropping.Outcome );
		Assert.True( dropping.Time > onFlat.Time + 0.05f );
	}

	[Fact]
	public void NoJumpWithoutReleaseOrOffTheGround()
	{
		var jump = new HippyJump();
		for ( var i = 0; i < 30; i++ )
			Assert.Equal( HippyJump.Outcome.None, jump.Step( true, Vector3.Zero, true, Gravity, Step ) );
		Assert.True( jump.Crouching );
		Assert.Equal( HippyJump.Outcome.None, jump.Step( false, Vector3.Zero, false, Gravity, Step ) );
		Assert.False( jump.Flying );
	}

	[Fact]
	public void TimeToLandCountsDownToTouchdown()
	{
		var jump = new HippyJump();
		Hold( jump, 0.3f, Vector3.Zero );
		var predicted = jump.TimeToLand;
		var (_, _, time) = Fly( jump, _ => Vector3.Zero );
		Assert.InRange( time, predicted - 0.03f, predicted + 0.03f );
	}
}
