using System;
using System.Collections.Generic;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public sealed class AnimationBaseOwnerInputPhaseTests
{
	[Fact]
	public void PhysicalInputReadsPostAdvanceAttributesAndRetainedCompletion()
	{
		var factory = new Factory();
		var owner = new AnimationBasePoseTreeOwner( factory );
		Assert.True( owner.Replace( new( 0, AnimationBaseTransition.Play, 1f ) ) );
		var initialCompletion = AnimationPlaybackCompletion.Empty;
		factory.Leaf.Advance( .25f, 0f, ref initialCompletion );
		var cache = new AnimationEvaluatedAttributeCache( 1 );
		var events = new List<string>();
		var phase = new AnimationBaseOwnerInputPhase( owner,
			new AnimationEvaluatedAttributePublisher( cache, 0, 1 ),
			() => events.Add( $"physical:{cache.Values[ 0 ].Flags}:{owner.Completion.Flags}" ),
			delta => events.Add( $"actor:{delta}" ) );
		var deltas = new[] { .3f, 1f, 1f };
		var positions = new uint[] { 1057803469, 1069966950, 1076048691 };
		var remaining = new uint[] { 1069128090, 1055286888, 3205287116 };
		for ( var i = 0; i < deltas.Length; i++ )
		{
			phase.Update( deltas[ i ], .75f );
			Assert.Equal( positions[ i ], BitConverter.SingleToUInt32Bits( factory.Leaf.Clock.Position ) );
			Assert.Equal( remaining[ i ], BitConverter.SingleToUInt32Bits( owner.Completion.Remaining ) );
			Assert.Equal( i == 2, owner.Completion.Finished );
			Assert.Equal( i == 0, owner.ReplacementPending );
			Assert.True( owner.ReplacementAdvanced );
			Assert.Equal( 7f, BitConverter.UInt32BitsToSingle( cache.Values[ 0 ].Payload.A ) );
		}
		Assert.Equal( new[] { "physical:5:1", "actor:0.3", "physical:9:1", "actor:1", "physical:9:1", "actor:1" }, events );
	}

	[Fact]
	public void FailedReplacementStillAgesChangeStateAndEmptyRootClearsCache()
	{
		var owner = new AnimationBasePoseTreeOwner( new Factory() );
		Assert.False( owner.Replace( new( 99, AnimationBaseTransition.Play, 1f ) ) );
		Assert.True( owner.ReplacementPending );
		Assert.False( owner.ReplacementAdvanced );
		var cache = new AnimationEvaluatedAttributeCache( 1 );
		cache.Publish( new AnimationAttributeQueryResult[ 1 ] );
		var events = new List<string>();
		var phase = new AnimationBaseOwnerInputPhase( owner,
			new AnimationEvaluatedAttributePublisher( cache, 0, 1 ),
			() => { Assert.True( cache.Values.IsEmpty ); events.Add( "physical" ); },
			_ => events.Add( "actor" ) );
		phase.Update( .3f, .75f );
		Assert.Equal( AnimationPlaybackCompletion.Empty, owner.Completion );
		Assert.True( owner.ReplacementPending );
		Assert.True( owner.ReplacementAdvanced );
		phase.Update( .3f, .75f );
		Assert.False( owner.ReplacementPending );
		Assert.True( owner.ReplacementAdvanced );
		Assert.Equal( new[] { "physical", "actor", "physical", "actor" }, events );
	}

	private sealed class Factory : IAnimationBasePoseTreeFactory
	{
		internal AnimationClipPlayback Leaf;
		public IAnimationPlaybackTree Create( int slot, int definition )
		{
			if ( definition == 99 )
				return null;
			var attributes = new AnimationClipAttributeSet( "motion", new uint[ 6 ],
				[new( new( 1, 0, 0, 0, 0 ), .2f, .6f, 0,
					new( BitConverter.SingleToUInt32Bits( 7f ), 0, 0, 0, 0, 0 ) )] );
			var clip = new RawAnimationClip( "motion", 30f, 1f, 61, 0, 0,
				[], [], [], null, null, AnimationClipSamplingMode.Block, attributes );
			Leaf = new( clip, 0, new AnimationPoseSamplingContext( 1, false ) );
			return new Tree( Leaf );
		}
		public void Publish( IAnimationPosePlaybackNode root ) { }
	}

	private sealed class Tree( IAnimationPlaybackNode root ) : IAnimationPlaybackTree
	{
		public IAnimationPlaybackNode Root => root;
	}
}
