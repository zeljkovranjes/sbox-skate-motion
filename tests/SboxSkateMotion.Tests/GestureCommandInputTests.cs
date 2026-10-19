using SkateMotion.Core.Input;
using System;
using System.Numerics;
using Xunit;

namespace SboxSkateMotion.Tests;

public class GestureCommandInputTests
{
	private static GestureCommandIdentity Identity( string name ) => new( name );

	private static GestureRecognitionSet Set( int lane, string name, params Vector2[] points )
		=> new( lane, [ new GesturePattern( points, 0f ) ], [ Identity( name ) ], 10 );

	private static ControllerReading Reading( Vector2 left, Vector2 right )
		=> new( left.X, -left.Y, right.X, -right.Y, 0f, 0f, default );

	[Fact]
	public void ForeignPlayerCannotQueueOrLatchOwnedPattern()
	{
		var identity = Identity( "Spin" );
		var receiver = new GestureCommandReceiver( 2, [ identity ] );
		Assert.False( receiver.ReceiveMatch( 1, identity, 1f, out _ ) );
		Assert.Null( receiver.TrackedPattern );
		Assert.True( receiver.ReceiveMatch( 2, identity, 0.75f, out var command ) );
		Assert.Equal( new QueuedInputCommand( 3, identity.Key, 0.75f ), command );
		receiver.ReceiveHold( 1, identity );
		Assert.False( receiver.HoldPending );
	}

	[Fact]
	public void NonHoldMatchPreservesTrackedNameAndHoldRemainsLatchedUntilConsumed()
	{
		var receiver = new GestureCommandReceiver( 0, [ Identity( "Spin" ) ] );
		receiver.ReceiveMatch( 0, Identity( "sPIN" ), 1f, out _ );
		receiver.ReceiveMatch( 0, Identity( "Other" ), 0.5f, out _ );
		Assert.Equal( "sPIN", receiver.TrackedPattern.Name );
		receiver.ReceiveHold( 0, Identity( "Spin" ) );
		receiver.ReceiveHold( 0, Identity( "Other" ) );
		Assert.True( receiver.ConsumeHold() );
		Assert.False( receiver.ConsumeHold() );
		receiver.ReceiveHold( 0, Identity( "SPIN" ) );
		Assert.True( receiver.HoldPending );
	}

	[Fact]
	public void HoldIdentityComparesWholeAsciiName()
	{
		Assert.True( Identity( "Abc_123" ).Matches( Identity( "aBC_123" ) ) );
		Assert.False( Identity( "Abc_123" ).Matches( Identity( "Abc_123_x" ) ) );
		Assert.False( Identity( "Abc[" ).Matches( Identity( "abc{" ) ) );
		Assert.Throws<ArgumentException>( () => Identity( "caf\u00e9" ) );
		Assert.Throws<ArgumentOutOfRangeException>( () => Identity( new string( 'a', 32 ) ) );
	}

	[Fact]
	public void IndependentSetsQueueEveryWinnerWithRightLaneFirst()
	{
		var sets = new[] {
			Set( 0, "Left", Vector2.UnitY, Vector2.UnitX ),
			Set( 1, "RightA", Vector2.UnitY, Vector2.UnitX ),
			Set( 1, "RightB", Vector2.UnitY, Vector2.UnitX ) };
		var pipeline = new GestureInputPipeline( sets, new( 0, [] ), 0f );
		var commands = new QueuedInputCommand[ 3 ];
		var count = 0;
		pipeline.Notify( Reading( Vector2.Zero, Vector2.Zero ), 0, 1, commands, ref count );
		pipeline.Notify( Reading( Vector2.UnitY, Vector2.UnitY ), 0, 1, commands, ref count );
		pipeline.Notify( Reading( Vector2.UnitX, Vector2.UnitX ), 0, 1, commands, ref count );
		Assert.Equal( 3, count );
		Assert.Equal( Identity( "RightA" ).Key, commands[ 0 ].Key );
		Assert.Equal( Identity( "RightB" ).Key, commands[ 1 ].Key );
		Assert.Equal( Identity( "Left" ).Key, commands[ 2 ].Key );
	}

	[Fact]
	public void AllLaneHoldsArriveBeforeNewMatchesCanReplaceTrackedName()
	{
		var receiver = new GestureCommandReceiver( 0, [ Identity( "Alpha" ), Identity( "Beta" ) ] );
		var pipeline = new GestureInputPipeline( [
			Set( 1, "Alpha", Vector2.UnitY, Vector2.UnitX, Vector2.UnitX ),
			Set( 1, "Beta", Vector2.UnitY, Vector2.UnitX ) ], receiver, 0f );
		var commands = new QueuedInputCommand[ 2 ];
		foreach ( var point in new[] { Vector2.Zero, Vector2.UnitY, Vector2.UnitX } )
		{
			var count = 0;
			pipeline.Notify( Reading( Vector2.Zero, point ), 0, 1, commands, ref count );
		}
		Assert.Equal( "Beta", receiver.TrackedPattern.Name );
		var finalCount = 0;
		pipeline.Notify( Reading( Vector2.Zero, Vector2.UnitX ), 0, 1, commands, ref finalCount );
		Assert.Equal( "Alpha", receiver.TrackedPattern.Name );
		Assert.True( receiver.HoldPending );
		Assert.Single( commands.AsSpan( 0, finalCount ).ToArray() );
	}

	[Fact]
	public void FilteringUsesIndependentComponentsAndPreservesThresholdBoundary()
	{
		var pipeline = new GestureInputPipeline( [
			Set( 1, "Edge", new( 0.1f, 0f ), new( 0f, -0.1f ) ) ], new( 0, [] ), 0.1f );
		var commands = new QueuedInputCommand[ 1 ];
		var count = 0;
		pipeline.Notify( default, 0, 1, commands, ref count );
		pipeline.Notify( new( 0f, 0f, 0.1f, -0.099f, 0f, 0f, default ), 0, 1, commands, ref count );
		pipeline.Notify( new( 0f, 0f, -0.099f, 0.1f, 0f, 0f, default ), 0, 1, commands, ref count );
		Assert.Equal( 1, count );
	}

	[Fact]
	public void InsufficientOutputCapacityDoesNotAdvanceHistory()
	{
		var set = Set( 1, "Path", Vector2.UnitY, Vector2.UnitX );
		var pipeline = new GestureInputPipeline( [ set ], new( 0, [] ), 0f );
		var count = 0;
		Assert.Throws<InvalidOperationException>( () => pipeline.Notify( default, 0, 1, [], ref count ) );
		var commands = new QueuedInputCommand[ 1 ];
		pipeline.Notify( Reading( default, Vector2.UnitY ), 0, 1, commands, ref count );
		pipeline.Notify( Reading( default, Vector2.UnitX ), 0, 1, commands, ref count );
		Assert.Equal( 0, count );
	}
}
