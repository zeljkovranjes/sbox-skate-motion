using System;
using System.Collections.Generic;
using SkateMotion.Core.Animation;
using SkateMotion.Core.Input;
using Xunit;

namespace SboxSkateMotion.Tests;

public class TrickBehaviorBindingTests
{
	private static readonly InputCommandKey Continue = InputCommandKey.Encode( "CONTINUE"u8 );
	private static readonly InputCommandKey Catch = InputCommandKey.Encode( "CATCH"u8 );
	private static readonly InputCommandKey Speed = InputCommandKey.Encode( "GESTURESPEED"u8 );

	[Fact]
	public void ParentMonitorAndEntryHeightRunThroughLifecycleAndScheduler()
	{
		var requests = new InputRequestBuffer( 3 );
		requests.Add( Speed, 0.75f );
		var heights = new List<TrickHeightResult>();
		var binding = Bind( [ TrickBehaviorKind.Continuation, TrickBehaviorKind.Height ], requests,
			( _, result ) => heights.Add( result ) );
		var lifecycle = new StateBehaviorLifecycle( [ new( 0, 1 ), new( 1, 1 ) ],
			[ new( 0, true ), new( 1, true ) ], binding.Callbacks, true );
		var scheduler = new StateBehaviorScheduler( [ [ 0 ], [ 0, 1 ] ], binding.Update, -1 );
		lifecycle.Enter( [ 0, 1 ] );
		requests.Add( Continue, 0f );
		scheduler.Run( 1, lifecycle.Storage, true );
		Assert.Equal( 0u, binding.Flags );
		lifecycle.Exit( [ 1 ] );
		requests.Set( Speed, 0.25f );
		scheduler.Run( 0, lifecycle.Storage, true );
		Assert.Equal( 1u << 22, binding.Flags );
		Assert.Single( heights );
		Assert.Equal( 0.75f, heights[ 0 ].Height );
		lifecycle.Exit( [ 0 ] );
		Assert.Equal( 0u, binding.Flags );
	}

	[Fact]
	public void InstancesHaveIndependentCountersAndShareOwnerFlags()
	{
		var requests = new InputRequestBuffer( 2 );
		requests.Add( Continue, 0f );
		var binding = Bind( [ TrickBehaviorKind.Continuation ], requests );
		var first = binding.Callbacks.Create( 0 );
		var second = binding.Callbacks.Create( 0 );
		binding.Callbacks.Enter( 0, first );
		binding.Callbacks.Enter( 0, second );
		binding.Update( 0, 0, first );
		binding.Update( 0, 0, second );
		Assert.Equal( 0u, binding.Flags );
		binding.Update( 0, 0, first );
		requests.Clear();
		requests.Add( Catch, 0f );
		binding.Update( 0, 0, second );
		Assert.Equal( (1u << 22) | (1u << 21), binding.Flags );
		binding.Flags |= 1u << 5;
		binding.Callbacks.Exit( 0, first );
		Assert.Equal( 1u << 5, binding.Flags );
		binding.Update( 0, 0, second );
		Assert.Equal( (1u << 5) | (1u << 21), binding.Flags );
	}

	[Fact]
	public void TransitionHeightEvaluatesEntryWithoutCreatingAnInstance()
	{
		var requests = new InputRequestBuffer( 1 );
		requests.Add( Speed, 0.5f );
		var values = new List<float>();
		var binding = Bind( [ TrickBehaviorKind.Height ], requests, ( _, result ) => values.Add( result.Height ) );
		Assert.Equal( -1, binding.Callbacks.Create( 0 ) );
		binding.Callbacks.Transition( 0 );
		requests.Set( Speed, 0.25f );
		binding.Update( 0, 0, -1 );
		binding.Callbacks.Transition( 0 );
		Assert.Equal( new[] { 0.5f, 0.25f }, values );
	}

