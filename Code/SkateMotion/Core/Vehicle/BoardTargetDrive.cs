using SkateMotion.Core.Animation;
using SkateMotion.Core.Constraints;

namespace SkateMotion.Core.Vehicle;

/// <summary>Retains independent linear and angular target-drive settings across mode changes.</summary>
internal sealed class BoardTargetDrive
{
	private static DriveSettings Disabled => new( DriveMode.SpeedLimited, 0f, 0f, 0f );

	internal DriveSettings Linear { get; set; } = Disabled;
	internal DriveSettings Angular { get; set; } = Disabled;
	internal bool Enabled { get; set; }

	internal void Release()
	{
		Enabled = false;
		ReleaseLinear();
		ReleaseAngular();
	}

	internal void FollowOrientation( float scale )
	{
		Enabled = true;
		SetAngularSpring( scale );
		ReleaseLinear();
	}

	internal void FollowPose( float scale )
	{
		Enabled = true;
		SetLinearSpring( scale );
		SetAngularSpring( scale );
	}

	internal void FollowSpeed()
	{
		Enabled = true;
		Linear = new DriveSettings( DriveMode.SpeedLimited, 59.999996185302734f, 0f, 3599.99951171875f );
		SetAngularSpring( 1f );
	}

	internal void SetLinearSpring( float scale ) => Linear = Spring( scale, 5000f, 1799.999755859375f );
	internal void SetAngularSpring( float scale ) => Angular = Spring( scale, 20000f, 35999.99609375f );
	internal void ReleaseLinear() => Linear = Disabled;
	internal void ReleaseAngular() => Angular = Disabled;

	private static DriveSettings Spring( float scale, float stiffness, float maximum )
	{
		var strength = scale * stiffness;
		var damping = LimbSolverMath.MultiplyAdd( LimbSolverMath.SquareRoot( strength ), 2f, 0f );
		return new DriveSettings( DriveMode.Spring, strength, damping, maximum );
	}
}
