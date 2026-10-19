using System.Numerics;
using SkateMotion.Core.Animation;
using SkateMotion.Core.Constraints;
using SkateMotion.Core.Physics;
using Xunit;

namespace SboxSkateMotion.Tests;

public class BoardHeldConstraintsTests
{
	[Fact]
	public void ReleaseDisablesOnlyTheSelectedDriveAndPreservesItsFrame()
	{
		var constraints = new BoardHeldConstraints();
		constraints.Activate( 0, At( 1f ), At( 3f ) );
		var retained = constraints.Frame( 0 );
		constraints.Release();
		Assert.Equal( 2, constraints.SelectedHand );
		Assert.Equal( new DriveSettings( DriveMode.SpeedLimited, 0f, 0f, 0f ), constraints.Drive( 0 ) );
		Assert.Equal( retained, constraints.Frame( 0 ) );
		constraints.Update( At( 10f ), At( 20f ) );
		constraints.Release();
		Assert.Equal( retained, constraints.Frame( 0 ) );
	}

	[Fact]
	public void OtherHandActivationRetainsThePreviousHandFrame()
	{
		var constraints = new BoardHeldConstraints();
		constraints.Activate( 0, At( 1f ), At( 4f ) );
		constraints.Release();
		constraints.Activate( 1, At( 5f ), At( 7f ) );
		Assert.Equal( new Vector3( 3f, 0f, 0f ), constraints.Frame( 0 ).BodyPosition );
		Assert.Equal( new Vector3( 2f, 0f, 0f ), constraints.Frame( 1 ).BodyPosition );
		Assert.Equal( 0f, constraints.Drive( 0 ).MaximumAcceleration );
		Assert.Equal( new DriveSettings( DriveMode.SpeedLimited, 299.9999694824219f, 0f, 17999.998046875f ), constraints.Drive( 1 ) );
		Assert.Equal( 1, constraints.SelectedHand );
	}

	[Fact]
	public void MovingAttachmentRefreshesOnlyTheSelectedFrame()
	{
		var constraints = new BoardHeldConstraints();
		constraints.Activate( 1, At( 1f ), At( 2f ) );
		constraints.Update( At( 2f ), At( 5f ) );
		Assert.Equal( new Vector3( 3f, 0f, 0f ), constraints.Frame( 1 ).BodyPosition );
		Assert.Equal( default, constraints.Frame( 0 ) );
		Assert.Equal( 1, constraints.SelectedHand );
	}

	private static LimbRigidTransform At( float x ) => LimbRigidTransform.Identity with
	{
		Translation = new Vector3( x, 0f, 0f )
	};
}