	[Fact]
	public void TransitionMonitorResetsCurrentInstanceInsteadOfAllocating()
	{
		var requests = new InputRequestBuffer( 1 );
		requests.Add( Continue, 0f );
		var binding = Bind( [ TrickBehaviorKind.Continuation ], requests );
		var instance = binding.Callbacks.Create( 0 );
		binding.Update( 0, 0, instance );
		binding.Update( 0, 0, instance );
		binding.Callbacks.Transition( 0 );
		binding.Flags = 0;
		binding.Update( 0, 0, instance );
		Assert.Equal( 0u, binding.Flags );
		binding.Update( 0, 0, instance );
		Assert.Equal( 1u << 22, binding.Flags );
	}

	[Fact]
	public void ExternalCallbacksReceiveOriginalHandleAndAllReferenceChanges()
	{
		var events = new List<string>();
		var callbacks = new StateBehaviorCallbacks(
			definition => { events.Add( $"create:{definition}" ); return 57; },
			instance => events.Add( $"retain:{instance}" ),
			instance => events.Add( $"release:{instance}" ),
			( definition, instance ) => events.Add( $"enter:{definition}:{instance}" ),
			( definition, instance ) => events.Add( $"exit:{definition}:{instance}" ),
			definition => events.Add( $"transition:{definition}" ) );
		var binding = new TrickBehaviorBinding( [ TrickBehaviorKind.External ], [ TrickBehaviorKind.External ],
			[ Continue ], Catch, new( 0 ), () => [], _ => default, ( _, _ ) => { }, callbacks,
			( operation, definition, instance ) => events.Add( $"update:{operation}:{definition}:{instance}" ) );
		var handle = binding.Callbacks.Create( 0 );
		binding.Callbacks.Retain( handle );
		binding.Callbacks.Enter( 0, handle );
		binding.Update( 0, 0, handle );
		binding.Callbacks.Exit( 0, handle );
		binding.Callbacks.Transition( 0 );
		binding.Callbacks.Release( handle );
		binding.Callbacks.Release( handle );
		Assert.Equal( new[] { "create:0", "retain:57", "enter:0:57", "update:0:0:57",
			"exit:0:57", "transition:0", "release:57", "release:57" }, events );
		Assert.Throws<InvalidOperationException>( () => binding.Callbacks.Release( handle ) );
	}

	[Fact]
	public void UpdateUsesCompiledOperationRatherThanDefinitionFamily()
	{
		var requests = new InputRequestBuffer( 1 );
		requests.Add( Continue, 0f );
		var binding = Bind( [ TrickBehaviorKind.Continuation, TrickBehaviorKind.Height ], requests );
		var handle = binding.Callbacks.Create( 0 );
		binding.Update( 0, 1, handle );
		binding.Update( 0, 1, handle );
		Assert.Equal( 1u << 22, binding.Flags );
	}

	[Fact]
	public void UnusedRetainedLifecycleSlotKeepsItsInstanceUntilOverwritten()
	{
		var requests = new InputRequestBuffer( 1 );
		requests.Add( Continue, 0f );
		var binding = Bind( [ TrickBehaviorKind.Continuation ], requests );
		var lifecycle = new StateBehaviorLifecycle( [ new( 0, 1 ) ], [ new( 0, true ) ], binding.Callbacks, true );
		lifecycle.Enter( [ 0 ] );
		var old = lifecycle.Storage[ 0 ].Instance;
		lifecycle.Exit( [ 0 ] );
		binding.Update( 0, 0, old );
		lifecycle.Enter( [ 0 ] );
		Assert.NotEqual( old, lifecycle.Storage[ 0 ].Instance );
		Assert.Throws<InvalidOperationException>( () => binding.Update( 0, 0, old ) );
	}

	private static TrickBehaviorBinding Bind( TrickBehaviorKind[] kinds, InputRequestBuffer requests,
		Action<int, TrickHeightResult> publish = null )
	{
		var external = new StateBehaviorCallbacks( _ => throw new InvalidOperationException(),
			_ => throw new InvalidOperationException(), _ => throw new InvalidOperationException(),
			( _, _ ) => throw new InvalidOperationException(), ( _, _ ) => throw new InvalidOperationException(),
			_ => throw new InvalidOperationException() );
		return new( kinds, kinds, [ Continue ], Catch, requests, () => [],
			_ => new( default, null, true, false, false, false ), publish ?? (( _, _ ) => { }),
			external, ( _, _, _ ) => throw new InvalidOperationException() );
	}
}
