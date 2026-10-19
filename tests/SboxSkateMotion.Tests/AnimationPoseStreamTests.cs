using System;
using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class AnimationPoseStreamTests
{
	private static readonly AnimationPoseStreamKey First = new( 1, 2, 3, 4, 5, 6 );
	private static readonly AnimationPoseStreamKey Second = new( 7, 8, 9, 10, 11, 12 );
	private static readonly AnimationPoseStreamKey Third = new( 13, 14, 15, 16, 17, 18 );

	[Fact]
	public void RegistrationPairsNamesAndLookupCanSwapThem()
	{
		var context = new AnimationPoseStreamContext( 16 );
		Assert.Equal( 0, context.Register( First, Second ) );
		Assert.Equal( 2, context.Register( Third, Third ) );
		Assert.Equal( 3, context.NameCount );
		Assert.Equal( 1, context.Find( Second ) );
		context.SwapPairs = true;
		Assert.Equal( 1, context.Find( First ) );
		Assert.Equal( 0, context.Find( Second ) );
		Assert.Equal( 2, context.Find( Third ) );
		Assert.Equal( -1, context.Find( First with { F = 99 } ) );
	}

	[Fact]
	public void ExistingNamesDoNotRewritePairingOrFollowSwapDuringRegistration()
	{
		var context = new AnimationPoseStreamContext( 16 );
		context.Register( First, Second );
		context.SwapPairs = true;
		Assert.Equal( 0, context.Register( First, Third ) );
		Assert.Equal( 2, context.NameCount );
		Assert.Equal( 1, context.GetPairedSlot( 0 ) );
	}

	[Fact]
	public void ResetClearsOccupancyAndSwapButRetainsRegisteredStorage()
	{
		var context = new AnimationPoseStreamContext( 16 );
		context.Register( First, Second );
		context.Append( 0, 42 );
		context.SwapPairs = true;
		context.Reset();
		Assert.Equal( 0, context.NameCount );
		Assert.False( context.SwapPairs );
		Assert.True( context.Commands( 0 ).IsEmpty );
		Assert.Equal( First, context.GetName( 0 ) );
		Assert.Equal( 1, context.GetPairedSlot( 0 ) );
		Assert.Equal( -1, context.Find( First ) );
	}

	[Fact]
	public void NegativeKeysParticipateInInterpolation()
	{
		var clock = Clock();
		var producer = Producer( clock, new( -8191, 2 ), new( 8191, 6 ) );
		var context = new AnimationPoseStreamContext( 16 );
		producer.Sample( context );
		Assert.Equal( new[] { 2u << 23, 6u << 23, 0x4007ffffu, 0x80200000u }, context.Commands( 0 ).ToArray() );
		Assert.Equal( new[] { 0u }, context.Commands( 1 ).ToArray() );
	}

	[Fact]
	public void BeforeFirstAndBeyondDurationUseAuthoredEndpointRules()
	{
		var clock = Clock();
		var producer = Producer( clock, new( 0, 2 ), new( 8191, 6 ) );
		var context = new AnimationPoseStreamContext( 16 );
		clock.Seek( -1f );
		producer.Sample( context );
		Assert.Equal( new[] { 2u << 23 }, context.Commands( 0 ).ToArray() );
		context.Reset();
		clock.Seek( 2f );
		producer.Sample( context );
		Assert.Equal( new[] { 2u << 23, 6u << 23, 0x400fffffu, 0x80200000u }, context.Commands( 0 ).ToArray() );
	}

	[Fact]
	public void NoMatchingKeyEmitsZeroInsteadOfHoldingTheLastSelection()
	{
		var clock = Clock();
		clock.Seek( 1f );
		var context = new AnimationPoseStreamContext( 16 );
		Producer( clock, new( 0, 2 ), new( 4095, 6 ) ).Sample( context );
		Assert.Equal( new[] { 0u }, context.Commands( 0 ).ToArray() );
	}

	[Fact]
	public void NoTracksStillEmitThreeZeroValuesWithoutRegisteringNames()
	{
		var context = new AnimationPoseStreamContext( 16 );
		new AnimationPoseStreamProducer( Clock(), Array.Empty<AnimationPoseStreamTrack>() ).Sample( context );
		context.QueueBlend( .5f );
		Assert.Equal( 0, context.NameCount );
		for ( var slot = 0; slot < 3; slot++ )
			Assert.Equal( new[] { 0u }, context.Commands( slot ).ToArray() );
	}

	[Fact]
	public void EmptyTrackRegistersItsPairAndEmitsFallbackValues()
	{
		var context = new AnimationPoseStreamContext( 16 );
		new AnimationPoseStreamProducer( Clock(), new[] { new AnimationPoseStreamTrack( First, Second,
			Array.Empty<AnimationPoseStreamKeyframe>() ) } ).Sample( context );
		Assert.Equal( 2, context.NameCount );
		Assert.Equal( new[] { 0u }, context.Commands( 0 ).ToArray() );
		Assert.Equal( new[] { 0u }, context.Commands( 1 ).ToArray() );
	}

	[Fact]
	public void ReductionUsesItsPrecedingWindow()
	{
		var context = new AnimationPoseStreamContext( 16 );
		context.Register( First, First );
		foreach ( var value in new uint[] { 1, 2, 3, 3, 4 } ) context.Append( 0, value << 23 );
		context.QueueBlend( .25f );
		Assert.Equal( new[] { 1u << 23, 2u << 23, 3u << 23, 3u << 23 }, context.Commands( 0 ).ToArray() );
	}

	[Fact]
	public void TrackCopiesInputAndRejectsUnsupportedOrderingOrSelection()
	{
		var keys = new[] { new AnimationPoseStreamKeyframe( -4, 7 ) };
		var track = new AnimationPoseStreamTrack( First, First, keys );
		keys[ 0 ] = new( 0, 99 );
		Assert.Equal( new AnimationPoseStreamKeyframe( -4, 7 ), track.Keys[ 0 ] );
		Assert.Throws<ArgumentException>( () => new AnimationPoseStreamTrack( First, First,
			new[] { new AnimationPoseStreamKeyframe( 2, 1 ), new AnimationPoseStreamKeyframe( 1, 2 ) } ) );
		Assert.Throws<ArgumentException>( () => new AnimationPoseStreamTrack( First, First,
			new[] { new AnimationPoseStreamKeyframe( 0, 128 ) } ) );
	}

	[Fact]
	public void CapacityGuardsDoNotOverwriteExistingRegistrationOrCommands()
	{
		var context = new AnimationPoseStreamContext( 1 );
		context.Register( First, Second );
		Assert.Throws<InvalidOperationException>( () => context.Register( Third, default ) );
		Assert.Equal( 2, context.NameCount );
		context.Append( 0, 42 );
		Assert.Throws<InvalidOperationException>( () => context.Append( 0, 99 ) );
		Assert.Equal( new uint[] { 42 }, context.Commands( 0 ).ToArray() );
	}

	[Fact]
	public void CompositeCallbacksGenerateAndExecuteOnTheClipClock()
	{
		var leaf = new AnimationClipPlayback( new RawAnimationClip( "sample", 1f, 1f, 2, 0, 0,
			new[] { 0, 0, 0 }, new[] { 2, 0, 0 }, new[] { Vector4.Zero, Vector4.One } ),
			0, new AnimationPoseSamplingContext( 0, false ) );
		var context = new AnimationPoseStreamContext( 64 );
		var producer = Producer( leaf.Clock, new( 0, 2 ), new( 8191, 6 ) );
		var composite = new AnimationPoseBufferOperations( new IAnimationPreparedPoseNode[] { leaf },
			_ => producer.Sample( context ), context.QueueBlend );
		composite.Sample( 0 );
		leaf.Seek( .5f );
		composite.Sample( 0 );
		composite.QueueBlend( .5f );
		var output = new AnimationPoseCommandBufferOperations( new AnimationPoseStack( 1, 8 ), value =>
		{
			var pose = new SkeletonPose( 1 );
			pose.SetTransforms( new[] { new AnimationBoneTransform( new Vector4( value, 0f, 0f, 1f ), Quaternion.Identity, Vector4.One ) } );
			return pose;
		} );
		output.Begin();
		var executor = new AnimationPoseCommandExecutor( output );
		Assert.True( executor.TryExecute( context, First, 100, out var result ) );
		Assert.Equal( 1u, result.Depth );
		Assert.InRange( output.Pose.Positions[ 0 ].X, 2.99999f, 3.00001f );
		Assert.False( executor.TryExecute( context, Third, 100, out result ) );
		Assert.Equal( default, result );
	}

	private static AnimationClipClock Clock() => new( 2f, 1f, 1f, false, false );
	private static AnimationPoseStreamProducer Producer( AnimationClipClock clock, params AnimationPoseStreamKeyframe[] keys ) =>
		new( clock, new[] { new AnimationPoseStreamTrack( First, First, keys ) } );
}
