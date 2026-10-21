using System;
using SkateMotion.Core.Animation;
using SkateMotion.Core.Vehicle;
using Xunit;

namespace SboxSkateMotion.Tests;

public class DropInTests
{
	private const float Spacing = 0.1f;

	[Fact]
	public void TheLipIsWhereLevelGroundGivesWayToARampBelow()
	{
		// Level for 0.8 m, then a ramp falling away at a steep angle.
		var heights = new float[ 16 ];
		var ups = new float[ 16 ];
		for ( var i = 0; i < heights.Length; i++ )
		{
			var past = (i + 1) * Spacing - 0.85f;
			heights[ i ] = past > 0f ? -past * 2f : 0f;
			ups[ i ] = past > 0f ? 0.45f : 1f;
		}
		Assert.Equal( 0.85f, DropInEdge.Find( heights, ups, Spacing ) ?? -1f, 2 );
	}

	[Fact]
	public void AGentleRampIsALipFromWhereItStartsToFall()
	{
		var heights = new float[ 16 ];
		var ups = new float[ 16 ];
		for ( var i = 0; i < heights.Length; i++ )
		{
			var past = (i + 1) * Spacing - 0.45f;
			heights[ i ] = past > 0f ? -past * 0.47f : 0f;
			ups[ i ] = past > 0f ? 0.906f : 1f;
		}
		Assert.InRange( DropInEdge.Find( heights, ups, Spacing ) ?? -1f, 0.4f, 0.6f );
	}

	[Fact]
	public void LevelGroundAStepDownAndASheerDropAreNoLips()
	{
		var level = new float[ 16 ];
		var upright = new float[ 16 ];
		Array.Fill( upright, 1f );
		Assert.Null( DropInEdge.Find( level, upright, Spacing ) );
		// A kerb down to flat ground.
		var step = new float[ 16 ];
		for ( var i = 8; i < step.Length; i++ )
			step[ i ] = -0.3f;
		Assert.Null( DropInEdge.Find( step, upright, Spacing ) );
		// Nothing below at all.
		var sheer = new float[ 16 ];
		for ( var i = 8; i < sheer.Length; i++ )
			sheer[ i ] = float.NaN;
		Assert.Null( DropInEdge.Find( sheer, upright, Spacing ) );
	}

	[Fact]
	public void TheGraphSeesTheLipTheHeldMountAndTheBoardGoingOver()
	{
		var facts = new StateFacts();
		var source = new RiderStateFacts();
		source.Fill( facts, new RiderAnimationInput { Activity = RiderActivity.Mounting, HoldingBoard = true, EdgeDistance = 0.7f,
			MountHeld = true }, false, 1f / 60f );
		Assert.Equal( 0.7f, facts.Facts[ "dist-to-edge" ] );
		Assert.True( facts.Intents.ContainsKey( "ob-mount" ) );
		Assert.True( facts.Intents.ContainsKey( "ob-mount-raw" ) );
		source.Fill( facts, new RiderAnimationInput { Activity = RiderActivity.Mounting, HoldingBoard = true }, false, 1f / 60f );
		Assert.False( facts.Facts.ContainsKey( "dist-to-edge" ) );
		Assert.False( facts.Intents.ContainsKey( "ob-mount-raw" ) );
		// Just over the lip the board goes over it as on a rail.
		source.Fill( facts, new RiderAnimationInput { Activity = RiderActivity.Riding, DroppingIn = true }, false, 1f / 60f );
		Assert.Equal( "grind", facts.Physics );
		Assert.Equal( 1f, facts.Facts[ "is-dropping-in" ] );
	}

	[Fact]
	public void AFreeFootReachesForTheGroundAndPlantsJustBeforeTheLanding()
	{
		RiderAnimationInput Air( float timeToLand ) => new()
		{
			Activity = RiderActivity.Riding, Airborne = true, Grab = "method", FeetOff = 2, VerticalSpeed = -2f, TimeToLand = timeToLand
		};
		var facts = new StateFacts();
		var source = new RiderStateFacts();
		source.Fill( facts, Air( 0.8f ), false, 1f / 60f );
		Assert.Equal( 0f, facts.Facts[ "should-prepare-one-foot-air-for-footplant" ] );
		source.Fill( facts, Air( 0.3f ), false, 1f / 60f );
		Assert.Equal( 1f, facts.Facts[ "should-prepare-one-foot-air-for-footplant" ] );
		Assert.Equal( 0f, facts.Facts[ "is-foot-planting" ] );
		source.Fill( facts, Air( 0.05f ), false, 1f / 60f );
		Assert.Equal( 1f, facts.Facts[ "is-foot-planting" ] );
		// Both feet on the board, or no grab, plants nothing.
		source.Fill( facts, Air( 0.05f ) with { FeetOff = 0 }, false, 1f / 60f );
		Assert.Equal( 0f, facts.Facts[ "is-foot-planting" ] );
		source.Fill( facts, Air( 0.05f ) with { Grab = null }, false, 1f / 60f );
		Assert.Equal( 0f, facts.Facts[ "is-foot-planting" ] );
	}
}
