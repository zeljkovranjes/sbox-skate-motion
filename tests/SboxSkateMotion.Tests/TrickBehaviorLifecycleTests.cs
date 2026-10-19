using SkateMotion.Core.Animation;
using SkateMotion.Core.Input;
using System;
using Xunit;

namespace SboxSkateMotion.Tests;

public class TrickBehaviorLifecycleTests
{
	[Fact]
	public void TakeoffEntryCapturesHeightWhileParentMonitorSurvivesChildExit()
	{
		var continueKey = InputCommandKey.Encode( "CONTINUE"u8 );
		var catchKey = InputCommandKey.Encode( "CATCH"u8 );
		var speedKey = InputCommandKey.Encode( "GESTURESPEED"u8 );
		var requests = new InputRequestBuffer( 2 );
		requests.Add( speedKey, 0.75f );
		var monitor = new TrickContinuationMonitor( [ continueKey ], catchKey );
		var height = default(TrickHeightResult);
		var entries = 0;
		var settings = new TrickHeightSettings( default, null, true, false, false, false );
		var callbacks = new StateBehaviorCallbacks(
			_ => -1, _ => { }, _ => { },
			( definition, _ ) =>
			{
				if ( definition == 0 )
					monitor.Enter();
				else
				{
					height = TrickHeightController.Evaluate( requests, [], settings );
					entries++;
				}
			},
			( definition, _ ) =>
			{
				if ( definition == 0 )
					monitor.Exit();
			}, _ => { } );
		var lifecycle = new StateBehaviorLifecycle(
			[ new( 0, 0 ), new( 0, 1 ), new( 1, 1 ) ],
			[ new( 1, true ), new( 2, true ) ], callbacks, true );
		var scheduler = new StateBehaviorScheduler( [ [], [ 0 ], [ 0, 1 ] ],
			( operation, _, _ ) =>
			{
				if ( operation == 0 )
					monitor.Update( requests );
			}, -1 );

		lifecycle.Enter( [ 0, 1, 2 ] );
		requests.Add( continueKey, 0f );
		scheduler.Run( 2, lifecycle.Storage, true );
		Assert.False( monitor.ContinuationRequested );
		lifecycle.Exit( [ 2 ] );
		requests.Set( speedKey, 0.25f );
		scheduler.Run( 1, lifecycle.Storage, true );
		Assert.True( monitor.ContinuationRequested );
		Assert.Equal( 0.75f, height.Height );
		Assert.Equal( 1, entries );
		lifecycle.Exit( [ 1 ] );
		Assert.False( monitor.ContinuationRequested );
		Assert.Empty( lifecycle.Active.ToArray() );
	}

	[Fact]
	public void DisabledLifecycleCallbacksDoNotCaptureHeight()
	{
		var entries = 0;
		var callbacks = new StateBehaviorCallbacks( _ => -1, _ => { }, _ => { },
			( _, _ ) => entries++, ( _, _ ) => { }, _ => { } );
		var lifecycle = new StateBehaviorLifecycle( [ new( 0, 1 ) ], [ new( 0, true ) ], callbacks, false );
		lifecycle.Enter( [ 0 ] );
		Assert.Equal( 0, entries );
	}
}
