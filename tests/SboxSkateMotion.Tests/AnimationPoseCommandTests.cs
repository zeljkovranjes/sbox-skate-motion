using System;
using System.Collections.Generic;
using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class AnimationPoseCommandTests
{
	[Theory]
	[InlineData( 0x80000000u )]
	[InlineData( 0xc0000000u )]
	public void BothCombineOpcodesUseTheQueuedWeight( uint combine )
	{
		var operations = new Recorder();
		var executor = new AnimationPoseCommandExecutor( operations );
		var result = executor.Execute( new[] { Value( 7 ), Value( 127 ), 0x7fffffffu, combine }, 10 );
		Assert.Equal( new[] { 7, 127 }, operations.Values );
		Assert.Equal( new[] { 1f }, operations.Weights );
		Assert.Equal( new AnimationPoseCommandResult( 1, 2, 0, 4 ), result );
	}

	[Fact]
	public void ValuesDoNotStopAtTheSampleLimit()
	{
		var result = new AnimationPoseCommandExecutor( new Recorder() ).Execute(
			new[] { Value( 0 ), Value( 1 ), Value( 2 ) }, 0 );
		Assert.Equal( new AnimationPoseCommandResult( 3, 3, 0, 3 ), result );
	}

	[Fact]
	public void EarlyExitRetainsCurrentCombineIndexAndWeightCount()
	{
		var operations = new Recorder();
		var result = new AnimationPoseCommandExecutor( operations ).Execute(
			new[] { Value( 0 ), Value( 1 ), 0x40080000u, 0x80200000u, Value( 2 ) }, 1 );
		Assert.Equal( new AnimationPoseCommandResult( 1, 2, 1, 3 ), result );
		Assert.Equal( new[] { 0, 1 }, operations.Values );
		Assert.Equal( 0.5000004768371582f, operations.Weights[ 0 ] );
	}

	[Fact]
	public void ExactLimitContinuesAndClearsWeights()
	{
		var result = new AnimationPoseCommandExecutor( new Recorder() ).Execute(
			new[] { Value( 0 ), Value( 1 ), 0x40000000u, 0x80200000u, Value( 2 ) }, 2 );
		Assert.Equal( new AnimationPoseCommandResult( 2, 3, 0, 5 ), result );
	}

	[Fact]
	public void CombineWithOnePoseDiscardsItsWeightWithoutBlending()
	{
		var operations = new Recorder();
		var result = new AnimationPoseCommandExecutor( operations ).Execute(
			new[] { Value( 7 ), 0x400fffffu, 0x80000000u }, 100 );
		Assert.Empty( operations.Weights );
		Assert.Empty( operations.Groups );
		Assert.Equal( new AnimationPoseCommandResult( 1, 1, 0, 3 ), result );
	}

	[Fact]
	public void MultiBlendReceivesAllWeightsInOrder()
	{
		var operations = new Recorder();
		var result = new AnimationPoseCommandExecutor( operations ).Execute(
			new[] { Value( 1 ), Value( 2 ), Value( 3 ), 0x40000000u, 0x40080000u, 0x400fffffu, 0x80000000u }, 100 );
		Assert.Equal( new[] { 0f, .5000004768371582f, 1f }, Assert.Single( operations.Groups ) );
		Assert.Equal( 1u, result.Depth );
	}

	[Fact]
	public void ReusingExecutorDoesNotRetainPreviousWeights()
	{
		var operations = new Recorder();
		var executor = new AnimationPoseCommandExecutor( operations );
		executor.Execute( new[] { 0x400fffffu }, 0 );
		executor.Execute( new[] { Value( 1 ), Value( 2 ), 0x80000000u }, 0 );
		Assert.Empty( operations.Weights );
		Assert.Equal( default, executor.Execute( ReadOnlySpan<uint>.Empty, 0 ) );
	}

	[Fact]
	public void ExtraWeightsCannotOverflowReusableStorage()
	{
		var executor = new AnimationPoseCommandExecutor( new Recorder() );
		Assert.Throws<InvalidOperationException>( () => executor.Execute(
			new[] { 0x40000000u, 0x40000000u, 0x40000000u, 0x40000000u, 0x40000000u, 0x40000000u }, 0 ) );
	}

	[Fact]
	public void FrameSelectionRetainsPreviousPositionAndRespectsPlaybackRate()
	{
		var clock = new AnimationClipClock( 61f, 30f, 1.5f, false, false );
		clock.SetRate( 2f );
		clock.Seek( .75f );
		clock.SelectFrame( 30 );
		Assert.Equal( .75f, clock.PreviousPosition );
		Assert.Equal( 1f / 3f, clock.Position );
		Assert.Equal( 1u, clock.WrapCount );
		clock.SelectFrame( 127 );
		Assert.Equal( 1f / 3f, clock.PreviousPosition );
		Assert.Equal( clock.Duration, clock.Position );
		Assert.Equal( 0u, clock.WrapCount );
	}

	[Theory]
	[InlineData( 0 )]
	[InlineData( 127 )]
	public void SingleFrameSelectionProducesZeroPositionWithoutErasingHistory( int frame )
	{
		var clock = new AnimationClipClock( 1f, 30f, 1f, false, false );
		clock.Seek( .75f );
		clock.SelectFrame( frame );
		Assert.Equal( 0f, clock.Position );
		Assert.Equal( .75f, clock.PreviousPosition );
		Assert.Equal( 1u, clock.WrapCount );
	}

	[Fact]
	public void CommandBufferSnapshotsRealClipFramesBeforeBlending()
	{
		var clip = new RawAnimationClip( "sample", 1f, 1f, 2, 0, 0,
			new[] { 0, 0, 2 }, new[] { 2, 0, 1 },
			new[] { new Vector4( 4f, 0f, 0f, .25f ), new Vector4( 12f, 0f, 0f, .75f ), new Vector4( 2f, 3f, 4f, 5f ) } );
		var leaf = new AnimationClipPlayback( clip, 0, new AnimationPoseSamplingContext( 1, false ) );
		var stack = new AnimationPoseStack( 1, 2 );
		var operations = new AnimationPoseCommandBufferOperations( stack, value =>
		{
			Assert.True( leaf.PrepareFrame( value ) );
			return leaf.Pose;
		} );
		operations.Begin();
		var result = new AnimationPoseCommandExecutor( operations ).Execute(
			new[] { Value( 1 ), Value( 0 ), 0x40080000u, 0x80200000u }, 100 );
		Assert.Equal( 1u, result.Depth );
		Assert.Equal( 1, operations.PreparedCount );
		Assert.InRange( operations.Pose.Positions[ 0 ].X, 7.99999f, 8f );
		Assert.Equal( new Vector4( 2f, 3f, 4f, 5f ), operations.Pose.Scales[ 0 ] );
		Assert.InRange( operations.Pose.BoneWeights[ 0 ], .49999f, .5f );
		Assert.Equal( 1f, leaf.Clock.PreviousPosition );
		Assert.Equal( 1u, leaf.Clock.WrapCount );
		Assert.Equal( 4f, leaf.Pose.Positions[ 0 ].X );
	}

	private static uint Value( int value ) => (uint)value << 23;

	private sealed class Recorder : IAnimationPoseCommandOperations
	{
		internal readonly List<int> Values = new();
		internal readonly List<float> Weights = new();
		internal readonly List<float[]> Groups = new();
		public void Sample( int value ) => Values.Add( value );
		public void Blend( float weight ) => Weights.Add( weight );
		public void BlendMany( ReadOnlySpan<float> weights ) => Groups.Add( weights.ToArray() );
	}
}
