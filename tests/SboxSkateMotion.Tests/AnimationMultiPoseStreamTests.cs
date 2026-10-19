using System;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class AnimationMultiPoseStreamTests
{
	[Fact]
	public void MultiInputCommandsIncludeUnregisteredStreams()
	{
		var context = new AnimationPoseStreamContext( 16 );
		for ( var slot = 0; slot < 3; slot++ )
		{
			context.Append( slot, 2u << 23 );
			context.Append( slot, 6u << 23 );
		}
		context.QueueBlendMany( new[] { .25f, .75f } );
		Assert.Equal( 0, context.NameCount );
		for ( var slot = 0; slot < 3; slot++ )
			Assert.Equal( new[] { 2u << 23, 6u << 23, 0x4003ffffu, 0x400bffffu, 0x80200000u },
				context.Commands( slot ).ToArray() );
	}

	[Fact]
	public void EqualInitialInputsRetainTheirCombineCommand()
	{
		var context = new AnimationPoseStreamContext( 16 );
		for ( var slot = 0; slot < 3; slot++ )
		{
			context.Append( slot, 0 );
			context.Append( slot, 0 );
		}
		context.QueueBlendMany( new[] { 1f, 0f } );
		Assert.Equal( new[] { 0u, 0u, 0x400fffffu, 0x40000000u, 0x80200000u }, context.Commands( 0 ).ToArray() );
	}

	[Theory]
	[InlineData( 1, 2 )]
	[InlineData( 2, 3 )]
	[InlineData( 3, 2 )]
	[InlineData( 4, 2 )]
	[InlineData( 5, 2 )]
	public void EqualInputsReduceOnlyAfterAPrecedingCommand( int inputs, int remaining )
	{
		var context = new AnimationPoseStreamContext( 32 );
		for ( var slot = 0; slot < 3; slot++ )
		{
			context.Append( slot, 7u << 23 );
			for ( var input = 0; input < inputs; input++ )
				context.Append( slot, 2u << 23 );
		}
		context.QueueBlendMany( new float[ inputs ] );
		for ( var slot = 0; slot < 3; slot++ )
		{
			Assert.Equal( remaining, context.Commands( slot ).Length );
			Assert.Equal( 7u << 23, context.Commands( slot )[ 0 ] );
			Assert.Equal( 2u << 23, context.Commands( slot )[ 1 ] );
		}
	}

	[Fact]
	public void RepeatedWeightWordsAreNotReducedAsValues()
	{
		var context = new AnimationPoseStreamContext( 16 );
		for ( var slot = 0; slot < 3; slot++ )
		{
			context.Append( slot, 0 );
			context.Append( slot, 0x40000000u );
			context.Append( slot, 0x40000000u );
		}
		context.QueueBlendMany( new[] { 0f, 1f } );
		Assert.Equal( 6, context.Commands( 0 ).Length );
		Assert.Equal( 0x80200000u, context.Commands( 0 )[ 5 ] );
	}

	[Fact]
	public void InvalidWeightsAreRejectedBeforeAnyStreamChanges()
	{
		var context = new AnimationPoseStreamContext( 16 );
		Assert.Throws<ArgumentOutOfRangeException>( () => context.QueueBlendMany( Array.Empty<float>() ) );
		Assert.Throws<ArgumentOutOfRangeException>( () => context.QueueBlendMany( new float[ 6 ] ) );
		Assert.Throws<ArgumentOutOfRangeException>( () => context.QueueBlendMany( new[] { .5f, float.NaN } ) );
		for ( var slot = 0; slot < 3; slot++ )
			Assert.True( context.Commands( slot ).IsEmpty );
	}
}
