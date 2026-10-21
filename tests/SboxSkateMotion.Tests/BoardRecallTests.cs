using SkateMotion.Core.Physics;
using Xunit;

namespace SboxSkateMotion.Tests;

public class BoardRecallTests
{
	[Fact]
	public void WithinTheUsualReachARecallFliesAsACallDoes()
	{
		for ( var distance = 0f; distance <= BoardReach.Farthest; distance += 0.5f )
			Assert.Equal( BoardReach.FlightSeconds( distance ), BoardReach.RecallSeconds( distance ), 5 );
	}

	[Fact]
	public void FromFartherTheBoardComesAtSeventyMetresASecond()
	{
		Assert.Equal( 2f, BoardReach.RecallSeconds( 140f ), 3 );
		Assert.Equal( 10f, BoardReach.RecallSeconds( 700f ), 3 );
		var previous = BoardReach.RecallSeconds( BoardReach.Farthest );
		for ( var distance = BoardReach.Farthest + 1f; distance <= 1000f; distance += 1f )
		{
			var seconds = BoardReach.RecallSeconds( distance );
			Assert.True( seconds >= previous );
			Assert.True( distance / seconds <= 70.001f );
			previous = seconds;
		}
	}

	[Fact]
	public void ANonsenseDistanceStillGivesAShortFlight()
	{
		Assert.Equal( BoardReach.FlightSeconds( float.NaN ), BoardReach.RecallSeconds( float.NaN ), 5 );
		Assert.Equal( BoardReach.FlightSeconds( float.PositiveInfinity ), BoardReach.RecallSeconds( float.PositiveInfinity ), 5 );
	}
}
