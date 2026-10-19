using System.Numerics;
using SkateMotion.Core.Constraints;
using Xunit;

namespace SboxSkateMotion.Tests;

public class DriveDynamicsTests
{
	[Theory]
	[InlineData( 30 )]
	[InlineData( 60 )]
	[InlineData( 120 )]
	public void SpringDriveSettlesDisplacedAndRotatedBody( int frequency )
	{
		var target = new DynamicBody { Rotation = Quaternion.Identity };
		var body = new DynamicBody
		{
			Position = new Vector3( 0.2f, 0.1f, 0.3f ),
			Rotation = Quaternion.CreateFromAxisAngle( Vector3.UnitY, 0.35f ),
			InverseMass = 1f, WorldInverseInertia = Matrix4x4.Identity
		};
		var geometry = new DriveGeometry( 0, 1, Vector3.Zero, Vector3.Zero, Quaternion.Identity, Quaternion.Identity );
		var spring = new DriveSettings( DriveMode.Spring, 100f, 20f, 100f );
		var integration = new BodyDynamicsSettings( Vector3.One, 0f, 0f, 100f, 100f, 1f, 0f, 0 );
		var workspaces = new SolverBody[ 2 ];
		workspaces[ 1 ].InverseMass = 1f;
		var drives = new DriveConstraint[ 1 ];
		var step = 1f / frequency;
		for ( var tick = 0; tick < frequency * 6; tick++ )
		{
			drives[ 0 ] = DriveCompiler.Compile( target, body, geometry, spring, spring, step );
			ConstraintSolver.Solve( workspaces, [], [], drives, 25 );
			BodyDynamics.Step( ref body, ref workspaces[ 1 ], integration, step, Vector3.Zero );
			Assert.True( float.IsFinite( body.Position.LengthSquared() ) );
			Assert.True( float.IsFinite( body.Rotation.LengthSquared() ) );
		}
		Assert.InRange( body.Position.Length(), 0f, 0.002f );
		Assert.InRange( body.Velocity.Length(), 0f, 0.002f );
		Assert.InRange( body.AngularVelocity.Length(), 0f, 0.002f );
		Assert.InRange( Math.Abs( Quaternion.Dot( body.Rotation, Quaternion.Identity ) ), 0.999f, 1.000001f );
	}

	[Fact]
	public void SpeedLimitedDriveAtItsTargetDoesNotCreateMotionOrNaNs()
	{
		var target = new DynamicBody { Rotation = Quaternion.Identity };
		var body = new DynamicBody
		{
			Rotation = Quaternion.Identity, InverseMass = 1f, WorldInverseInertia = Matrix4x4.Identity
		};
		var geometry = new DriveGeometry( 0, 1, Vector3.Zero, Vector3.Zero, Quaternion.Identity, Quaternion.Identity );
		var settings = new DriveSettings( DriveMode.SpeedLimited, 0f, 0f, 100f );
		var drive = DriveCompiler.Compile( target, body, geometry, settings, settings, 1f / 60f );
		SolverBody[] workspace = [new(), new() { InverseMass = 1f }];
		ConstraintSolver.Solve( workspace, [], [], new[] { drive }, 25 );
		var integration = new BodyDynamicsSettings( Vector3.One, 0f, 0f, 100f, 100f, 1f, 0f, 0 );
		BodyDynamics.Step( ref body, ref workspace[ 1 ], integration, 1f / 60f, Vector3.Zero );
		Assert.Equal( Vector3.Zero, body.Position );
		Assert.Equal( Vector3.Zero, body.Velocity );
		Assert.Equal( Vector3.Zero, body.AngularVelocity );
		Assert.Equal( Quaternion.Identity, body.Rotation );
	}

	[Theory]
	[InlineData( 30, 0.2f )]
	[InlineData( 60, 1f )]
	[InlineData( 120, 5f )]
	public void SaturatedDriveAppliesItsAccelerationCapAcrossStepAndMass( int frequency, float inverseMass )
	{
		var target = new DynamicBody { Position = new Vector3( 100f, 0f, 0f ), Rotation = Quaternion.Identity };
		var body = new DynamicBody
		{
			Rotation = Quaternion.Identity, InverseMass = inverseMass, WorldInverseInertia = Matrix4x4.Identity
		};
		var geometry = new DriveGeometry( 0, 1, Vector3.Zero, Vector3.Zero, Quaternion.Identity, Quaternion.Identity );
		var settings = new DriveSettings( DriveMode.Position, 0f, 0f, 2f );
		var step = 1f / frequency;
		var drive = DriveCompiler.Compile( target, body, geometry, settings, settings, step );
		SolverBody[] workspace = [new(), new() { InverseMass = inverseMass }];
		ConstraintSolver.Solve( workspace, [], [], new[] { drive }, 25 );
		var integration = new BodyDynamicsSettings( Vector3.One, 0f, 0f, 100f, 100f, 1f, 0f, 0 );
		BodyDynamics.Step( ref body, ref workspace[ 1 ], integration, step, Vector3.Zero );
		Assert.InRange( Math.Abs( body.Velocity.X / step - 2f ), 0f, 0.00001f );
		Assert.Equal( 0f, body.Velocity.Y );
		Assert.Equal( 0f, body.Velocity.Z );
	}
}
