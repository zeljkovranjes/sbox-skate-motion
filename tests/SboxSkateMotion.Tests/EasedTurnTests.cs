using SkateMotion.Core.Vehicle;
using Xunit;

namespace SboxSkateMotion.Tests;

public class EasedTurnTests
{
	private const float Delta = 1f / 60f;

	[Fact]
	public void ATurnGathersSpeedReachesItsTopAndSettlesOnTheAngle()
	{
		var turn = new EasedTurn( 360f, 1440f );
		var left = 120f;
		var previous = 0f;
		var fastest = 0f;
		for ( var frame = 0; frame < 120 && left > 1e-3f; frame++ )
		{
			var step = turn.Step( left, Delta );
			// Never more than the speed-up allows from one frame to the next, short of the last step onto the angle.
			if ( step < left )
				Assert.True( MathF.Abs( step - previous ) <= 1440f * Delta * Delta + 1e-4f );
			Assert.InRange( step, 0f, left );
			fastest = MathF.Max( fastest, step / Delta );
			previous = step;
			left -= step;
		}
		Assert.True( left <= 1e-3f );
		Assert.Equal( 360f, fastest, 1 );
		// The first frame turns only as far as the speed-up allows.
		Assert.Equal( 1440f * Delta * Delta, new EasedTurn( 360f, 1440f ).Step( 120f, Delta ), 4 );
	}

	[Fact]
	public void ATurnNeverGoesPastTheAngleLeftNorTheOtherWay()
	{
		var turn = new EasedTurn( 360f, 1440f );
		for ( var frame = 0; frame < 30; frame++ )
			turn.Step( 90f, Delta );
		// The angle left flips: no step back past zero this frame, and the turn slows before it reverses.
		Assert.Equal( 0f, turn.Step( 0f, Delta ) );
		var step = turn.Step( -40f, Delta );
		Assert.InRange( step, -40f, 0f );
		Assert.True( turn.Rate > -360f );
		turn.Reset();
		Assert.Equal( 0f, turn.Rate );
		Assert.Equal( 0f, turn.Step( float.NaN, Delta ) );
		Assert.Equal( 0f, turn.Step( 10f, 0f ) );
	}
}
