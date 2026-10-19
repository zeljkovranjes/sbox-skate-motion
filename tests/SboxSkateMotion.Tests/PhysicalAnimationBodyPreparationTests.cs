using System;
using System.Linq;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class PhysicalAnimationBodyPreparationTests
{
	[Fact]
	public void MovesOnlyRequestedBodiesAndPreservesPartitionOrder()
	{
		var world = new PhysicalBodyPartitions { ModeTwoCount = 2, ModeFourCount = 1, Epoch = 29 };
		var bodies = new[] { Body( 2, world ), Body( 26, world ), Body( 4, world ), Body( 3, world ) };
		Link( world.ModeTwoHead, bodies[ 0 ].QueueLink, bodies[ 1 ].QueueLink );
		Link( world.ModeFourHead, bodies[ 2 ].QueueLink );
		PhysicalAnimationBodyPreparation.Update( bodies.Select( b => new PhysicalBodyBinding { Body = b } ).ToArray(), 4 );
		Assert.Equal( new[] { 4, 12, 4, 3 }, bodies.Select( b => b.MotionMode ) );
		Assert.All( bodies, b => Assert.Equal( 0, b.AccumulatorState ) );
		Assert.Equal( 0, world.ModeTwoCount );
		Assert.Equal( 3, world.ModeFourCount );
		Assert.Same( world.ModeTwoHead, world.ModeTwoHead.Next );
		Assert.Same( world.ModeTwoHead, world.ModeTwoHead.Previous );
		Assert.Same( bodies[ 1 ].QueueLink, world.ModeFourHead.Next );
		Assert.Same( bodies[ 0 ].QueueLink, bodies[ 1 ].QueueLink.Next );
		Assert.Same( bodies[ 2 ].QueueLink, bodies[ 0 ].QueueLink.Next );
		Assert.Same( bodies[ 2 ].QueueLink, world.ModeFourHead.Previous );
		Assert.Same( bodies[ 0 ].QueueLink, bodies[ 2 ].QueueLink.Previous );
	}

	[Fact]
	public void EmptyAndPartialCountsLeaveUnvisitedBodiesUntouched()
	{
		var first = Body( 1, null );
		var second = Body( 2, null );
		var bindings = new[] { new PhysicalBodyBinding { Body = first }, new PhysicalBodyBinding { Body = second } };
		PhysicalAnimationBodyPreparation.Update( bindings, 0 );
		Assert.Equal( 9, first.AccumulatorState );
		PhysicalAnimationBodyPreparation.Update( bindings, 1 );
		Assert.Equal( 0, first.AccumulatorState );
		Assert.Equal( 9, second.AccumulatorState );
		Assert.Equal( 2, second.MotionMode );
		Assert.Throws<ArgumentOutOfRangeException>( () => PhysicalAnimationBodyPreparation.Update( bindings, 3 ) );
	}

	[Fact]
	public void EachBodyUsesItsOwnWorldAndRepeatedPreparationDoesNotMoveItAgain()
	{
		var firstWorld = new PhysicalBodyPartitions { ModeTwoCount = 1 };
		var secondWorld = new PhysicalBodyPartitions { ModeTwoCount = 1 };
		var first = Body( 2, firstWorld );
		var second = Body( 2, secondWorld );
		Link( firstWorld.ModeTwoHead, first.QueueLink );
		Link( secondWorld.ModeTwoHead, second.QueueLink );
		var bodies = new[] { new PhysicalBodyBinding { Body = first }, new PhysicalBodyBinding { Body = second } };
		PhysicalAnimationBodyPreparation.Update( bodies, 2 );
		PhysicalAnimationBodyPreparation.Update( bodies, 2 );
		Assert.Same( first.QueueLink, firstWorld.ModeFourHead.Next );
		Assert.Same( second.QueueLink, secondWorld.ModeFourHead.Next );
		Assert.Equal( 1, firstWorld.ModeFourCount );
		Assert.Equal( 1, secondWorld.ModeFourCount );
	}

	private static PhysicalBodyState Body( int mode, PhysicalBodyPartitions world ) => new()
	{
		MotionMode = mode, AccumulatorState = 9, Partitions = world
	};

	private static void Link( PhysicalBodyQueueLink head, params PhysicalBodyQueueLink[] bodies )
	{
		var previous = head;
		foreach ( var body in bodies )
		{
			previous.Next = body;
			body.Previous = previous;
			previous = body;
		}
		previous.Next = head;
		head.Previous = previous;
	}
}
