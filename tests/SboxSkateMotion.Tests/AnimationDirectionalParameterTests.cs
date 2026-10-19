using System;
using System.Collections.Generic;
using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class AnimationDirectionalParameterTests
{
	[Fact]
	public void ChildChangeRefreshesSamplesEvenWhenParentParameterIsUnchanged()
	{
		var node = Clock();
		var parameters = new Parameters( 0f, 10f );
		Assert.True( node.UpdateParameters( parameters, 5f, false ) );
		node.Seek( .75f );
		parameters.Samples[ 1 ] = 20f;
		parameters.Dirty[ 1 ] = true;
		Assert.True( node.UpdateParameters( parameters, 5f, false ) );
		Assert.Equal( .25f, node.Weight );
		Assert.Equal( 1.25f, node.Duration );
		Assert.Equal( .625f, node.Elapsed );
	}

	[Fact]
	public void UnchangedInputsUpdateEveryChildButDoNotQuerySamples()
	{
		var node = Clock();
		var parameters = new Parameters( 0f, 10f );
		node.UpdateParameters( parameters, 5f, false );
		parameters.Calls.Clear();
		parameters.Samples[ 1 ] = 20f;
		Assert.False( node.UpdateParameters( parameters, 5.00001f, false ) );
		Assert.Equal( new[] { "update:0", "update:1" }, parameters.Calls );
		Assert.Equal( .5f, node.Weight );
	}

	[Fact]
	public void MissingParentRetainsPriorValueAndNormalizationDuringChildRefresh()
	{
		var node = Clock();
		var parameters = new Parameters( 0f, 10f );
		node.UpdateParameters( parameters, .25f, true );
		parameters.Dirty[ 0 ] = true;
		parameters.Samples[ 0 ] = 20f;
		parameters.Samples[ 1 ] = 40f;
		Assert.True( node.UpdateParameters( parameters, -100f, false, false ) );
		Assert.Equal( .25f, node.Weight );
	}

	[Fact]
	public void RefreshVisitsChildrenInRetainedSortedOrderWithoutShortCircuiting()
	{
		var node = Clock();
		var parameters = new Parameters( 10f, 0f );
		node.UpdateParameters( parameters, 5f, false );
		parameters.Calls.Clear();
		parameters.Dirty[ 1 ] = true;
		Assert.True( node.UpdateParameters( parameters, 5f, false ) );
		Assert.Equal( new[] { "update:1", "update:0", "sample:1", "sample:0" }, parameters.Calls );
	}

	[Fact]
	public void ChildChangeCanSelectBeforeAParentParameterExists()
	{
		var node = Clock();
		var parameters = new Parameters( 0f, 10f );
		parameters.Dirty[ 0 ] = true;
		Assert.True( node.UpdateParameters( parameters, 0f, false, false ) );
		Assert.Equal( 1f, node.Weight );
		Assert.Equal( 2f, node.Duration );
	}

	[Fact]
	public void RefreshedSelectionFeedsExistingBodyAndStreamWeightRules()
	{
		var node = new AnimationDirectionalPosePlayback( new IAnimationPosePlaybackNode[] { Leaf( 0f, 2 ), Leaf( 10f, 6 ) }, new float[ 2 ] );
		var parameters = new Parameters( 0f, 10f );
		Assert.True( node.UpdateParameters( parameters, 5f, false ) );
		parameters.Samples[ 1 ] = 20f;
		parameters.Dirty[ 1 ] = true;
		Assert.True( node.UpdateParameters( parameters, 5f, false ) );
		Assert.True( node.Prepare( true, false, 0 ) );
		Assert.Equal( .25f, node.Playback.Weight );
		Assert.Equal( 1.875f, node.Pose.Positions[ 0 ].X );
		var context = new AnimationPoseStreamContext( 16 );
		node.SamplePoseStreams( context );
		Assert.Equal( new[] { 2u << 23, 6u << 23, 0x40000000u, 0x80200000u }, context.Commands( 0 ).ToArray() );
	}

	private static AnimationDirectionalPlayback Clock() => new(
		new IAnimationRatePlaybackNode[] { new AnimationClipClock( 31f, 30f, 1f, false, false ), new AnimationClipClock( 61f, 30f, 1f, false, false ) },
		new float[ 2 ] );

	private static AnimationClipPlayback Leaf( float position, ushort value )
	{
		var name = new AnimationPoseStreamKey( 1, 2, 3, 4, 5, 6 );
		var clip = new RawAnimationClip( "sample", 1f, 1f, 2, 0, 0,
			new[] { 0, 0, 0 }, new[] { 1, 0, 0 }, new[] { new Vector4( position, 0f, 0f, 1f ) }, null,
			new[] { new AnimationPoseStreamTrack( name, name, new[] { new AnimationPoseStreamKeyframe( 8191, value ) } ) } );
		return new AnimationClipPlayback( clip, 1, new AnimationPoseSamplingContext( 0, false ) );
	}

	private sealed class Parameters : IAnimationDirectionalParameters
	{
		internal Parameters( params float[] samples )
		{
			Samples = samples;
			Dirty = new bool[ samples.Length ];
		}

		internal readonly float[] Samples;
		internal readonly bool[] Dirty;
		internal readonly List<string> Calls = new();

		public bool UpdateChild( int child )
		{
			Calls.Add( "update:" + child );
			return Dirty[ child ];
		}

		public float QuerySample( int child )
		{
			Calls.Add( "sample:" + child );
			return Samples[ child ];
		}
	}
}
