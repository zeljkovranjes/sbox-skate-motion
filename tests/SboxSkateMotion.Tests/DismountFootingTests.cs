using System.Numerics;
using SkateMotion.Core.Animation;
using SkateMotion.Core.Vehicle;
using Xunit;

namespace SboxSkateMotion.Tests;

public class DismountFootingTests
{
	private const float Frame = 1f / 60f;

	[Fact]
	public void ACrouchHeldWhileSteppingOffEndsOnFoot()
	{
		// Getting off the board in a hippy jump's crouch, the held control no longer crouches the rider on foot; back on
		// the board, holding it crouches again.
		var jump = new HippyJump();
		for ( var i = 0; i < 20; i++ )
			jump.Step( true, Vector3.Zero, true, 9.8f, Frame );
		Assert.True( jump.Crouching );
		jump.Clear();
		Assert.False( jump.Crouching );
		Assert.False( jump.Flying );
		jump.Step( true, Vector3.Zero, true, 9.8f, Frame );
		Assert.True( jump.Crouching );
	}

	[Theory]
	[InlineData( 0f, 0.1f, false )]
	[InlineData( -0.5f, 0.25f, false )]
	[InlineData( 0f, 0.6f, true )]
	[InlineData( 2f, 0.05f, true )]
	[InlineData( 0f, float.PositiveInfinity, true )]
	public void SteppingDownIsNotAFallButLeavingTheGroundIs( float rising, float gap, bool airborne )
	{
		Assert.Equal( airborne, BipedFooting.Airborne( rising, gap ) );
	}

	[Fact]
	public void AWalkerInTheAirIsInTheBipedsAir()
	{
		var facts = new StateFacts();
		var source = new RiderStateFacts();
		source.Fill( facts, new RiderAnimationInput { Activity = RiderActivity.Walking, Airborne = true, MoveSpeed = 2f }, false, Frame );
		Assert.Equal( "offboardair", facts.Physics );
		Assert.Equal( 1f, facts.Facts[ "is-in-biped-air" ] );
		Assert.Equal( 1f, facts.Facts[ "can-biped-land" ] );
		// Nothing under a walker is a board to land on.
		Assert.Equal( 0f, facts.Facts[ "can-land-on-board" ] );
		// Its speed picks a standing or a running jump.
		Assert.Equal( 2f, facts.Facts[ "com-vel-compare" ] );

		source.Fill( facts, new RiderAnimationInput { Activity = RiderActivity.Walking }, false, Frame );
		Assert.Equal( "offboard", facts.Physics );
		Assert.Equal( 0f, facts.Facts[ "is-in-biped-air" ] );
	}

	[Fact]
	public void ARiderStillLandsOnItsBoard()
	{
		var facts = new StateFacts();
		var source = new RiderStateFacts();
		source.Fill( facts, new RiderAnimationInput { Airborne = true }, false, Frame );
		Assert.Equal( 1f, facts.Facts[ "can-land-on-board" ] );
		Assert.Equal( 0f, facts.Facts[ "is-in-biped-air" ] );
		Assert.False( facts.Facts.ContainsKey( "com-vel-compare" ) );
		source.Fill( facts, new RiderAnimationInput { OffBoard = true, OverBoard = true }, false, Frame );
		Assert.Equal( 1f, facts.Facts[ "can-land-on-board" ] );
		source.Fill( facts, new RiderAnimationInput { OffBoard = true, OverBoard = false }, false, Frame );
		Assert.Equal( 0f, facts.Facts[ "can-land-on-board" ] );
	}

