using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public sealed class StateBehaviorSchedulerTests
{
	[Fact]
	public void StateSelectsOperationOrderAndPublishesInstanceBeforeEachCall()
	{
		var events = new List<(int, int, int, int)>();
		StateBehaviorScheduler scheduler = null!;
		scheduler = new( [[7, 2], [3, 7]],
			( operation, definition, instance ) => events.Add( (operation, definition, instance, scheduler.CurrentInstance) ), 91 );
		scheduler.Run( 1, [new( 5, 20 ), new( 9, 30 )], true );
		Assert.Equal( new[] { (3, 5, 20, 20), (7, 9, 30, 30) }, events );
		Assert.Equal( 30, scheduler.CurrentInstance );
	}

	[Fact]
	public void EmptyAndDisabledSchedulesPreserveSharedContext()
	{
		var scheduler = new StateBehaviorScheduler( [[], [1]],
			( _, _, _ ) => throw new Exception( "No operation expected." ), 37 );
		scheduler.Run( 0, [], true );
		scheduler.Run( -1, [], false );
		Assert.Equal( 37, scheduler.CurrentInstance );
	}

	[Fact]
	public void ScheduleUsesOnlyItsFixedStoragePrefixAndPreservesNullInstances()
	{
		var events = new List<(int, int)>();
		var scheduler = new StateBehaviorScheduler( [[4, 5]],
			( _, definition, instance ) => events.Add( (definition, instance) ), 0 );
		scheduler.Run( 0, [new( 1, 11 ), new( 2, -1 ), new( 3, 33 )], true );
		Assert.Equal( new[] { (1, 11), (2, -1) }, events );
		Assert.Equal( -1, scheduler.CurrentInstance );
	}

	[Fact]
	public void CallbackContextChangesSurviveUntilTheNextOperation()
	{
		StateBehaviorScheduler scheduler = null!;
		var observed = new List<int>();
		scheduler = new( [[0, 1]], ( _, _, _ ) =>
		{
			observed.Add( scheduler.CurrentInstance );
			scheduler.CurrentInstance = 99;
		}, -1 );
		scheduler.Run( 0, [new( 0, 10 ), new( 1, 20 )], true );
		Assert.Equal( new[] { 10, 20 }, observed );
		Assert.Equal( 99, scheduler.CurrentInstance );
	}

	[Fact]
	public void InvalidInputsFailBeforePublishingOrInvokingAnything()
	{
		var count = 0;
		var scheduler = new StateBehaviorScheduler( [[0, 1]], ( _, _, _ ) => count++, 73 );
		Assert.Throws<ArgumentOutOfRangeException>( () => scheduler.Run( 1, [], true ) );
		Assert.Throws<ArgumentException>( () => scheduler.Run( 0, [new( 0, 1 )], true ) );
		Assert.Equal( 73, scheduler.CurrentInstance );
		Assert.Equal( 0, count );
		Assert.Throws<ArgumentException>( () => new StateBehaviorScheduler( [[-1]], ( _, _, _ ) => { }, -1 ) );
	}

	[Fact]
	public void ReusedSchedulesDoNotAllocate()
	{
		var scheduler = new StateBehaviorScheduler( [[0, 1], [2]], static ( _, _, _ ) => { }, -1 );
		var storage = new[] { new ActiveStateBehavior( 1, 4 ), new( 2, 5 ) };
		for ( var index = 0; index < 100; index++ )
			scheduler.Run( index & 1, storage, true );
		var before = GC.GetAllocatedBytesForCurrentThread();
		for ( var index = 0; index < 1000; index++ )
			scheduler.Run( index & 1, storage, true );
		Assert.Equal( 0, GC.GetAllocatedBytesForCurrentThread() - before );
	}
}
