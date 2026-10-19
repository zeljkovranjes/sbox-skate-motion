using SkateMotion.Core;
using Xunit;

namespace SboxSkateMotion.Tests;

public class SteeringGripTests
{
	[Theory]
	[InlineData( 3f )]
	[InlineData( 6f )]
	[InlineData( 9f )]
	public void FullSteerAtSpeedNeverAsksForMoreThanTheCarveGrip( float speed )
	{
		var settings = new SkateSettings();
		var angle = settings.SteeringAngle( 1f, speed );
		// Two steered axles: yaw rate 2 v tan(a) / L, sideways acceleration v times that.
		var lateral = speed * 2f * speed * MathF.Tan( angle ) / settings.Wheelbase;
		Assert.True( lateral <= settings.CarveAcceleration + 0.001f, $"{lateral} m/s^2 at {speed} m/s" );
	}

	[Fact]
	public void SlowSteeringKeepsTheTruckGeometryAndIsSymmetric()
	{
		var settings = new SkateSettings();
		Assert.Equal( settings.TruckAngle / (1f + 0.5f * settings.SteeringSpeedScale), settings.SteeringAngle( 1f, 0.5f ), 5 );
		Assert.Equal( -settings.SteeringAngle( 0.6f, 6f ), settings.SteeringAngle( -0.6f, 6f ), 6 );
		Assert.Equal( settings.TruckAngle, settings.SteeringAngle( 1f, 0f ), 5 );
	}
}
