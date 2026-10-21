using System.Numerics;
using SkateMotion.Core.Camera;
using Xunit;

namespace SboxSkateMotion.Tests;

public class CameraHeadingTests
{
	private const float Frame = 1f / 60f;
	private const float Degree = MathF.PI / 180f;

	private static Vector3 Moving( float heading, float speed ) =>
		new( MathF.Cos( heading ) * speed, MathF.Sin( heading ) * speed, 0f );

	[Fact]
	public void FollowsTheTravelNotTheWayTheBoardFaces()
	{
		var heading = new TravelHeading();
		heading.Reset( 0f );
		// Rolling backwards (fakie), sliding sideways (powerslide) and spinning in place all keep the travel heading.
		foreach ( var facing in new[] { MathF.PI, MathF.PI / 2f, 2.5f } )
		{
			for ( var frame = 0; frame < 30; frame++ )
				heading.Update( Moving( 0f, 5f ), facing, false, Frame );
			Assert.Equal( 0f, heading.Value, 4 );
		}
	}

	[Fact]
	public void HoldsAtLowSpeedThenLinesUpWithTheNearerEnd()
	{
		var heading = new TravelHeading();
		heading.Reset( 0f );
		for ( var frame = 0; frame < 30; frame++ )
			heading.Update( Moving( 0f, 4f ), MathF.PI, false, Frame );
		// Stopped with the tail ahead (rolled to a stop fakie): the view holds, then lines up behind the tail, not
		// the nose, so it never turns round.
		for ( var frame = 0; frame < 50; frame++ )
			heading.Update( Vector3.Zero, MathF.PI - 0.4f, false, Frame );
		Assert.Equal( 0f, heading.Value, 4 );
		for ( var frame = 0; frame < 300; frame++ )
			heading.Update( Vector3.Zero, MathF.PI - 0.4f, false, Frame );
		Assert.Equal( -0.4f, heading.Value, 2 );
	}

	[Fact]
	public void StartingOffBlendsInWithSpeedInsteadOfJumping()
	{
		var heading = new TravelHeading();
		heading.Reset( 0f );
		var previous = heading.Value;
		var largest = 0f;
		// Rolling back the other way, slowly gathering speed.
		for ( var frame = 0; frame < 120; frame++ )
		{
			heading.Update( Moving( 2.8f, frame * 0.02f ), 0f, false, Frame );
			largest = MathF.Max( largest, MathF.Abs( CameraMath.Wrap( heading.Value - previous ) ) );
			previous = heading.Value;
		}
		Assert.Equal( 2.8f, heading.Value, 3 );
		Assert.True( largest < 12f * Degree, $"heading jumped {largest / Degree} deg in a frame" );
	}

	[Fact]
	public void AirHoldsTheTakeOffHeadingAndLandingBlendsBack()
	{
		var heading = new TravelHeading();
		heading.Reset( 0f );
		heading.Update( Moving( 0f, 5f ), 0f, false, Frame );
		for ( var frame = 0; frame < 40; frame++ )
			heading.Update( Moving( 0.6f, 5f ), 1f, true, Frame );
		Assert.Equal( 0f, heading.Value, 4 );
		var previous = heading.Value;
		var largest = 0f;
		for ( var frame = 0; frame < 40; frame++ )
		{
			heading.Update( Moving( 0.6f, 5f ), 1f, false, Frame );
			largest = MathF.Max( largest, MathF.Abs( heading.Value - previous ) );
			previous = heading.Value;
		}
		Assert.Equal( 0.6f, heading.Value, 4 );
		Assert.True( largest < 0.05f, $"landing blend jumped {largest} rad in a frame" );
	}

	[Fact]
	public void OllieHoldsTheHeightAndDropsAreFollowedDown()
	{
		var height = new RideHeight();
		height.Reset( 0.1f );
		var highest = 0f;
		for ( var time = 0f; time < 0.6f; time += Frame )
		{
			var board = 0.1f + 0.5f * MathF.Sin( MathF.PI * time / 0.6f );
			highest = MathF.Max( highest, height.Update( board, true, true, Frame ) - 0.1f );
		}
		// A half-metre ollie lifts the followed height by about a fifth of that at most.
		Assert.InRange( highest, 0f, 0.15f );
		height.Update( 0.1f, false, true, Frame );
		height.Reset( 2.5f );
		height.Update( 2.5f, true, true, Frame );
		Assert.Equal( 1.2f, height.Update( 1.2f, true, true, Frame ), 4 );
	}

	[Fact]
	public void LandingOnALedgeBlendsInsteadOfJumping()
	{
		var height = new RideHeight();
		height.Reset( 0f );
		height.Update( 0f, false, true, Frame );
		for ( var time = 0f; time < 0.5f; time += Frame )
			height.Update( 0.6f * MathF.Sin( MathF.PI * 0.5f * time / 0.5f ), true, true, Frame );
		var previous = height.Value;
		var largest = 0f;
		for ( var frame = 0; frame < 60; frame++ )
		{
			height.Update( 0.6f, false, true, Frame );
			largest = MathF.Max( largest, MathF.Abs( height.Value - previous ) );
			previous = height.Value;
		}
		Assert.Equal( 0.6f, height.Value, 4 );
		Assert.True( largest < 0.04f, $"landing moved the height {largest} m in a frame" );
	}

	[Fact]
	public void ShotsEaseInAndIgnoreFlicker()
	{
		var shots = new ShotBlend();
		shots.Reset( CameraShot.Chase );
		// A manual flickering on for a few frames over a bump never reaches the framing.
		for ( var frame = 0; frame < 5; frame++ )
			shots.Update( CameraShot.Manual, false, Frame );
		shots.Update( CameraShot.Chase, false, Frame );
		Assert.Equal( CameraShot.Chase, shots.Value );
		var previous = shots.Value.Distance;
		var largest = 0f;
		for ( var frame = 0; frame < 60; frame++ )
		{
			shots.Update( CameraShot.Manual, false, Frame );
			largest = MathF.Max( largest, MathF.Abs( shots.Value.Distance - previous ) );
			previous = shots.Value.Distance;
		}
		Assert.Equal( CameraShot.Manual.Distance, shots.Value.Distance, 4 );
		Assert.True( largest < 0.03f, $"distance scale jumped {largest} in a frame" );
	}
}
