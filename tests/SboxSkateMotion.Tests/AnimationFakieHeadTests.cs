using SkateMotion.Core.Animation;
using SkateMotion.Core.Input;
using Xunit;

namespace SboxSkateMotion.Tests;

public sealed class AnimationFakieHeadTests
{
	private static readonly AnimationFakieHeadDefinition Definition = new(
		new( 11, 22, 33, 44, 55, 66 ), new( 77, 88, 99, 111, 222, 333 ) );

	[Fact]
	public void RetainedFlagControlsRequestsAndEntryDoesNotResetTheScalar()
	{
		var context = new Context { Primary = true };
		var instance = new AnimationFakieHeadInstance();
		instance.Update( Definition, context );
		Assert.Equal( new[] { "primary", "fakie" }, context.Events );
		context.Events.Clear();
		context.Fakie = true;
		instance.Update( Definition, context );
		Assert.Equal( new[] { "primary", "fakie", "start", "scalar" }, context.Events );
		Assert.Equal( Definition, context.Started );
		Assert.Equal( 1f, instance.Value );

		context.Events.Clear();
		context.Primary = false;
		context.Secondary = true;
		instance.Exit();
		instance.Enter();
		instance.Update( Definition, context );
		Assert.Equal( new[] { "primary", "secondary", "fakie", "scalar" }, context.Events );
		Assert.Equal( 0x3f7d70a4, BitConverter.SingleToInt32Bits( instance.Value ) );
		Assert.Equal( instance.Value, context.Value );
		Assert.True( instance.Active );

		context.Events.Clear();
		context.Fakie = false;
		instance.Update( Definition, context );
		Assert.Equal( new[] { "primary", "secondary", "fakie", "stop" }, context.Events );
		Assert.Equal( Definition.Channel, context.Stopped );
		Assert.False( instance.Active );
		Assert.Equal( 0x3f7d70a4, BitConverter.SingleToInt32Bits( instance.Value ) );

		context.Secondary = false;
		context.Fakie = true;
		instance.Update( Definition, context );
		Assert.Equal( .5f, instance.Value );
	}

	[Fact]
	public void CompiledDispatchRetainsHeadStateAndPreservesExternalHandles()
	{
		var context = new Context { Primary = true, Fakie = true };
		var externalEvents = new List<string>();
		var external = new StateBehaviorCallbacks(
			id => { externalEvents.Add( $"create:{id}" ); return 57; },
			id => externalEvents.Add( $"retain:{id}" ), id => externalEvents.Add( $"release:{id}" ),
			( definition, id ) => externalEvents.Add( $"enter:{definition}:{id}" ),
			( definition, id ) => externalEvents.Add( $"exit:{definition}:{id}" ),
			definition => externalEvents.Add( $"transition:{definition}" ) );
		var binding = new AnimationFakieHeadBinding( [Definition, null], [true, false], context, external,
			( operation, definition, instance ) => externalEvents.Add( $"update:{operation}:{definition}:{instance}" ) );
		var graphDefinition = new CompiledStateGraphDefinition(
			[new( -1, -1, -1, 0, 0, 0, true, StateSelectionMode.Retain )], [], [], [],
			[new( 0, 2 )], [new( 0, true ), new( 0, true )], [[0, 1]] );
		var runtime = new CompiledStateGraphRuntime( graphDefinition, [binding.Bind( 0 ), binding.Bind( 1 )],
			binding.Callbacks.Retain, binding.Callbacks.Release, [],
			[( id, instance ) => binding.Update( 0, id, instance ),
			 ( id, instance ) => binding.Update( 1, id, instance )], () => { }, true );
		runtime.Update( .1f );
		Assert.Single( context.Events, x => x == "start" );
		Assert.Contains( "enter:1:57", externalEvents );
		Assert.Contains( "update:1:1:57", externalEvents );
		context.Primary = false;
		context.Secondary = true;
		runtime.Update( .1f );
		Assert.Single( context.Events, x => x == "start" );
		Assert.Equal( 0x3f7d70a4, BitConverter.SingleToInt32Bits( context.Value ) );
		Assert.Throws<InvalidOperationException>( () => binding.Update( 0, 1, 0 ) );
		Assert.Throws<ArgumentNullException>( () => new AnimationFakieHeadBinding(
			[Definition], [true], context, default, ( _, _, _ ) => { } ) );
	}

	private sealed class Context : IAnimationFakieHeadContext
	{
		internal readonly List<string> Events = new();
		internal bool Primary;
		internal bool Secondary;
		internal bool Fakie;
		internal float Value;
		internal AnimationFakieHeadDefinition Started;
		internal InputCommandKey Stopped;

		public bool IsPrimaryInputActive()
		{
			Events.Add( "primary" );
			return Primary;
		}

		public bool IsSecondaryInputActive()
		{
			Events.Add( "secondary" );
			return Secondary;
		}

		public bool IsRidingFakie()
		{
			Events.Add( "fakie" );
			return Fakie;
		}

		public void StartChannel( InputCommandKey channel, InputCommandKey motion )
		{
			Events.Add( "start" );
			Started = new( channel, motion );
		}

		public void StopChannel( InputCommandKey channel )
		{
			Events.Add( "stop" );
			Stopped = channel;
		}

		public void SetParameter( AnimationAttributeKey name, float value, bool normalized, int owner )
		{
			Assert.Equal( AnimationAttributeKey.Encode( "torso" ), name );
			Assert.False( normalized );
			Assert.Equal( -1, owner );
			Events.Add( "scalar" );
			Value = value;
		}
	}
}
