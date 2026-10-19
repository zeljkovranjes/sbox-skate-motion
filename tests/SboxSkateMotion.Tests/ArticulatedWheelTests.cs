using System.Numerics;
using SkateMotion.Core.Constraints;
using Xunit;

namespace SboxSkateMotion.Tests;

public class ArticulatedWheelTests
{
	[Theory]
	[InlineData( 30 )]
	[InlineData( 60 )]
	[InlineData( 120 )]
	public void AxleHoldsWheelUnderGravityWithoutStoppingItsSpin( int frequency )
	{
		var support = new DynamicBody { Rotation = Quaternion.Identity };
		var wheel = new DynamicBody
		{
			Rotation = Quaternion.Identity, InverseMass = 5f,
			WorldInverseInertia = Matrix4x4.CreateScale( 100f, 50f, 50f ),
			AngularVelocity = new Vector3( 20f, 0f, 0f )
		};
		var linear = new JointGeometry( 0, 1, Vector3.Zero, Vector3.Zero,
			Quaternion.Identity, Vector3.Zero, Vector3.Zero );
		var angular = new QuaternionJointGeometry( Quaternion.Identity, Quaternion.Identity, false, 1000f );
		var settings = new BodyDynamicsSettings( new Vector3( 100f, 50f, 50f ), 0f, 0f, 100f, 1000f, 1f, 0f, 0 );
		var bodies = new SolverBody[ 2 ];
		bodies[ 1 ].InverseMass = wheel.InverseMass;
		var joints = new JointConstraint[ 1 ];
		var step = 1f / frequency;
		var gravity = new Vector3( 0f, 0f, -9.81f );
		for ( var tick = 0; tick < frequency * 8; tick++ )
		{
			wheel.Acceleration = gravity;
			joints[ 0 ] = QuaternionJointCompiler.Compile( support, wheel, linear, angular, step );
			ConstraintSolver.Solve( bodies, [], joints, [], 25 );
			BodyDynamics.Step( ref wheel, ref bodies[ 1 ], settings, step, gravity );
			Assert.InRange( wheel.Position.Length(), 0f, 0.0001f );
			Assert.InRange( wheel.Velocity.Length(), 0f, 0.0001f );
			Assert.InRange( Math.Abs( wheel.AngularVelocity.X - 20f ), 0f, 0.001f );
			Assert.InRange( Math.Abs( wheel.Rotation.Length() - 1f ), 0f, 0.00001f );
		}
	}
}
