using System;
using System.Numerics;
using SkateMotion.Core.Grinding;
using Xunit;

namespace SboxSkateMotion.Tests;

public class AirAssistTipReachTests
{
	private const float Step = 1f / 120f;
	private const float Gravity = 9.8f;

	// A rail along +x, its top 0.4 m up.
	private static readonly RailSegment[] Rail = { new( new Vector3( 0f, 0f, 0.4f ), new Vector3( 10f, 0f, 0.4f ) ) };

	// Flies a board turned across the rail (deck forward +y) from beside it until its trucks come down to the rail's
	// height, applying the assist with the given end-slide reach; returns where its middle came down.
	private static Vector3 Fly( Vector3 position, Vector3 velocity, float tipReach )
	{
		var assist = new GrindAirAssist();
		for ( var t = 0f; t < 3f && (velocity.Z > 0f || position.Z - 0.1f > 0.4f); t += Step )
		{
			var (move, change) = assist.Move( position, position.Z - 0.1f, velocity, Rail, Gravity, Step,
				heading: Vector3.UnitY, tipReach: tipReach );
			velocity += change;
			position += velocity * Step + move;
			velocity.Z -= Gravity * Step;
		}
		return position;
	}

	[Theory]
	[InlineData( 0.287f )]
	[InlineData( GrindControl.TipReach )]
	public void AnEndSlideComesDownWithTheRailAtTheBoardsOwnReach( float reach )
	{
		// Turned across the rail, its middle 0.25 m beside it: the tail is nearest, and the board is moved so the rail
		// comes under the tail where that board's slide rests it, not at another board's reach.
		var landed = Fly( new Vector3( 1f, 0.25f, 0.95f ), new Vector3( 4f, 0f, 0f ), reach );
		Assert.True( MathF.Abs( landed.Y - reach ) < 0.01f, $"middle came down {landed.Y} m off the rail, wanted {reach}" );
	}
}
