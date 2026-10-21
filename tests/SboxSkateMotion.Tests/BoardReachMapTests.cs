using System;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class BoardReachMapTests
{
	// Reaches as a clip set measures them: each misses its own yaw by a few degrees, the left-hand side reaches no
	// further round than 73 degrees and the back reaches end 12 degrees short of straight behind on the right, leaving
	// the left side behind the rider with no reach at all.
	private static readonly float[] Yaws = { -90f, -45f, 0f, 45f, 90f, 135f, 180f };
	private static readonly float[] Bearings = { 73f, 40f, -10f, -47f, -88f, -136f, -168f };

	[Fact]
	public void TheReachLandsOnTheBoardAllTheWayRound()
	{
		var map = new BoardReachMap( Yaws, Bearings );
		for ( var bearing = 0f; bearing < 360f; bearing += 1f )
		{
			var (yaw, turn) = map.Solve( bearing );
			// Where the reach blended at that yaw holds the board, once the rider has turned as asked.
			var reached = Reach( yaw ) + turn;
			Assert.True( Difference( reached, bearing ) < 0.01f, $"bearing {bearing}: reaches {reached}" );
		}
	}

	[Theory]
	[InlineData( 0f, true )]
	[InlineData( 45f, true )]
	[InlineData( -90f, true )]
	[InlineData( -160f, true )]
	[InlineData( 100f, false )]
	[InlineData( 150f + 360f, false )]
	[InlineData( -170f, false )]
	public void OnlyABoardNoReachCoversNeedsATurn( float bearing, bool covered )
	{
		var (_, turn) = new BoardReachMap( Yaws, Bearings ).Solve( bearing );
		Assert.Equal( covered, turn == 0f );
	}

	[Fact]
	public void ABoardOnTheUncoveredSideIsTurnedToTheNearerReach()
	{
		var map = new BoardReachMap( Yaws, Bearings );
		// Just past the left reach: turn left the rest of the way and reach as far left as the clips go.
		Assert.Equal( (-90f, 7f), map.Solve( 80f ) );
		// Nearly behind on the left: turn right until the back reach covers it.
		var (yaw, turn) = map.Solve( 170f );
		Assert.Equal( 180f, yaw );
		Assert.Equal( -22f, turn, 3 );
		// Kept to the edge it began turning toward, even past the middle of the gap.
		Assert.Equal( -90f, map.Solve( 170f, 1 ).Yaw );
		Assert.Equal( 97f, map.Solve( 170f, 1 ).Turn, 3 );
	}

	[Fact]
	public void ReachesOutOfOrderAreRefused()
	{
		Assert.Throws<ArgumentException>( () => new BoardReachMap( new[] { 0f, 45f }, new[] { -10f, 20f } ) );
		Assert.Throws<ArgumentException>( () => new BoardReachMap( new[] { 0f }, new[] { 0f } ) );
	}

	// Where the reach holds the board at a yaw, the clips' reaches mixed in proportion between the two either side.
	private static float Reach( float yaw )
	{
		for ( var i = 0; i < Yaws.Length - 1; i++ )
		{
			if ( yaw < Yaws[ i ] || yaw > Yaws[ i + 1 ] )
				continue;
			var t = (yaw - Yaws[ i ]) / (Yaws[ i + 1 ] - Yaws[ i ]);
			return Bearings[ i ] + (Bearings[ i + 1 ] - Bearings[ i ]) * t;
		}
		throw new ArgumentOutOfRangeException( nameof( yaw ) );
	}

	private static float Difference( float a, float b ) => MathF.Abs( ((a - b) % 360f + 540f) % 360f - 180f );
}
