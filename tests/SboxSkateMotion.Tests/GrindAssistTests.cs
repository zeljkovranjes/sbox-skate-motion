using System;
using System.Numerics;
using SkateMotion.Core.Grinding;
using Xunit;

namespace SboxSkateMotion.Tests;

public class GrindAssistTests
{
	private static readonly RailSegment[] Rail = { new( new Vector3( 0f, 0f, 0.4f ), new Vector3( 10f, 0f, 0.4f ) ) };

	[Fact]
	public void AFallingBoardBesideARailIsSteeredOntoIt()
	{
		// 0.15 m to the side of the rail, moving along it and falling toward its height.
		var position = new Vector3( 1f, 0.15f, 1.2f );
		var velocity = new Vector3( 4f, 0f, -1f );
		var total = Vector3.Zero;
		for ( var i = 0; i < 60; i++ )
		{
			var change = GrindAssist.VelocityChange( position, position.Z - 0.1f, velocity, Rail, 9.8f, 1f / 120f );
			velocity += change;
			total += change;
			velocity.Z -= 9.8f / 120f;
			position += velocity / 120f;
			if ( position.Z - 0.1f <= 0.4f )
				break;
		}
		Assert.True( total.Y < 0f );
		Assert.InRange( position.Y, -0.03f, 0.03f );
		// It arrives moving along the rail, not across it.
		Assert.InRange( velocity.Y, -0.2f, 0.2f );
	}

	[Fact]
	public void FarRailsRisingBoardsAndSharpTurnsAreLeftAlone()
	{
		// Out of reach.
		Assert.Equal( Vector3.Zero, GrindAssist.VelocityChange( new Vector3( 1f, 2f, 1.2f ), 1.1f, new Vector3( 4f, 0f, -1f ), Rail, 9.8f, 0.01f ) );
		// Past the rail's end on arrival.
		Assert.Equal( Vector3.Zero, GrindAssist.VelocityChange( new Vector3( 9.8f, 0.1f, 1.2f ), 1.1f, new Vector3( 4f, 0f, -1f ), Rail, 9.8f, 0.01f ) );
		// Still rising.
		Assert.Equal( Vector3.Zero, GrindAssist.VelocityChange( new Vector3( 1f, 0.4f, 1.2f ), 1.1f, new Vector3( 4f, 0f, 1f ), Rail, 9.8f, 0.01f ) );
		// Slow travel would need far more than the largest bend.
		Assert.Equal( Vector3.Zero, GrindAssist.VelocityChange( new Vector3( 1f, 0.8f, 1.2f ), 1.1f, new Vector3( 0.5f, 0f, -1f ), Rail, 9.8f, 0.01f ) );
	}
}
