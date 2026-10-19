using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class AnimationTiltBehaviorBindingTests
{
	[Fact]
	public void SharedFlagsGatePhysicalReadsAndRetainedInstancesPublishIndependentValues()
	{
		var gameplay = new AnimationGameplayFlags( 0x03000000 );
		var actor = new AnimationPlayActorState( default, 0, 1UL << 30, default );
		var attributes = new AnimationAttributeOverrideBuffer( 1, [] );
		var reads = 0;
		var binding = new AnimationTiltBehaviorBinding( gameplay, actor,
			() => { reads++; return new( 1f, 0f, 0 ); }, Controller, attributes );
		var tilt = binding.Bind();
		var first = tilt.Create();
		var second = tilt.Create();
		var name = AnimationAttributeKey.Encode( "TILT" );
		binding.Update( name, first );
		Assert.Equal( 0, reads );
		Assert.Equal( 0, attributes.Count );
		var application = binding.BindApplication();
		Assert.Equal( -1, application.Create() );
		application.Transition();
		gameplay.Enter( AnimationGameplayFlag.Manual );
		binding.Update( name, first );
		binding.Update( name, first );
		Assert.Equal( .75f, attributes.Values[ 0 ].Attribute.Payload.Scalar );
		binding.Update( name, second );
		Assert.Equal( .25f, attributes.Values[ 0 ].Attribute.Payload.Scalar );
		Assert.False( attributes.Values[ 0 ].Persistent );
		Assert.Equal( -1, attributes.Values[ 0 ].Owner );
		application.Exit( -1 );
		Assert.Equal( 0x13000000u, gameplay.Word );
		Assert.True( gameplay.IsManualing );
		binding.Update( name, second );
		Assert.Equal( 3, reads );
		actor.Flags = 0;
		application.Enter( -1 );
		binding.Update( name, second );
		Assert.Equal( -.25f, attributes.Values[ 0 ].Attribute.Payload.Scalar );
	}

	[Fact]
	public void ApplicationExitAndReentryResetOnlyAfterTheInstanceObservesDisabledState()
	{
		var gameplay = new AnimationGameplayFlags( 0 );
		var actor = new AnimationPlayActorState( default, 0, 1UL << 30, default );
		var attributes = new AnimationAttributeOverrideBuffer( 1, [] );
		var binding = new AnimationTiltBehaviorBinding( gameplay, actor, () => new( 1f, 0f, 0 ), Controller, attributes );
		var tilt = binding.Bind();
		var instance = tilt.Create();
		var application = binding.BindApplication();
		var name = AnimationAttributeKey.Encode( "TILT" );
		application.Enter( -1 );
		binding.Update( name, instance );
		binding.Update( name, instance );
		application.Exit( -1 );
		application.Enter( -1 );
		tilt.Exit( instance );
		tilt.Enter( instance );
		tilt.Transition();
		binding.Update( name, instance );
		Assert.Equal( 1f, attributes.Values[ 0 ].Attribute.Payload.Scalar );
		application.Exit( -1 );
		binding.Update( name, instance );
		Assert.Equal( 1f, attributes.Values[ 0 ].Attribute.Payload.Scalar );
		application.Enter( -1 );
		binding.Update( name, instance );
		Assert.Equal( .25f, attributes.Values[ 0 ].Attribute.Payload.Scalar );
	}

	private static TiltParameterController Controller() =>
		new( [ 0f, 1f, 2f, 3f ], [ 1f, 1f, 1f, 1f ], 1f, .25f, 1f, .25f );
}