	[Fact]
	public void SteppingOffKeepsTheBoardsSituationOnlyForItsFirstMoment()
	{
		// Off a rail: the graph sees the grind as the step off starts, so it can pick how the rider gets off, and then a
		// rider on foot, falling and landing.
		var facts = new StateFacts();
		var source = new RiderStateFacts();
		source.Fill( facts, new RiderAnimationInput { Grind = "fiftyfifty" }, false, Frame );
		Assert.Equal( "grind", facts.Physics );
		var falling = new RiderAnimationInput { Activity = RiderActivity.Dismounting, Airborne = true, Grind = "fiftyfifty" };
		source.Fill( facts, falling, false, Frame );
		Assert.Equal( "grind", facts.Physics );
		Assert.Equal( 1f, facts.Facts[ "can-land-on-board" ] );
		for ( var i = 0; i < 10; i++ )
			source.Fill( facts, falling, false, Frame );
		Assert.Equal( "offboardair", facts.Physics );
		Assert.Equal( 0f, facts.Facts[ "can-land-on-board" ] );
		Assert.Equal( 1f, facts.Facts[ "is-in-biped-air" ] );
		source.Fill( facts, falling with { Airborne = false }, false, Frame );
		Assert.Equal( "offboard", facts.Physics );

		// Off a board riding along a wall the board is on the ground, but the rider stepping off is in the air.
		var wall = new RiderStateFacts();
		wall.Fill( facts, new RiderAnimationInput { WallRiding = true }, false, Frame );
		Assert.Equal( "ground", facts.Physics );
		wall.Fill( facts, new RiderAnimationInput { Activity = RiderActivity.Dismounting, Airborne = true, Speed = 5f }, false, Frame );
		Assert.Equal( "air", facts.Physics );

		// A frame as long as the whole moment still shows the board's situation once.
		var slow = new RiderStateFacts();
		slow.Fill( facts, new RiderAnimationInput { OffBoard = true }, false, 0.2f );
		slow.Fill( facts, falling, false, 0.2f );
		Assert.Equal( "offboard", facts.Physics );
		slow.Fill( facts, falling, false, 0.2f );
		Assert.Equal( "offboardair", facts.Physics );
	}

	[Fact]
	public void OnlyTheBoardsOwnRunoutOntoTheGroundRunsItOut()
	{
		// A step off at speed just after a landing is not a runout: the landing would answer one with a wipeout.
		var facts = new StateFacts();
		var source = new RiderStateFacts();
		var stepping = new RiderAnimationInput { Activity = RiderActivity.Dismounting, Speed = 5f };
		source.Fill( facts, stepping, false, Frame );
		Assert.Equal( 0f, facts.Facts[ "physics-wants-runout" ] );
		source.Fill( facts, stepping with { Runout = true }, false, Frame );
		Assert.Equal( 1f, facts.Facts[ "physics-wants-runout" ] );
		source.Fill( facts, stepping with { Runout = true, Airborne = true }, false, Frame );
		Assert.Equal( 0f, facts.Facts[ "physics-wants-runout" ] );
		source.Fill( facts, stepping with { Runout = true, Speed = 2f }, false, Frame );
		Assert.Equal( 0f, facts.Facts[ "physics-wants-runout" ] );
	}

	[Fact]
	public void AFlightClipPlaysOverTheWholeFallAndNeverStartsOver()
	{
		// A half-second jump clip over a 1.5 s fall: it slows to end as the rider lands, and never reaches its end (where it
		// would start over) in the air, at any frame rate.
		foreach ( var step in new[] { 1f / 120f, 1f / 60f, 1f / 15f } )
		{
			const float length = 0.5f;
			var time = 0f;
			for ( var left = 1.5f; left > 0f; left -= step )
			{
				time += step * AirTimeMatch.Rate( length - time, left, step );
				Assert.True( time < length, $"clip reached its end {left:0.00} s before landing at {step:0.000} s frames" );
			}
			Assert.True( time > 0.85f * length, $"clip at {time / length:P0} at touchdown with {step:0.000} s frames" );
		}
	}

	[Theory]
	[InlineData( 0.3f, 1f, 0.3f )]
	[InlineData( 0.3f, 0f, AirTimeMatch.SlowestRate )]
	[InlineData( 0.3f, 0.05f, AirTimeMatch.FastestRate )]
	[InlineData( 0f, 1f, 0f )]
	public void AFlightClipIsStretchedToTheTimeLeft( float remaining, float timeToLand, float rate )
	{
		Assert.Equal( rate, AirTimeMatch.Rate( remaining, timeToLand, 1f / 60f ), 3 );
	}

