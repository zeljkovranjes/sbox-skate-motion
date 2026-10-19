using System;
using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class BoardHandlingOwnerContextTests
{
	private static readonly AnimationAttributeKey Yaw = AnimationAttributeKey.Encode( "yaw" );
	private static readonly AnimationBoneTransform[] Reference =
	{
		new( new Vector4( 0, 0, 0, 1 ), Quaternion.Identity, Vector4.One )
	};

	[Fact]
	public void RetainedHandlingLeafUsesOwnersCurrentInterpolationPolicy()
	{
		var context = new AnimationOwnerPoseContext( new( 0, false ), 2, true, .1f );
		var tree = new BoardHandlingAnimationTree( new[] { Clip( 5f, 15f, 0f ) }, Reference, Yaw, context );
		tree.PoseRoot.Seek( .25f );
		Assert.True( tree.PoseRoot.Prepare( true, context.ConsumeHistory, context.SamplingFlags ) );
		Assert.Equal( 5f, tree.PoseRoot.Pose.Positions[ 0 ].X );
		context.Sampling.InterpolationPolicy = 1;
		Assert.True( tree.PoseRoot.Prepare( true, context.ConsumeHistory, context.SamplingFlags ) );
		Assert.InRange( tree.PoseRoot.Pose.Positions[ 0 ].X, 7.49f, 7.51f );
	}

	[Fact]
	public void TrajectoryEnabledOwnerConsumesIntervalInsteadOfPublishingAbsoluteRoot()
	{
		var context = new AnimationOwnerPoseContext( new( 0, true ), 2, true, .1f );
		var tree = new BoardHandlingAnimationTree( new[] { Clip( 5f, 15f, 0f ) }, Reference, Yaw, context );
		var completion = AnimationPlaybackCompletion.Empty;
		tree.Root.Advance( 1f, 0f, ref completion );
		Assert.True( tree.PoseRoot.Prepare( true, context.ConsumeHistory, context.SamplingFlags ) );
		Assert.Equal( 10f, tree.PoseRoot.Pose.Positions[ 0 ].X );
		Assert.True( tree.PoseRoot.Prepare( true, context.ConsumeHistory, context.SamplingFlags ) );
		Assert.Equal( 0f, tree.PoseRoot.Pose.Positions[ 0 ].X );
	}

	[Fact]
	public void OwnerThresholdChangesExistingDirectionalTreeEndpointSelection()
	{
		var context = new AnimationOwnerPoseContext( new( 0, false ), 2, true, .1f );
		var tree = new BoardHandlingAnimationTree( new[] { Clip( 0f, 0f, 0f ), Clip( 10f, 10f, 1f ) },
			Reference, Yaw, context );
		tree.UpdateYaw( .05f );
		Assert.True( tree.PoseRoot.Prepare( true, context.ConsumeHistory, context.SamplingFlags ) );
		Assert.Equal( 0f, tree.PoseRoot.Pose.Positions[ 0 ].X );
		context.PoseThreshold = .01f;
		tree.UpdateSamplingContext();
		Assert.True( tree.PoseRoot.Prepare( true, context.ConsumeHistory, context.SamplingFlags ) );
		Assert.InRange( tree.PoseRoot.Pose.Positions[ 0 ].X, .063f, .065f );
	}

	private static RawAnimationClip Clip( float first, float second, float yaw ) => new(
		"sample", 1f, 1f, 2, 0, 0, new[] { 0, 2, 3 }, new[] { 2, 1, 1 },
		new[] { new Vector4( first, 0, 0, 1 ), new Vector4( second, 0, 0, 1 ),
			new Vector4( 0, 0, 0, 1 ), Vector4.One },
		new AnimationClipTrajectory( 0, Vector3.Zero, Quaternion.Identity ), null,
		AnimationClipSamplingMode.Block, new AnimationClipAttributeSet( "sample", new uint[ 6 ],
			new[] { new AnimationClipAttribute( Yaw, -1f, -1f, 0,
				new AnimationAttributePayload( BitConverter.SingleToUInt32Bits( yaw ), 0, 0, 0, 0, 0 ) ) } ) );
}
