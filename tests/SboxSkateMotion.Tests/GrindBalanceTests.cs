using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class GrindBalanceTests
{
	private const float Frame = 1f / 60f;

	[Fact]
	public void AStillSlideGetsTheWholeSwayAndAMovingGrindNone()
	{
		var still = Balanced( 0f );
		var moving = Balanced( 20f );
		Assert.True( still.Weight > 0.99f, $"still {still.Weight}" );
		Assert.True( still.ClipMotion < GrindBalance.StillBelow );
		Assert.True( moving.Weight < 0.01f, $"moving {moving.Weight}" );
		// Between the two, a clip that turns the torso a little gets some of it.
		var slow = Balanced( 5f );
		Assert.InRange( slow.Weight, 0.2f, 0.8f );

		static GrindBalance Balanced( float degreesPerSecond )
		{
			var balance = new GrindBalance();
			for ( var frame = 0; frame < 60 * 3; frame++ )
			{
				var angle = degreesPerSecond * frame * Frame * MathF.PI / 180f;
				balance.Watch( Quaternion.CreateFromAxisAngle( Vector3.UnitY, angle ), Frame );
				balance.Update( Frame, true );
			}
			return balance;
		}
	}

	[Fact]
	public void AQuickBlendOntoTheRailHoldsTheSwayBackOnlyAMoment()
	{
		// Coming onto the rail the torso swings 290 degrees a second for 0.2 s as the slide's clip blends in, then holds.
		var balance = new GrindBalance();
		var angle = 0f;
		for ( var frame = 0; frame < 12; frame++ )
		{
			angle += 290f * Frame * MathF.PI / 180f;
			balance.Watch( Quaternion.CreateFromAxisAngle( Vector3.UnitY, angle ), Frame );
			balance.Update( Frame, true );
		}
		Assert.True( balance.Weight < 0.01f );
		var settled = 0f;
		for ( var frame = 1; frame <= 60 && settled == 0f; frame++ )
		{
			balance.Watch( Quaternion.CreateFromAxisAngle( Vector3.UnitY, angle ), Frame );
			balance.Update( Frame, true );
			if ( balance.Weight > 0.9f )
				settled = frame * Frame;
		}
		// Within a second of the blend ending the sway is nearly all in (it took 1.3 s to start at all when bursts counted).
		Assert.InRange( settled, 0.2f, 1f );
	}

	[Fact]
	public void SwaysAtHalfToOneCorrectionASecond()
	{
		var balance = new GrindBalance();
		var (pitchTurns, rollTurns) = (0, 0);
		var (lastPitch, lastRoll) = (balance.Pitch, balance.Roll);
		for ( var frame = 0; frame < 60 * 40; frame++ )
		{
			balance.Update( Frame, true );
			if ( MathF.Sign( balance.Pitch ) != MathF.Sign( lastPitch ) )
				pitchTurns++;
			if ( MathF.Sign( balance.Roll ) != MathF.Sign( lastRoll ) )
				rollTurns++;
			(lastPitch, lastRoll) = (balance.Pitch, balance.Roll);
			Assert.InRange( balance.Pitch, -1f, 1f );
			Assert.InRange( balance.Roll, -1f, 1f );
		}
		// Each wave crosses zero twice a cycle: 0.45 to 1 a second over 40 s.
		Assert.InRange( pitchTurns / 80f, 0.45f, 1f );
		Assert.InRange( rollTurns / 80f, 0.45f, 1f );
	}

	[Fact]
	public void TheSwayNeverJumpsOnOrOffTheRail()
	{
		var balance = new GrindBalance();
		var (pitch, roll) = (0f, 0f);
		var largest = 0f;
		for ( var frame = 0; frame < 60 * 20; frame++ )
		{
			balance.Watch( Quaternion.Identity, Frame );
			balance.Update( Frame, frame % 150 < 100 );
			var (nowPitch, nowRoll) = (balance.Pitch * balance.Weight, balance.Roll * balance.Weight);
			largest = MathF.Max( largest, MathF.Max( MathF.Abs( nowPitch - pitch ), MathF.Abs( nowRoll - roll ) ) );
			(pitch, roll) = (nowPitch, nowRoll);
		}
		// Easing out over 0.3 s moves at most about 0.08 of the sway a frame and the waves themselves about 0.07; a jump
		// would be the whole of it.
		Assert.True( largest < 0.15f, $"largest step {largest}" );
	}

	[Fact]
	public void EasesInOnTheRailAndOutLeavingIt()
	{
		var balance = new GrindBalance();
		void Run( bool onRail, float seconds )
		{
			for ( var time = 0f; time < seconds - 1e-4f; time += Frame )
			{
				balance.Watch( Quaternion.Identity, Frame );
				balance.Update( Frame, onRail );
			}
		}
		Run( false, 2f );
		Assert.Equal( 0f, balance.Weight );
		Run( true, Frame );
		Assert.True( balance.Weight < 0.01f );
		Run( true, GrindBalance.EaseIn );
		Assert.True( balance.Weight > 0.99f );
		Run( false, GrindBalance.EaseOut + Frame );
		Assert.Equal( 0f, balance.Weight );
	}

	[Fact]
	public void BadInputChangesNothing()
	{
		var balance = new GrindBalance();
		for ( var frame = 0; frame < 120; frame++ )
		{
			balance.Watch( Quaternion.Identity, Frame );
			balance.Update( Frame, true );
		}
		var (weight, pitch, motion) = (balance.Weight, balance.Pitch, balance.ClipMotion);
		balance.Watch( new Quaternion( float.NaN, 0f, 0f, 1f ), Frame );
		balance.Watch( Quaternion.Identity, float.NaN );
		foreach ( var delta in new[] { 0f, -1f, float.NaN, float.PositiveInfinity } )
			balance.Update( delta, false );
		Assert.Equal( weight, balance.Weight );
		Assert.Equal( pitch, balance.Pitch );
		Assert.Equal( motion, balance.ClipMotion );
	}
}
