using SkateMotion.Core.Animation;
using SkateMotion.Core.Input;
using System;
using Xunit;

namespace SboxSkateMotion.Tests;

public class TrickContinuationMonitorTests
{
	private static readonly InputCommandKey ContinueA = InputCommandKey.Encode( "CONTINUE_A"u8 );
	private static readonly InputCommandKey ContinueB = InputCommandKey.Encode( "CONTINUE_B"u8 );
	private static readonly InputCommandKey Catch = InputCommandKey.Encode( "CATCH"u8 );

	private static TrickContinuationMonitor Monitor() => new( [ ContinueA, ContinueB ], Catch );

	private static InputRequestBuffer Requests( params InputCommandKey[] keys )
	{
		var requests = new InputRequestBuffer( keys.Length );
		foreach ( var key in keys )
			requests.Add( key, 0f );
		return requests;
	}

	[Fact]
	public void EntrySkipsTheFirstUpdateAndPresenceDoesNotRequirePositiveValue()
	{
		var monitor = Monitor();
		monitor.Enter();
		monitor.Update( Requests( ContinueA ) );
		Assert.False( monitor.ContinuationRequested );
		monitor.Update( Requests( ContinueA ) );
		Assert.True( monitor.ContinuationRequested );
		Assert.False( monitor.CatchRequested );
	}

	[Fact]
	public void ContinuationHasPriorityOverCatchRegardlessOfRequestOrder()
	{
		var monitor = Monitor();
		monitor.Update( Requests() );
		monitor.Update( Requests( Catch, ContinueB ) );
		Assert.True( monitor.ContinuationRequested );
		Assert.False( monitor.CatchRequested );
	}

	[Fact]
	public void RequestsLatchIndependentlyUntilBehaviorExit()
	{
		var monitor = Monitor();
		monitor.Update( Requests() );
		monitor.Update( Requests( Catch ) );
		monitor.Update( Requests( ContinueA ) );
		monitor.Update( Requests() );
		Assert.True( monitor.ContinuationRequested );
		Assert.True( monitor.CatchRequested );
		monitor.Exit();
		Assert.False( monitor.ContinuationRequested );
		Assert.False( monitor.CatchRequested );
		Assert.Equal( 4, monitor.UpdateCount );
	}

	[Fact]
	public void EnterResetsOnlyCounterAndExitPreservesUnrelatedFlags()
	{
		var monitor = Monitor();
		monitor.Flags = uint.MaxValue;
		monitor.UpdateCount = 13;
		monitor.Enter();
		Assert.Equal( 0, monitor.UpdateCount );
		Assert.Equal( uint.MaxValue, monitor.Flags );
		monitor.Exit();
		Assert.Equal( 0xff9fffffu, monitor.Flags );
	}

	[Fact]
	public void CounterWrapUsesSignedComparisonBeforeReadingRequests()
	{
		var monitor = Monitor();
		monitor.UpdateCount = int.MaxValue;
		monitor.Update( Requests( ContinueA, Catch ) );
		Assert.Equal( int.MinValue, monitor.UpdateCount );
		Assert.Equal( 0u, monitor.Flags );
	}

	[Fact]
	public void UnknownRequestsCannotLatchAndCallerCannotMutateConfiguredKeys()
	{
		var keys = new[] { ContinueA };
		var monitor = new TrickContinuationMonitor( keys, Catch );
		keys[ 0 ] = ContinueB;
		monitor.Update( Requests() );
		monitor.Update( Requests( ContinueB ) );
		Assert.False( monitor.ContinuationRequested );
		monitor.Update( Requests( ContinueA ) );
		Assert.True( monitor.ContinuationRequested );
	}

	[Fact]
	public void InvalidRequestBufferLeavesRetainedStateUntouched()
	{
		var monitor = Monitor();
		Assert.Throws<ArgumentNullException>( () => monitor.Update( null ) );
		Assert.Equal( 0, monitor.UpdateCount );
	}
}