	[Fact]
	public void GettingOffInTheAirOfAHippyJumpHandsTheRiderOverToTheGround()
	{
		// The hippy jump's flight leaves only through landing on the board, a fall onto the ground on the board, or getting
		// off as the rider is off the deck. Stepping off in the air must take that last way out: a rider kept in the
		// flight's pose on foot looks frozen in the air.
		var runout = State( "HippyRunout", null, "hippy-out" );
		runout.Active = false;
		var dismountAir = State( "Air", null, null, runout );
		dismountAir.Transitions.Add( To( "OffBoard.Ground", null ) );
		var dismount = State( "Dismount", null, null, dismountAir, State( "Ground", Intent( "ob-dismount" ), "step-off" ) );
		dismount.Interruptible = "false";
		var standing = State( "Ground", Physics( "offboard" ), "stand" );
		standing.Transitions.Add( To( "OffBoard.Air", null ) );
		var flying = State( "Air", And( Physics( "offboardair" ), Fact( "is-in-biped-air" ) ), "biped-air" );
		flying.Interruptible = "false";
		flying.Transitions.Add( To( "OffBoard.Ground", null ) );
		var offBoard = State( "OffBoard", null, null, standing, flying );
		offBoard.Interruptible = "false";
		var flight = State( "Cyc", null, "hippy-air" );
		flight.Interruptible = "false";
		flight.Transitions.Add( To( "WipeOut", Physics( "ground" ) ) );
		var hippy = State( "HippyJump", Physics( "offboard" ), null, flight );
		hippy.Transitions.Add( To( "Dismount.Air.HippyRunout", And( Physics( "offboard" ), Intent( "ob-dismount" ) ) ) );
		var riding = State( "Riding", null, "ride" );
		riding.Interruptible = "false";
		riding.Transitions.Add( To( "HippyJump.Cyc", Physics( "offboard" ) ) );
		var root = Root( dismount, offBoard, State( "OnBoard", null, null, hippy, riding ), State( "WipeOut", null, "wipeout" ) );
		root.Policy = "priority";
		var graph = new StateGraph( root );
		var facts = new StateFacts();
		var source = new RiderStateFacts();
		void Show( RiderAnimationInput input )
		{
			source.Fill( facts, input, false, Frame );
			graph.Update( facts, Frame );
		}

		for ( var i = 0; i < 5; i++ )
			Show( new RiderAnimationInput { OffBoard = true, HippyJump = true } );
		Assert.Equal( "OnBoard.HippyJump.Cyc", graph.State );
		var seen = new System.Collections.Generic.List<string>();
		for ( var i = 0; i < 30; i++ )
		{
			Show( new RiderAnimationInput { Activity = RiderActivity.Dismounting, Airborne = true } );
			seen.Add( graph.State );
		}
		Assert.Contains( "Dismount.Air.HippyRunout", seen );
		Assert.DoesNotContain( "WipeOut", seen );
		Show( new RiderAnimationInput { Activity = RiderActivity.Dismounting } );
		for ( var i = 0; i < 10; i++ )
			Show( new RiderAnimationInput { Activity = RiderActivity.Walking } );
		Assert.Equal( "OffBoard.Ground", graph.State );
		for ( var i = 0; i < 10; i++ )
			Show( new RiderAnimationInput { Activity = RiderActivity.Walking, Airborne = true } );
		Assert.Equal( "OffBoard.Air", graph.State );
	}

	private static StateGraphDefinition Root( params StateGraphDefinition[] children ) => State( "Root", null, null, children );

	private static StateGraphDefinition State( string name, StateTestDefinition guard, string clip, params StateGraphDefinition[] children )
	{
		var state = new StateGraphDefinition { Name = name, Guard = guard, Play = clip is null ? null : new StatePlayDefinition { Clip = clip } };
		state.States.AddRange( children );
		return state;
	}

	private static StateTransitionDefinition To( string target, StateTestDefinition guard ) => new() { Target = target, Guard = guard };

	private static StateTestDefinition Intent( string name ) => new() { Test = "intent", Intent = name };

	private static StateTestDefinition Physics( string state ) => new() { Test = "physics", State = state };

	private static StateTestDefinition Fact( string name ) => new() { Test = "fact", Fact = name };

	private static StateTestDefinition And( params StateTestDefinition[] items )
	{
		var test = new StateTestDefinition { Op = "and" };
		test.Items.AddRange( items );
		return test;
	}
}
