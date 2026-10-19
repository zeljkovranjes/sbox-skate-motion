using System;
using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class AnimationReferenceTreeTests
{
	private static readonly AnimationPoseStreamKey Name = new( 1, 2, 3, 4, 5, 6 );

	[Fact]
	public void ConstantClockPreservesCompletionAndExistingCommands()
	{
		var reference = Reference();
		var completion = new AnimationPlaybackCompletion( 0x81, true, .125f, .75f );
		var expected = completion;
		var context = new AnimationPoseStreamContext( 16 );
		Leaf().SamplePoseStreams( context );
		var commands = context.Commands( 0 ).ToArray();

		reference.Advance( 10f, .5f, ref completion );
		reference.Seek( 5f );
		reference.SetRate( 2f );
		reference.SamplePoseStreams( context );

		Assert.Equal( expected, completion );
		Assert.Equal( 0f, reference.Duration );
		Assert.Equal( 0f, reference.Elapsed );
		Assert.Equal( commands, context.Commands( 0 ).ToArray() );
		Assert.Equal( 1, context.NameCount );
	}

	[Fact]
	public void ReferenceOwnsItsInputAndRestoresOnlyWhenEnabled()
	{
		var transforms = new[] { Transform() };
		var reference = new AnimationReferencePosePlayback( transforms );
		transforms[ 0 ] = default;
		Assert.True( reference.Prepare( true, true, uint.MaxValue ) );
		Assert.Equal( new Vector3( 10f, 0f, 0f ), reference.Pose.Positions[ 0 ] );
		reference.Pose.Positions[ 0 ] = Vector3.One;
		Assert.False( reference.Prepare( false, false, 0 ) );
		Assert.Equal( Vector3.One, reference.Pose.Positions[ 0 ] );
		Assert.True( reference.Prepare( true, false, 0 ) );
		Assert.Equal( new Vector3( 10f, 0f, 0f ), reference.Pose.Positions[ 0 ] );
	}

	[Theory]
	[InlineData( true, 8f )]
	[InlineData( false, 12f )]
	public void CompositionRetainsOperandOrderAndActiveMask( bool firstActive, float position )
	{
		var leaf = Leaf();
		var reference = Reference();
		var node = firstActive
			? new AnimationCompositionPosePlayback( leaf, reference, true )
			: new AnimationCompositionPosePlayback( reference, leaf, false );
		for ( var repeat = 0; repeat < 3; repeat++ )
		{
			Assert.True( node.Prepare( true, false, 0 ) );
			Assert.Equal( new Vector3( position, 0f, 0f ), node.Pose.Positions[ 0 ] );
			Assert.Equal( .25f, node.Pose.BoneWeights[ 0 ] );
			Assert.Equal( new Quaternion( 0f, 0f, 1f, 0f ), node.Pose.Rotations[ 0 ] );
			Assert.Equal( 2f, leaf.Pose.Positions[ 0 ].X );
		}
		Assert.False( node.Prepare( false, false, 0 ) );
	}

	[Theory]
	[InlineData( true )]
	[InlineData( false )]
	public void ActiveClockAndStreamsRemainAligned( bool firstActive )
	{
		var leaf = Leaf();
		var reference = Reference();
		var node = firstActive
			? new AnimationCompositionPosePlayback( leaf, reference, true )
			: new AnimationCompositionPosePlayback( reference, leaf, false );
		node.SetRate( 2f );
		node.Seek( .125f );
		var completion = AnimationPlaybackCompletion.Empty;
		node.Advance( .0625f, 0f, ref completion );
		Assert.Equal( leaf.Duration, node.Duration );
		Assert.Equal( .5f, node.Duration );
		Assert.Equal( leaf.Elapsed, node.Elapsed );
		Assert.Equal( 0f, reference.Elapsed );
		var previous = leaf.Clock.PreviousPosition;
		var context = new AnimationPoseStreamContext( 16 );
		node.SamplePoseStreams( context );
		node.SamplePoseStreams( context );
		Assert.Equal( new[] { 6u << 23, 6u << 23 }, context.Commands( 0 ).ToArray() );
		Assert.Equal( previous, leaf.Clock.PreviousPosition );
		node.Prepare( true, true, 0 );
		Assert.Equal( 2, context.Commands( 0 ).Length );
	}

	[Fact]
	public void NestedReferenceWrappersSurviveSequenceHandoff()
	{
		var inner = new AnimationCompositionPosePlayback( Leaf(), Reference(), true );
		var next = new AnimationCompositionPosePlayback( Reference(), inner, false );
		var sequence = new AnimationSequencePosePlayback( Leaf(), next );
		var completion = AnimationPlaybackCompletion.Empty;
		sequence.Advance( 1.125f, 0f, ref completion );
		Assert.True( sequence.Playback.ActiveNext );
		Assert.True( sequence.Prepare( true, false, 0 ) );
		Assert.Equal( -2f, sequence.Pose.Positions[ 0 ].X );
		Assert.Equal( .25f, sequence.Pose.BoneWeights[ 0 ] );
		var context = new AnimationPoseStreamContext( 16 );
		sequence.SamplePoseStreams( context );
		Assert.Equal( new[] { 6u << 23 }, context.Commands( 0 ).ToArray() );
	}

	[Fact]
	public void RejectsChildrenWithoutRateControlOrMatchingSkeletons()
	{
		var sequence = new AnimationSequencePosePlayback( Leaf(), Leaf() );
		Assert.Throws<ArgumentException>( () => new AnimationCompositionPosePlayback( sequence, Reference(), true ) );
		var empty = new AnimationReferencePosePlayback( Array.Empty<AnimationBoneTransform>() );
		Assert.Throws<ArgumentException>( () => new AnimationCompositionPosePlayback( Leaf(), empty, true ) );
	}

	private static AnimationBoneTransform Transform() => new(
		new Vector4( 10f, 0f, 0f, .75f ), new Quaternion( 0f, 0f, 1f, 0f ), Vector4.One );

	private static AnimationReferencePosePlayback Reference() => new( new[] { Transform() } );

	private static AnimationClipPlayback Leaf()
	{
		var clip = new RawAnimationClip( "sample", 1f, 1f, 2, 0, 0,
			new[] { 0, 0, 0 }, new[] { 1, 0, 0 }, new[] { new Vector4( 2f, 0f, 0f, .25f ) }, null,
			new[] { new AnimationPoseStreamTrack( Name, Name, new[] { new AnimationPoseStreamKeyframe( 8191, 6 ) } ) } );
		return new AnimationClipPlayback( clip, 1, new AnimationPoseSamplingContext( 0, false ) );
	}
}
