using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class FootLockTests
{
	private const float Step = 1f / 90f;

	[Fact]
	public void APlantedFootStaysWhereItLandedWhileThePoseSlidesIt()
	{
		// A landing's pose slides the planted foot back 1 in a frame: the foot stays put across the ground, at the pose's
		// own height.
		var hold = new FootLock();
		var start = hold.Update( new Vector3( 1f, 2f, 0.05f ), true, Step );
		for ( var frame = 1; frame <= 5; frame++ )
		{
			var at = hold.Update( new Vector3( 1f - 0.025f * frame, 2f, 0.05f + 0.001f * frame ), true, Step );
			Assert.Equal( start.X, at.X, 4 );
			Assert.Equal( start.Y, at.Y, 4 );
			Assert.Equal( 0.05f + 0.001f * frame, at.Z, 4 );
		}
		Assert.True( hold.Holding );
	}

	[Fact]
	public void ALiftedFootIsLetGoAndEasesBackToThePose()
	{
		var hold = new FootLock();
		hold.Update( new Vector3( 0f, 0f, 0f ), true, Step );
		hold.Update( new Vector3( -0.1f, 0f, 0f ), true, Step );
		var first = hold.Update( new Vector3( -0.1f, 0f, 0.05f ), false, Step );
		Assert.False( hold.Holding );
		// Let go, it starts from where it was held and closes on the pose, never past it.
		Assert.True( first.X < 0f && first.X > -0.1f, $"at {first.X}" );
		var at = first;
		for ( var frame = 0; frame < 60; frame++ )
		{
			var next = hold.Update( new Vector3( -0.1f, 0f, 0.05f ), false, Step );
			Assert.True( next.X <= at.X + 1e-6f && next.X >= -0.1f - 1e-6f );
			at = next;
		}
		Assert.Equal( -0.1f, at.X, 3 );
	}

	[Fact]
	public void AFootThePoseTakesOutOfReachIsLetGo()
	{
		var hold = new FootLock();
		hold.Update( Vector3.Zero, true, Step );
		hold.Update( new Vector3( -FootLock.Reach - 0.01f, 0f, 0f ), true, Step );
		Assert.False( hold.Holding );
	}

	[Fact]
	public void AFootLeftFarBehindEasesBackFromNoFurtherThanReach()
	{
		// The rider placed 10 m away while a foot is held: the foot is not left behind there to ease across.
		var hold = new FootLock();
		hold.Update( Vector3.Zero, true, Step );
		var at = hold.Update( new Vector3( 10f, 0f, 0f ), true, Step );
		Assert.False( hold.Holding );
		Assert.True( 10f - at.X <= FootLock.Reach + 1e-4f, $"at {at.X}" );
		Assert.True( at.X < 10f, $"at {at.X}" );
	}
}
