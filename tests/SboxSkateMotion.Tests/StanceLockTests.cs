using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class StanceLockTests
{
	private const float Step = 1f / 60f;

	private static Vector3[] Feet( Vector3 left, Vector3 right ) => new[] { left, right };

	[Fact]
	public void APlantedFootStaysWhereItWasPutDown()
	{
		// A run's pose sweeps the planted foot back 1 cm a frame under a body going faster: it stays put across the ground,
		// at the pose's own height, and the swinging foot goes where the pose has it.
		var hold = new StanceLock();
		var planted = new[] { true, false };
		hold.Update( Feet( new Vector3( 0.1f, 0.1f, 0f ), new Vector3( -0.2f, -0.1f, 0.15f ) ), planted, Step );
		for ( var frame = 1; frame <= 6; frame++ )
		{
			var swing = new Vector3( -0.2f + 0.05f * frame, -0.1f, 0.15f );
			var feet = Feet( new Vector3( 0.1f + 0.01f * frame, 0.1f, 0.002f * frame ), swing );
			hold.Update( feet, planted, Step );
			Assert.Equal( 0.1f, feet[ 0 ].X, 4 );
			Assert.Equal( 0.1f, feet[ 0 ].Y, 4 );
			Assert.Equal( 0.002f * frame, feet[ 0 ].Z, 4 );
			Assert.Equal( swing, feet[ 1 ] );
		}
		Assert.True( hold.Holding( 0 ) );
		Assert.Equal( 1, hold.Held );
	}

	[Fact]
	public void APoseTakingAHeldFootPastSlipStepsItOver()
	{
		// Settling into a stand, the pose puts a planted foot 15 cm away: the foot is lifted and put down there rather than
		// slid across the ground, and is held where it lands.
		var hold = new StanceLock();
		var planted = new[] { false, true };
		var swing = new Vector3( 0f, 0.2f, 0.1f );
		hold.Update( Feet( swing, Vector3.Zero ), planted, Step );
		var target = new Vector3( 0.15f, 0f, 0f );
		var feet = Feet( swing, target );
		hold.Update( feet, planted, Step );
		Assert.True( hold.Stepping( 1 ) );
		Assert.Equal( 0f, feet[ 1 ].X, 4 );
		var before = 0f;
		var highest = 0f;
		var frames = 0;
		while ( hold.Stepping( 1 ) && frames++ < 60 )
		{
			feet = Feet( swing, target );
			hold.Update( feet, planted, Step );
			Assert.True( feet[ 1 ].X >= before - 1e-6f && feet[ 1 ].X <= 0.15f + 1e-6f, $"frame {frames}: {feet[ 1 ].X}" );
			Assert.True( feet[ 1 ].Z >= -1e-6f && feet[ 1 ].Z <= StanceLock.StepLift + 1e-6f, $"frame {frames}: {feet[ 1 ].Z}" );
			before = feet[ 1 ].X;
			highest = MathF.Max( highest, feet[ 1 ].Z );
		}
		Assert.Equal( 0.15f, feet[ 1 ].X, 4 );
		Assert.Equal( 0f, feet[ 1 ].Z, 4 );
		Assert.True( highest > StanceLock.StepLift * 0.8f, $"highest {highest}" );
		Assert.True( frames * Step <= StanceLock.StepTime + Step + 1e-4f, $"{frames} frames" );
		// Down, it is held where it landed.
		var after = Feet( swing, new Vector3( 0.17f, 0f, 0f ) );
		hold.Update( after, planted, Step );
		Assert.True( hold.Holding( 1 ) );
		Assert.Equal( 0.15f, after[ 1 ].X, 4 );
	}

	[Fact]
	public void WhileOneFootStepsTheOtherIsDraggedAlong()
	{
		// Setting off from a stand, the body goes on before either foot lifts: both are held, one steps over, and the other
		// is kept no further than Slip behind the pose until that step is done.
		var hold = new StanceLock();
		var both = new[] { true, true };
		hold.Update( Feet( new Vector3( 0f, 0.1f, 0f ), new Vector3( 0f, -0.1f, 0f ) ), both, Step );
		Assert.Equal( 2, hold.Held );
		for ( var frame = 1; frame <= 8; frame++ )
		{
			var pose = Feet( new Vector3( 0.02f * frame, 0.1f, 0f ), new Vector3( 0.02f * frame, -0.1f, 0f ) );
			var feet = (Vector3[])pose.Clone();
			hold.Update( feet, both, Step );
			Assert.False( hold.Stepping( 0 ) && hold.Stepping( 1 ), $"frame {frame}: both stepping" );
			for ( var foot = 0; foot < 2; foot++ )
			{
				if ( hold.Stepping( foot ) )
					continue;
				var behind = pose[ foot ].X - feet[ foot ].X;
				Assert.True( behind >= -1e-5f && behind <= StanceLock.Slip + 1e-5f, $"frame {frame} foot {foot}: {behind}" );
			}
		}
		Assert.True( hold.Stepping( 0 ) != hold.Stepping( 1 ) );
	}

	[Fact]
	public void UnplantedFeetGoWhereThePoseHasThem()
	{
		var hold = new StanceLock();
		var neither = new[] { false, false };
		for ( var frame = 0; frame < 5; frame++ )
		{
			var pose = Feet( new Vector3( 0.03f * frame, 0.1f, 0.1f ), new Vector3( -0.03f * frame, -0.1f, 0.1f ) );
			var shown = (Vector3[])pose.Clone();
			hold.Update( shown, neither, Step );
			Assert.Equal( pose, shown );
		}
		Assert.Equal( 0, hold.Held );
	}

	[Fact]
	public void AFootLetGoOfGoesBackToThePoseOverRelease()
	{
		var hold = new StanceLock();
		hold.Update( Feet( Vector3.Zero, new Vector3( 0f, -0.2f, 0.2f ) ), new[] { true, false }, Step );
		hold.Update( Feet( new Vector3( -0.05f, 0f, 0f ), new Vector3( 0f, -0.2f, 0.2f ) ), new[] { true, false }, Step );
		// Lifted: from where it was held it closes on the pose, never past it, and is on it once Release has gone by.
		var before = 0f;
		for ( var frame = 1; frame * Step <= StanceLock.Release + 1e-4f; frame++ )
		{
			var feet = Feet( new Vector3( -0.05f, 0f, 0.03f ), new Vector3( 0f, -0.2f, 0f ) );
			hold.Update( feet, new[] { false, false }, Step );
			Assert.True( feet[ 0 ].X <= before + 1e-6f && feet[ 0 ].X >= -0.05f - 1e-6f, $"frame {frame}: {feet[ 0 ].X}" );
			before = feet[ 0 ].X;
		}
		Assert.Equal( -0.05f, before, 4 );
		Assert.Equal( 0, hold.Held );
	}

	[Fact]
	public void AFootPlantedAgainWhileGoingBackIsHeldWhereItIsShown()
	{
		var hold = new StanceLock();
		hold.Update( Feet( Vector3.Zero, new Vector3( 0.3f, 0f, 0.1f ) ), new[] { true, false }, Step );
		hold.Update( Feet( new Vector3( -0.06f, 0f, 0f ), new Vector3( 0.3f, 0f, 0.1f ) ), new[] { true, false }, Step );
		var lifted = Feet( new Vector3( -0.06f, 0f, 0.02f ), new Vector3( 0.3f, 0f, 0.1f ) );
		hold.Update( lifted, new[] { false, false }, Step );
		var shown = lifted[ 0 ].X;
		Assert.True( shown > -0.06f + 1e-3f && shown < 0f, $"at {shown}" );
		// Down again a frame later, it is held from where it was shown, not snapped to the pose or back to the old hold.
		var again = Feet( new Vector3( -0.06f, 0f, 0f ), new Vector3( 0.3f, 0f, 0.1f ) );
		hold.Update( again, new[] { true, false }, Step );
		Assert.True( again[ 0 ].X < shown + 1e-6f && again[ 0 ].X > -0.06f + 1e-3f, $"at {again[ 0 ].X}" );
		Assert.True( hold.Holding( 0 ) );
	}
}
