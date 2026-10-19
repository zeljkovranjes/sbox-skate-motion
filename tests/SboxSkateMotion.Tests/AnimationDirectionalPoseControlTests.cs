using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public sealed class AnimationDirectionalPoseControlTests
{
	private sealed class Operations : IAnimationPoseOperations
	{
		internal readonly List<string> Events = new();
		internal bool FirstReady;
		internal bool SecondReady;
		internal float Weight;

		public bool Prepare( int child, bool enabled )
		{
			Events.Add( $"prepare:{child}:{enabled}" );
			return child == 0 ? FirstReady : SecondReady;
		}

		public void Blend( float weight )
		{
			Events.Add( "blend" );
			Weight = weight;
		}

		public void Sample( int child ) => Events.Add( $"sample:{child}" );

		public void QueueBlend( float weight )
		{
			Events.Add( "queue" );
			Weight = weight;
		}
	}

	[Fact]
	public void DisabledPreparationStillCallsBothChildrenInOrder()
	{
		var operations = new Operations();
		var control = new AnimationDirectionalPoseControl( operations );
		Assert.False( control.Prepare( 0, 1, .5f, .3f, true, true, false ) );
		Assert.Equal( new[] { "prepare:0:False", "prepare:1:False" }, operations.Events );
		Assert.Equal( .3f, control.Threshold );
	}

	[Theory]
	[InlineData( false, false, false, false )]
	[InlineData( true, false, true, false )]
	[InlineData( false, true, true, false )]
	[InlineData( true, true, true, true )]
	public void PreparationCombinesOnlyTwoValidChildren( bool first, bool second, bool valid, bool blend )
	{
		var operations = new Operations { FirstReady = first, SecondReady = second };
		var control = new AnimationDirectionalPoseControl( operations );
		Assert.Equal( valid, control.Prepare( 0, 1, .5f, .3f, true, true, true ) );
		Assert.Equal( valid, control.HasPose );
		Assert.Equal( blend, operations.Events.Contains( "blend" ) );
	}

	[Fact]
	public void EndpointCurveChangesImmediateWeightAndActivation()
	{
		var operations = new Operations { FirstReady = true, SecondReady = true };
		var control = new AnimationDirectionalPoseControl( operations );
		control.Prepare( 0, 1, .4f, .3f, true, false, true );
		Assert.Equal( .1875f, operations.Weight, 5 );
		operations.Events.Clear();
		control.Prepare( 0, 1, .2f, .3f, true, false, true );
		Assert.Equal( new[] { "prepare:0:True", "prepare:1:False", "blend" }, operations.Events );
		Assert.Equal( 0f, operations.Weight );
	}

	[Fact]
	public void InteriorPairsRetainRawImmediateWeight()
	{
		var operations = new Operations { FirstReady = true, SecondReady = true };
		var control = new AnimationDirectionalPoseControl( operations );
		control.Prepare( 0, 1, .4f, .3f, false, false, true );
		Assert.Equal( .4f, operations.Weight );
	}

	[Fact]
	public void DeferredBlendUsesRemappedPhaseInsteadOfImmediateCurve()
	{
		var operations = new Operations();
		var control = new AnimationDirectionalPoseControl( operations );
		control.Sample( 0, 1, .4f, true, false );
		Assert.Equal( new[] { "sample:0", "sample:1", "queue" }, operations.Events );
		Assert.Equal( .25f, operations.Weight, 6 );
	}

	[Fact]
	public void PositiveRawWeightStillSamplesSecondChildWhenQueuedWeightIsZero()
	{
		var operations = new Operations();
		var control = new AnimationDirectionalPoseControl( operations );
		control.Sample( 0, 1, .1f, true, false );
		Assert.Equal( new[] { "sample:0", "sample:1", "queue" }, operations.Events );
		Assert.Equal( 0f, operations.Weight );
	}

	[Fact]
	public void UpperEndpointSamplesOnlySecondAndDoesNotChangePreparationState()
	{
		var operations = new Operations { SecondReady = true };
		var control = new AnimationDirectionalPoseControl( operations );
		control.Prepare( 0, 1, .5f, .2f, true, true, true );
		operations.Events.Clear();
		control.Sample( 0, 1, 1f, false, true );
		Assert.Equal( new[] { "sample:1" }, operations.Events );
		Assert.True( control.HasPose );
		Assert.Equal( .2f, control.Threshold );
	}
}
