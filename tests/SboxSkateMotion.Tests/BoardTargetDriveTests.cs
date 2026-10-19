using System.Numerics;
using SkateMotion.Core;
using SkateMotion.Core.Constraints;
using SkateMotion.Core.Vehicle;
using Xunit;

namespace SboxSkateMotion.Tests;

public class BoardTargetDriveTests
{
	[Fact]
	public void OrientationTransitionClearsLinearDriveAndReleaseClearsBoth()
	{
		var drive = new BoardTargetDrive();
		drive.FollowPose( 0.5f );
		Assert.True( drive.Enabled );
		Assert.Equal( DriveMode.Spring, drive.Linear.Mode );
		Assert.Equal( 2500f, drive.Linear.Strength );
		Assert.Equal( 100f, drive.Linear.Damping );
		drive.FollowOrientation( 0.25f );
		Assert.Equal( 0f, drive.Linear.MaximumAcceleration );
		Assert.Equal( 5000f, drive.Angular.Strength );
		drive.Release();
		Assert.False( drive.Enabled );
		Assert.Equal( 0f, drive.Linear.MaximumAcceleration );
		Assert.Equal( 0f, drive.Angular.MaximumAcceleration );
	}

	[Fact]
	public void ChannelChangesPreserveTheOtherChannelAndEnableFlag()
	{
		var drive = new BoardTargetDrive();
		drive.FollowSpeed();
		var angular = drive.Angular;
		drive.SetLinearSpring( 2f );
		Assert.Equal( angular, drive.Angular );
		drive.ReleaseAngular();
		Assert.True( drive.Enabled );
		Assert.Equal( 10000f, drive.Linear.Strength );
		Assert.Equal( 0f, drive.Angular.MaximumAcceleration );
	}

	[Fact]
	public void BoardMovesTowardSeparateTargetAndReleaseStopsCorrection()
	{
		var board = CreateBoard();
		var target = new Vector3( 0.1f, 0f, 0.05f );
		board.SetTargetFrame( target, Quaternion.Identity );
		board.TargetDrive.FollowPose( 0.25f );
		const float step = 1f / 240f;
		for ( var i = 0; i < 120; i++ )
		{
			board.BeginStep();
			board.Step( [], 0f, 0f, step );
		}
		Assert.InRange( Vector3.Distance( board.Deck.Position, target ), 0f, 0.01f );
		Assert.Equal( target, board.TargetBody.Position );
		Assert.Equal( 0f, board.TargetBody.InverseMass );
		Assert.InRange( board.MaximumAnchorError(), 0f, 0.01f );
		board.TargetDrive.Release();
		board.SetTargetFrame( new Vector3( 10f, 10f, 10f ), Quaternion.Identity );
		var before = board.Deck.Velocity;
		board.BeginStep();
		board.Step( [], 0f, 0f, step );
		Assert.InRange( Vector3.Distance( before - Vector3.UnitZ * (9.81f * step), board.Deck.Velocity ), 0f, 0.01f );
	}

	[Fact]
	public void OrientationDriveRotatesDeckWithoutHoldingItInAir()
	{
		var board = CreateBoard();
		var target = Quaternion.CreateFromAxisAngle( Vector3.UnitY, 0.15f );
		board.SetTargetFrame( Vector3.Zero, target );
		board.TargetDrive.FollowOrientation( 0.25f );
		for ( var i = 0; i < 120; i++ )
		{
			board.BeginStep();
			board.Step( [], 0f, 0f, 1f / 240f );
		}
		Assert.InRange( MathF.Abs( Quaternion.Dot( target, board.Deck.Rotation ) ), 0.999f, 1.00001f );
		Assert.True( board.Deck.Position.Z < -1.1f );
		Assert.Equal( Vector3.Zero, board.TargetBody.Position );
		Assert.Equal( target, board.TargetBody.Rotation );
	}

	private static ArticulatedBoard CreateBoard()
	{
		var settings = new SkateSettings();
		Vector3[] wheels =
		[
			new( 0.26f, 0.095f, -0.04f ), new( 0.26f, -0.095f, -0.04f ),
			new( -0.26f, 0.095f, -0.04f ), new( -0.26f, -0.095f, -0.04f )
		];
		return new ArticulatedBoard( settings, wheels, 0.0285f );
	}
}
