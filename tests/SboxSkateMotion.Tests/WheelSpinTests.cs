using SkateMotion.Core.Vehicle;
using Xunit;

namespace SboxSkateMotion.Tests;

public class WheelSpinTests
{
	private const float Radius = 0.03f;

	[Theory]
	[InlineData( 5f )]
	[InlineData( -2f )]
	public void AWheelOnTheGroundTurnsAtGroundSpeedOverItsRadius( float speed )
	{
		var spin = new WheelSpin();
		var turned = 0f;
		var previous = 0f;
		for ( var frame = 0; frame < 60; frame++ )
		{
			spin.Update( 0, speed, Radius, true, 1f / 60f );
			turned += MathF.IEEERemainder( spin.Angle( 0 ) - previous, MathF.Tau );
			previous = spin.Angle( 0 );
		}
		Assert.Equal( speed / Radius, spin.Rate( 0 ), 3 );
		Assert.Equal( speed / Radius, turned, 1 );
		Assert.InRange( spin.Angle( 0 ), -MathF.PI, MathF.PI );
	}

	[Fact]
	public void AFreeWheelSpinsOnAndSlowsGently()
	{
		var spin = new WheelSpin();
		spin.Update( 1, 6f, Radius, true, 1f / 60f );
		for ( var frame = 0; frame < 60; frame++ )
			spin.Update( 1, 0f, Radius, false, 1f / 60f );
		Assert.Equal( 6f / Radius * MathF.Exp( -WheelSpin.FreeDrag ), spin.Rate( 1 ), 2 );
		// Back on the ground it rolls with the ground again.
		spin.Update( 1, 3f, Radius, true, 1f / 60f );
		Assert.Equal( 3f / Radius, spin.Rate( 1 ), 3 );
	}

	[Fact]
	public void SlidingAcrossItsWheelsStopsThem()
	{
		var spin = new WheelSpin();
		spin.Update( 2, 5f, Radius, true, 1f / 60f );
		// Sideways the wheel's own rolling speed is nothing.
		spin.Update( 2, 0f, Radius, true, 1f / 60f );
		Assert.Equal( 0f, spin.Rate( 2 ) );
	}

	[Fact]
	public void TheFreeSpinSlowsTheSameAtAnyFrameRate()
	{
		float Run( float step )
		{
			var spin = new WheelSpin();
			spin.Update( 3, 6f, Radius, true, step );
			for ( var time = 0f; time < 1f - step * 0.5f; time += step )
				spin.Update( 3, 0f, Radius, false, step );
			return spin.Rate( 3 );
		}
		Assert.Equal( Run( 1f / 30f ), Run( 1f / 144f ), 1 );
	}
}
