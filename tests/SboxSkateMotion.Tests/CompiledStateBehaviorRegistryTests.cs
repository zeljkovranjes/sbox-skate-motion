using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public sealed class CompiledStateBehaviorRegistryTests
{
	[Fact]
	public void EqualLocalHandlesStayDistinctAndSharedInstancesKeepOriginalReferences()
	{
		var references = new int[ 2 ];
		var events = new List<string>();
		var lifetimes = Enumerable.Range( 0, 2 ).Select( family =>
			new CompiledBehaviorInstanceLifetime( local =>
			{
				Assert.Equal( 0, local );
				references[ family ]++;
			}, local =>
			{
				Assert.Equal( 0, local );
				references[ family ]--;
			} ) ).ToArray();
		var registry = new CompiledStateBehaviorRegistry( 3, 3 );
		for ( var id = 0; id < 3; id++ )
		{
			var definition = id;
			var family = id == 1 ? 1 : 0;
			registry.RegisterBehavior( id, new( () => { references[ family ]++; return 0; },
				local => events.Add( $"enter:{definition}:{local}" ),
				local => events.Add( $"exit:{definition}:{local}" ), () => { } ), lifetimes[ family ] );
			registry.RegisterOperation( id, ( behavior, local ) =>
				events.Add( $"update:{behavior}:{local}" ), lifetimes[ family ] );
		}
		var transition = false;
		var runtime = registry.Build( Definition(), [( _, _ ) => transition], () => { }, true );
		runtime.Update( .5f );
		var first = runtime.Lifecycle.Active[ 0 ].Instance;
		var second = runtime.Lifecycle.Active[ 1 ].Instance;
		Assert.NotEqual( first, second );
		Assert.Equal( new[] { 1, 1 }, references );
		Assert.Equal( new[] { "enter:0:0", "enter:1:0", "update:0:0", "update:1:0" }, events );
		events.Clear();
		transition = true;
		runtime.Update( .25f );
		Assert.Equal( first, runtime.Lifecycle.Active[ 1 ].Instance );
		Assert.Equal( new[] { 2, 0 }, references );
		Assert.Equal( new[] { "exit:1:0", "enter:2:0", "update:0:0", "update:2:0" }, events );
		// Clearing active entries preserves the original retained storage references.
		runtime.Lifecycle.ClearActive();
		Assert.Equal( new[] { 2, 0 }, references );
		runtime.Lifecycle.Enter( [ 1 ] );
		Assert.Equal( new[] { 1, 1 }, references );
		Assert.NotEqual( first, runtime.Lifecycle.Active[ 0 ].Instance );
	}

	[Fact]
	public void CompiledScheduleSharesTiltActivationAndScalarOutputWithRetainedLifetime()
	{
		var flags = new AnimationGameplayFlags( 0 );
		var attributes = new AnimationAttributeOverrideBuffer( 2, [] );
		var scalarOutput = new AnimationScalarOutput( attributes, 8 );
		var scalar = new AnimationScalarBehaviorBinding( scalarOutput );
		var name = AnimationAttributeKey.Encode( "TILT" );
		var scalarName = AnimationAttributeKey.Encode( "HEIGHT" );
		var tilt = new AnimationTiltBehaviorBinding( flags,
			new( default, 0, 1UL << 30, default ), () => new( 1f, 0f, 0 ),
			() => new( [ 0f, 1f, 2f, 3f ], [ 1f, 1f, 1f, 1f ], 1f, .25f, 1f, .25f ), attributes );
		var scalarDefinition = new AnimationScalarBehaviorDefinition( scalarName, 1f, 2f, 3f, false );
		var registry = new CompiledStateBehaviorRegistry( 3, 3 );
		var lifetime = new CompiledBehaviorInstanceLifetime( tilt.Retain, tilt.Release );
		registry.RegisterBehavior( 0, tilt.Bind(), lifetime );
		registry.RegisterBehavior( 1, tilt.BindApplication() );
		registry.RegisterBehavior( 2, scalar.Bind( scalarDefinition ) );
		registry.RegisterOperation( 0, ( _, instance ) => tilt.Update( name, instance ), lifetime );
		registry.RegisterOperation( 1, ( _, _ ) => flags.Update() );
		registry.RegisterOperation( 2, ( _, _ ) => scalar.Update( scalarDefinition ) );
		var transition = false;
		var runtime = registry.Build( Definition(), [( _, _ ) => transition], () => { }, true );
		runtime.Update( .1f );
		Assert.True( flags.IsBodyTiltEnabled );
		Assert.Equal( .25f, attributes.Values[ 0 ].Attribute.Payload.Scalar );
		runtime.Update( .1f );
		Assert.Equal( .75f, attributes.Values[ 0 ].Attribute.Payload.Scalar );
		transition = true;
		runtime.Update( .1f );
		Assert.False( flags.IsBodyTiltEnabled );
		Assert.Equal( .75f, attributes.Values[ 0 ].Attribute.Payload.Scalar );
		Assert.Equal( new[] { 1f, 2f }, scalarOutput.Queued.ToArray().Select( value => value.Value ) );
		runtime.Lifecycle.ClearActive();
		Assert.Equal( 3f, scalarOutput.Queued[ 2 ].Value );
		runtime.Lifecycle.Enter( [ 2 ] );
		// Slot replacement releases the old tilt state; a new allocation starts clean.
		Assert.Throws<InvalidOperationException>( () => tilt.Update( name, 0 ) );
		var reused = tilt.Bind().Create();
		Assert.Equal( 0, reused );
		flags.Enter( AnimationGameplayFlag.BodyTilt );
		tilt.Update( name, reused );
		Assert.Equal( .25f, attributes.Values[ 0 ].Attribute.Payload.Scalar );
		tilt.Release( reused );
	}

	[Fact]
	public void IncompleteOrDuplicateRegistrationCannotSilentlyBuildAPlayableGraph()
	{
		var registry = new CompiledStateBehaviorRegistry( 3, 3 );
		var calls = 0;
		var binding = new CompiledStateBehaviorBinding( () => { calls++; return -1; },
			_ => { }, _ => { }, () => { } );
		registry.RegisterBehavior( 0, binding );
		Assert.Throws<InvalidOperationException>( () => registry.RegisterBehavior( 0, binding ) );
		Assert.Throws<ArgumentException>( () => registry.Build( Definition(), [( _, _ ) => false], () => { }, true ) );
		Assert.Equal( 0, calls );
		registry.RegisterBehavior( 1, binding );
		registry.RegisterBehavior( 2, binding );
		for ( var i = 0; i < 3; i++ )
			registry.RegisterOperation( i, ( _, _ ) => { } );
		registry.Build( Definition(), [( _, _ ) => false], () => { }, true );
		Assert.Throws<InvalidOperationException>( () => registry.RegisterOperation( 0, ( _, _ ) => { } ) );
		Assert.Throws<InvalidOperationException>( () => registry.Build( Definition(), [( _, _ ) => false], () => { }, true ) );
		Assert.Equal( 0, calls );
	}

	private static CompiledStateGraphDefinition Definition() => new(
		[
			new( -1, -1, 1, 2, 0, 0, true, StateSelectionMode.Reselect ),
			new( 0, -1, -1, 0, 0, 1, true, StateSelectionMode.Retain ),
			new( 0, -1, -1, 0, 0, 0, true, StateSelectionMode.Retain )
		], [ 0 ], [ new( 1, 2, 0 ) ], [ [] ],
		[ new( 0, 1 ), new( 1, 1 ), new( 2, 1 ) ],
		[ new( 0, true ), new( 1, true ), new( 2, true ) ],
		[ [ 0 ], [ 0, 1 ], [ 0, 2 ] ] );
}
