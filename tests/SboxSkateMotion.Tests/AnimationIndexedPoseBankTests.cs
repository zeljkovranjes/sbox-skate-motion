using System;
using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class AnimationIndexedPoseBankTests
{
	[Theory]
	[InlineData( 0, 0f )]
	[InlineData( 1, 0f )]
	[InlineData( 5, 4f )]
	[InlineData( 30, 30f )]
	[InlineData( 61, 61f )]
	public void CacheUsesDiscretePlaybackTimingAndPreservesRawLanes( int index, float position )
	{
		var bank = new AnimationIndexedPoseBank( Clip() );
		var pose = bank.Sample( index );

		Assert.Equal( 62, bank.Count );
		Assert.Equal( 1, bank.BoneCount );
		Assert.Equal( new Vector3( position, 0f, 0f ), pose.Positions[ 0 ] );
		Assert.Equal( new Quaternion( 0f, 0f, 0f, 2f ), pose.Rotations[ 0 ] );
		Assert.Equal( .25f, pose.BoneWeights[ 0 ] );
		Assert.Equal( new Vector4( 2f, 3f, 4f, 5f ), pose.Scales[ 0 ] );
	}

	[Fact]
	public void ReusedSelectionCannotOverwriteCachedFramesOrStackSnapshots()
	{
		var bank = new AnimationIndexedPoseBank( Clip() );
		var selected = bank.Sample( 0 );
		selected.Positions[ 0 ] = new Vector3( 999f );
		Assert.Same( selected, bank.Sample( 0 ) );
		Assert.Equal( Vector3.Zero, selected.Positions[ 0 ] );
		var stack = new AnimationPoseStack( 1, 2 );
		var operations = new AnimationPoseCommandBufferOperations( stack, bank.Sample );
		operations.Begin();
		operations.Sample( 30 );
		operations.Sample( 61 );
		Assert.Equal( new Vector3( 30f, 0f, 0f ), stack.Pose.Positions[ 0 ] );
		operations.Blend( 1f );
		Assert.Equal( new Vector3( 61f, 0f, 0f ), stack.Pose.Positions[ 0 ] );
		Assert.Equal( Vector3.Zero, bank.Sample( 0 ).Positions[ 0 ] );
	}

	[Fact]
	public void InvalidIndexLeavesTheLastSelectionIntact()
	{
		var bank = new AnimationIndexedPoseBank( Clip() );
		var selected = bank.Sample( 61 );
		Assert.Throws<ArgumentOutOfRangeException>( () => bank.Sample( -1 ) );
		Assert.Throws<ArgumentOutOfRangeException>( () => bank.Sample( 62 ) );
		Assert.Equal( new Vector3( 61f, 0f, 0f ), selected.Positions[ 0 ] );
		Assert.Throws<ArgumentNullException>( () => new AnimationIndexedPoseBank( null ) );
	}

	private static RawAnimationClip Clip()
	{
		var keys = new Vector4[ 64 ];
		for ( var index = 0; index < 62; index++ )
			keys[ index ] = new Vector4( index, 0f, 0f, .25f );
		keys[ 62 ] = new Vector4( 0f, 0f, 0f, 2f );
		keys[ 63 ] = new Vector4( 2f, 3f, 4f, 5f );
		return new RawAnimationClip( "poses", 60f, 1f, 62, 0, 0,
			new[] { 0, 62, 63 }, new[] { 62, 1, 1 }, keys, null, null,
			AnimationClipSamplingMode.Continuous );
	}
}
