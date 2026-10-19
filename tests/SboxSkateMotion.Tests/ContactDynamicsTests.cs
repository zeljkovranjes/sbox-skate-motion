using System.Numerics;
using SkateMotion.Core.Constraints;
using Xunit;

namespace SboxSkateMotion.Tests;

public class ContactDynamicsTests
{
	[Theory]
	[InlineData( 30 )]
	[InlineData( 60 )]
	[InlineData( 120 )]
	public void ReconstructedContactsCatchFallingWheelAndSupportItsWeight( int frequency )
	{
		var surface = new DynamicBody { Rotation = Quaternion.Identity };
		var wheel = new DynamicBody
		{
			Position = Vector3.UnitZ, Rotation = Quaternion.Identity,
			InverseMass = 1f, WorldInverseInertia = Matrix4x4.CreateScale( 1000f )
		};
		var gravity = new Vector3( 0f, 0f, -9.81f );
		var integration = new BodyDynamicsSettings( new Vector3( 1000f ), 0f, 0f, 100f, 1000f, 1f, 0f, 0 );
		SolverBody[] workspace = [new() { InverseMass = 1f }, new()];
		var contacts = new ContactConstraint[ 1 ];
		var step = 1f / frequency;
		for ( var tick = 0; tick < frequency * 4; tick++ )
		{
			wheel.Acceleration = gravity;
			var point = wheel.Position - Vector3.UnitZ * 0.05f;
			var contact = new CollisionContact( 0, 1, point, new Vector3( point.X, point.Y, 0f ),
				Vector3.UnitZ, Vector3.UnitX, Vector3.UnitY, 0.8f, 0.6f, 0f );
			contacts[ 0 ] = ContactCompiler.Compile( wheel, surface, contact, step );
			ConstraintSolver.Solve( workspace, contacts, [], [], 25 );
			BodyDynamics.Step( ref wheel, ref workspace[ 0 ], integration, step, gravity );
			Assert.InRange( wheel.Position.Z, 0.0499f, 1.0001f );
		}
		Assert.InRange( Math.Abs( wheel.Position.Z - 0.05f ), 0f, 0.0001f );
		Assert.InRange( wheel.Velocity.Length(), 0f, 0.0001f );
	}

	[Theory]
	[InlineData( 30 )]
	[InlineData( 60 )]
	[InlineData( 120 )]
	public void MovingSurfaceTransfersMotionThroughContactFriction( int frequency )
	{
		var surface = new DynamicBody { Rotation = Quaternion.Identity, Velocity = Vector3.UnitX };
		var wheel = new DynamicBody
		{
			Position = new Vector3( 0f, 0f, 0.05f ), Rotation = Quaternion.Identity,
			InverseMass = 1f, WorldInverseInertia = Matrix4x4.CreateScale( 1000f )
		};
		var gravity = new Vector3( 0f, 0f, -9.81f );
		var integration = new BodyDynamicsSettings( new Vector3( 1000f ), 0f, 0f, 100f, 1000f, 1f, 0f, 0 );
		SolverBody[] workspace = [new() { InverseMass = 1f }, new()];
		var contacts = new ContactConstraint[ 1 ];
		var step = 1f / frequency;
		for ( var tick = 0; tick < frequency * 4; tick++ )
		{
			wheel.Acceleration = gravity;
			var point = wheel.Position - Vector3.UnitZ * 0.05f;
			var contact = new CollisionContact( 0, 1, point, new Vector3( point.X, point.Y, 0f ),
				Vector3.UnitZ, Vector3.UnitX, Vector3.UnitY, 0.8f, 0.6f, 0f );
			contacts[ 0 ] = ContactCompiler.Compile( wheel, surface, contact, step );
			ConstraintSolver.Solve( workspace, contacts, [], [], 25 );
			BodyDynamics.Step( ref wheel, ref workspace[ 0 ], integration, step, gravity );
		}
		var pointVelocity = wheel.Velocity + Vector3.Cross( wheel.AngularVelocity, -Vector3.UnitZ * 0.05f );
		Assert.InRange( (pointVelocity - surface.Velocity).Length(), 0f, 0.001f );
		Assert.InRange( wheel.Position.X, 0.1f, 4.1f );
		Assert.InRange( Math.Abs( wheel.Position.Z - 0.05f ), 0f, 0.0001f );
	}
}
