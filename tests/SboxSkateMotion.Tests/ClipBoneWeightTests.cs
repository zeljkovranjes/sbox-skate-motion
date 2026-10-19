using System;
using System.Collections.Generic;
using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class ClipBoneWeightTests
{
	[Fact]
	public void AuthoredWeightsAreCopiedAndSurviveSamplingAtDifferentTimes()
	{
		var clip = Clip( "layer" );
		var weights = new[] { .2f, .9f };
		clip.SetBoneWeights( weights );
		weights[ 0 ] = 1f;
		var pose = new SkeletonPose( 2 );
		foreach ( var time in new[] { 0f, .125f, 2f } )
		{
			pose.Sample( clip, Skeleton(), time, false );
			Assert.Equal( new[] { .2f, .9f }, pose.BoneWeights );
		}
	}

	[Fact]
	public void OrdinaryClipAndRestResetPreviouslySampledWeights()
	{
		var layer = Clip( "layer" );
		layer.SetBoneWeights( new[] { 0f, .5f } );
		var pose = new SkeletonPose( 2 );
		pose.Sample( layer, Skeleton(), 0f, false );
		pose.Sample( Clip( "body" ), Skeleton(), 0f, false );
		Assert.Equal( new[] { 1f, 1f }, pose.BoneWeights );
		pose.Sample( layer, Skeleton(), 0f, false );
		pose.SetRest( Skeleton() );
		Assert.Equal( new[] { 1f, 1f }, pose.BoneWeights );
	}

	[Fact]
	public void PoseCopiesOwnTheirWeights()
	{
		var first = new SkeletonPose( 2 );
		first.BoneWeights[ 0 ] = .25f;
		var second = new SkeletonPose( 2 );
		second.CopyFrom( first );
		first.BoneWeights[ 0 ] = 0f;
		Assert.Equal( .25f, second.BoneWeights[ 0 ] );
	}

	[Fact]
	public void BlendCarriesWeightsWithoutMaskingItsUniformTransformBlend()
	{
		var first = Clip( "first" );
		var second = Clip( "second" );
		first.SetBoneWeights( new[] { 0f, 1f } );
		second.SetBoneWeights( new[] { 1f, 0f } );
		var skeleton = Skeleton();
		var clips = new ClipSet( skeleton, [ first, second ] );
		clips.AddBlends( new Dictionary<string, BlendDefinition>
		{
			[ "blend" ] = new() { Parameter = "mix", Children =
				[ new() { Name = "first", At = 0f }, new() { Name = "second", At = 1f } ] }
		} );
		var parameters = new AnimationParameters();
		parameters.Set( "mix", .25f );
		var pose = new SkeletonPose( 2 );
		pose.Sample( clips.Find( "blend" ), skeleton, 0f, false, parameters );
		Assert.Equal( new[] { .25f, .75f }, pose.BoneWeights );
		Assert.Equal( skeleton.RestPositions, pose.Positions );
	}

	[Fact]
	public void InvalidWeightsDoNotReplaceThePreviousBinding()
	{
		var clip = Clip( "layer" );
		clip.SetBoneWeights( new[] { .2f, .8f } );
		Assert.Throws<ArgumentException>( () => clip.SetBoneWeights( new[] { 1f } ) );
		Assert.Throws<ArgumentException>( () => clip.SetBoneWeights( new[] { 1f, float.NaN } ) );
		Assert.Throws<ArgumentException>( () => clip.SetBoneWeights( new[] { 1f, float.PositiveInfinity } ) );
		var output = new float[ 2 ];
		clip.SampleBoneWeights( output );
		Assert.Equal( new[] { .2f, .8f }, output );
	}

	[Fact]
	public void WrongOutputSizeFailsBeforeWriting()
	{
		var output = new[] { 7f };
		Assert.Throws<ArgumentException>( () => Clip( "body" ).SampleBoneWeights( output ) );
		Assert.Equal( 7f, output[ 0 ] );
	}

	private static ClipSkeleton Skeleton() => new(
		[ "root", "child" ], [ -1, 0 ], [ Vector3.Zero, Vector3.UnitX ], [ Quaternion.Identity, Quaternion.Identity ] );

	private static AnimationClip Clip( string name ) => new(
		name, 30f, 2, [ 0, 0 ], [ 0, 0 ], [], [ 0, 0 ], [ 0, 0 ], [] );
}
