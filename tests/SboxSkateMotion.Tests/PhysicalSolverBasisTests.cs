using System.Numerics;
using SkateMotion.Core.Animation;
using SkateMotion.Core.Constraints;
using SkateMotion.Core.Physics;
using Xunit;

namespace SboxSkateMotion.Tests;

public class PhysicalSolverBasisTests
{
	[Fact]
	public void PhysicalAnchorUsesStoredAxesInsteadOfRebuildingFromOrientation()
	{
		var physical = new PhysicalBodyState
		{
			Pose = new( new LimbFrame( Vector3.UnitY, -Vector3.UnitX, Vector3.UnitZ ), Vector3.Zero ),
			Orientation = Vector4.UnitW,
			MotionMode = 4,
			InverseMass = 1f,
			InverseInertiaDiagonal = Vector3.One
		};
		var body = PhysicalSolverBody.Read( physical );
		var geometry = new DriveGeometry( 0, 1, new( 2f, 3f, 4f ), new( 5f, 6f, 7f ),
			Quaternion.Identity, Quaternion.Identity );
		var settings = new DriveSettings( DriveMode.Position, 0f, 0f, 100f );
		var drive = DriveCompiler.Compile( body, body, geometry, settings, settings, .1f );
		Assert.Equal( new Vector3( -3f, 2f, 4f ), drive.Pair.OffsetA );
		Assert.Equal( new Vector3( -6f, 5f, 7f ), drive.Pair.OffsetB );
	}

	[Fact]
	public void ChangingOrientationInvalidatesAxesFromThePreviousPose()
	{
		var body = new DynamicBody { Rotation = Quaternion.Identity, RotationBasis = Matrix4x4.Identity };
		body.Rotation = Quaternion.CreateFromAxisAngle( Vector3.UnitZ, .5f );
		Assert.Null( body.RotationBasis );
	}

	[Fact]
	public void IntegratedAxesSurvivePhysicalWritebackAndReadWithoutRecalculation()
	{
		var body = new DynamicBody { Rotation = Quaternion.CreateFromYawPitchRoll( .3f, -.2f, .4f ), InverseMass = 1f };
		var workspace = new SolverBody { AngularVelocity = new( .01f, -.02f, .03f ) };
		var settings = new BodyDynamicsSettings( Vector3.One, 0f, 0f, 100f, 100f, 1f, 1f, 4 );
		BodyDynamics.Step( ref body, ref workspace, settings, .01f, Vector3.Zero );
		Assert.NotNull( body.RotationBasis );
		var physical = new PhysicalBodyState();
		PhysicalSolverBody.WriteDynamics( physical, body );
		var restored = PhysicalSolverBody.Read( physical );
		Assert.Equal( body.Rotation, restored.Rotation );
		Assert.Equal( body.RotationBasis, restored.RotationBasis );
	}
}
