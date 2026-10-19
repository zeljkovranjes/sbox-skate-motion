using System.Numerics;
using SkateMotion.Core;
using Xunit;

namespace SboxSkateMotion.Tests;

public class SuspensionTests
{
	[Theory]
	[InlineData( 30 )]
	[InlineData( 60 )]
	[InlineData( 120 )]
	[InlineData( 240 )]
	public void TiltedThinDeckSettlesOnFourWheels( int rate )
	{
		var settings = new SkateSettings();
		var physics = new SkatePhysics( settings );
		var contacts = new WheelContact[ 4 ];
		var forces = new WheelForce[ 4 ];
		var body = new BoardBody( new( 0f, 0f, 0.095f ),
			Quaternion.CreateFromYawPitchRoll( 0f, 0.02f, 0.03f ), default, default,
			new( 0.3268f, 4.2708f, 4.5893f ) );
		var dt = 1f / rate;
		PhysicsStep result = default;
		for ( var frame = 0; frame < rate * 5; frame++ )
		{
			for ( var i = 0; i < 4; i++ )
			{
				var origin = body.LocalPoint( SkateWheels.Offset( i, settings ) );
				var distance = origin.Z / body.Up.Z;
				contacts[ i ] = new( true, origin - body.Up * distance, Vector3.UnitZ, default, distance );
			}
			result = physics.Step( body, contacts, default, dt, forces );
			var force = result.Force;
			var torque = result.Torque;
			foreach ( var wheel in forces )
			{
				force += wheel.Force;
				torque += Vector3.Cross( wheel.Point - body.Position, wheel.Force );
			}
			var localTorque = Vector3.Transform( torque, Quaternion.Conjugate( body.Rotation ) );
			var angular = body.AngularVelocity + Vector3.Transform( localTorque / body.Inertia, body.Rotation ) * dt;
			var velocity = body.Velocity + force / settings.Mass * dt;
			var spin = angular.Length();
			var rotation = spin > 0.000001f
				? Quaternion.Normalize( Quaternion.CreateFromAxisAngle( angular / spin, spin * dt ) * body.Rotation )
				: body.Rotation;
			body = body with { Position = body.Position + velocity * dt, Velocity = velocity,
				AngularVelocity = angular, Rotation = rotation };
			Assert.InRange( body.Position.Z, 0.04f, 0.12f );
			Assert.True( float.IsFinite( angular.LengthSquared() ) );
		}
		Assert.Equal( 4, result.WheelCount );
		Assert.InRange( body.AngularVelocity.Length(), 0f, 0.005f );
		Assert.InRange( body.Velocity.Length(), 0f, 0.005f );
		Assert.InRange( body.Up.Z, 0.9999f, 1.0001f );
	}
}
