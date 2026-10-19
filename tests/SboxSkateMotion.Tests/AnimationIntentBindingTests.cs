using SkateMotion.Core.Animation;
using SkateMotion.Core.Input;
using Xunit;

namespace SboxSkateMotion.Tests;

public class AnimationIntentBindingTests
{
	[Fact]
	public void ExactRequestPresencePublishesZeroAndPreservesPhysicalQueueOrder()
	{
		var requests = new InputRequestBuffer( 3 );
		var key = new InputCommandKey( 1, 2, 3, 4, 5, 6 );
		var target = AnimationAttributeKey.Encode( "INTENT" );
		var output = new AnimationScalarOutput( new( 2, [] ), 4 );
		var binding = new AnimationIntentBinding( requests, output );
		var definition = new AnimationIntentDefinition( key, target, true );
		var lifecycle = binding.Bind();
		Assert.Equal( -1, lifecycle.Create() );
		lifecycle.Enter( -1 );
		lifecycle.Exit( -1 );
		lifecycle.Transition();
		requests.Add( new( 1, 2, 3, 4, 5, 7 ), 9f );
		binding.Update( definition );
		Assert.Equal( 0, output.QueuedCount );
		requests.Add( key, 0f );
		binding.Update( definition );
		requests.Set( key, -.75f );
		binding.Update( definition with { PublishAnimation = false } );
		Assert.Equal( 2, output.QueuedCount );
		Assert.Equal( 0f, output.Queued[ 0 ].Value );
		Assert.Equal( -.75f, output.Queued[ 1 ].Value );
		Assert.Equal( target, output.Queued[ 0 ].Name );
		Assert.Equal( 1, output.Attributes.Count );
		Assert.Equal( 0f, output.Attributes.Values[ 0 ].Attribute.Payload.Scalar );
		Assert.False( output.Attributes.Values[ 0 ].Persistent );
		requests.Clear();
		binding.Update( definition );
		Assert.Equal( 2, output.QueuedCount );
	}
}
