using System.Numerics;
using SkateMotion.Core.Constraints;
using Xunit;

namespace SboxSkateMotion.Tests;

public class SweptContactTests
{
	[Theory]
	[InlineData( 0f, 0f )]
	[InlineData( 0.013f, 0f )]
	[InlineData( -0.013f, 0.004f )]
	public void SweepTravelDoesNotBrakeAnAlreadyRollingWheel( float forwardTravel, float lateralTravel )
	{
		const float radius = 0.0285f;
		const float speed = 5.675f;
		const float step = 1f / 240f;
		var rotation = Quaternion.CreateFromAxisAngle( Vector3.UnitY, 0.3f );
		var normal = Vector3.Transform( Vector3.UnitZ, rotation );
		var forward = Vector3.Transform( Vector3.UnitX, rotation );
		var axle = Vector3.UnitY;
		var wheel = new DynamicBody
		{
			Position = normal * radius, Rotation = rotation,
			Velocity = forward * speed, AngularVelocity = axle * (speed / radius),
			Acceleration = -normal * 9.81f,
			InverseMass = 1f, WorldInverseInertia = Matrix4x4.CreateScale( 1000f )
		};
		var surface = new DynamicBody { Rotation = Quaternion.Identity };
		var hit = forward * forwardTravel + axle * lateralTravel;
		var geometry = new CollisionContact( 0, 1, Vector3.Zero, hit, normal, forward, axle,
			0.8f, 0.6f, 0f ).WithProjectedSurfacePoint();
		ContactConstraint[] contacts = [ContactCompiler.Compile( wheel, surface, geometry, step )];
		SolverBody[] workspace = [new() { InverseMass = 1f }, new()];
		ConstraintSolver.Solve( workspace, contacts, [], [], 25 );
		var integration = new BodyDynamicsSettings( new Vector3( 1000f ), 0f, 0f, 100f, 1000f, 1f, 0f, 0 );
		BodyDynamics.Step( ref wheel, ref workspace[ 0 ], integration, step, Vector3.Zero );
		var pointVelocity = wheel.Velocity + Vector3.Cross( wheel.AngularVelocity, -normal * radius );
		Assert.InRange( pointVelocity.Length(), 0f, 0.0001f );
		Assert.InRange( Vector3.Distance( wheel.Velocity, forward * speed ), 0f, 0.0001f );
		Assert.InRange( Math.Abs( Vector3.Dot( wheel.AngularVelocity, axle ) - speed / radius ), 0f, 0.0001f );
	}
}
