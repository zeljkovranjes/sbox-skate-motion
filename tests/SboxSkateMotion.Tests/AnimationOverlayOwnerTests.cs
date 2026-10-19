using System;
using System.Collections.Generic;
using SkateMotion.Core.Animation;
using SkateMotion.Core.Input;
using Xunit;

namespace SboxSkateMotion.Tests;

public class AnimationOverlayOwnerTests
{
	[Fact]
	public void LowerPriorityInsertionRetainsItsBaseUntilPublicationAndRemovalSpliceIt()
	{
		var events = new List<int>();
		var owner = new AnimationOverlayOwner( 3, new Factory( events ) );
		owner.PublishBase( new Node( 10, events ) );
		Assert.Equal( AnimationOverlayRequestResult.Success, owner.Create( Key( 1 ), 10, Request( 1 ) ) );
		var head = owner.Root;
		owner.Create( Key( 2 ), 0, Request( 2 ) );
		owner.Create( Key( 3 ), -5, Request( 3 ) );
		owner.PublishBase( new Node( 20, events ) );
		Advance( owner );
		Assert.Equal( new[] { 10, 1 }, events );
		Assert.Same( head, owner.Root );
		owner.Remove( Key( 2 ) );
		events.Clear();
		Advance( owner );
		Assert.Equal( new[] { 20, 3, 1 }, events );
		owner.Remove( Key( 1 ) );
		events.Clear();
		Advance( owner );
		Assert.Equal( new[] { 20, 3 }, events );
		owner.Remove( Key( 3 ) );
		Assert.Same( owner.BaseRoot, owner.Root );
		Assert.Equal( 6, owner.FreeTreeCount );
	}

	[Fact]
	public void RehashReversesRetainedCollisionChainsBeforeTheNewEntryAndRetirement()
	{
		var factory = new Factory( new() );
		var owner = new AnimationOverlayOwner( 3, factory );
		owner.PublishBase( new Node( 10, new() ) );
		var keys = new[] { new InputCommandKey( 1, 0, 0, 0, 0, 29 ),
			new InputCommandKey( 2, 0, 0, 0, 0, 28 ), new InputCommandKey( 3, 0, 0, 0, 0, 27 ) };
		Assert.Equal( 1, owner.BucketCount );
		for ( var index = 0; index < keys.Length; index++ )
		{
			owner.Create( keys[ index ], 0, Request( index + 1 ) with { ExitDuration = 0f } );
			Assert.Equal( index < 2 ? 2 : 5, owner.BucketCount );
		}
		foreach ( var key in keys )
			Assert.True( owner.Stop( key ) );
		owner.RemoveFinished();
		Assert.Equal( new[] { 3, 1, 2 }, factory.Released );
		Assert.Equal( 0, owner.Count );
		Assert.Equal( 6, owner.FreeTreeCount );
	}

	[Fact]
	public void KeysRetainTheSixthWordAndRemovedStorageCanBeReusedAfterPendingRelease()
	{
		var factory = new Factory( new() );
		var owner = new AnimationOverlayOwner( 2, factory );
		owner.PublishBase( new Node( 10, new() ) );
		var first = new InputCommandKey( 1, 2, 3, 4, 5, 6 );
		var second = first with { F = 7 };
		owner.Create( first, 0, Request( 1 ) );
		var reusedNode = owner.Root;
		owner.Create( second, 0, Request( 2 ) );
		Assert.Equal( 2, owner.Count );
		Assert.Equal( AnimationOverlayRequestResult.Capacity, owner.Create( Key( 8 ), 0, Request( 8 ) ) );
		Assert.Equal( AnimationOverlayRequestResult.Success, owner.Request( first, 99, false, Request( 3 ) ) );
		owner.Remove( first );
		Assert.Equal( new[] { 1, 3 }, factory.Released );
		Assert.Equal( 3, owner.FreeTreeCount );
		owner.Create( first, 1, Request( 4 ) );
		Assert.Same( reusedNode, owner.Root );
		owner.Reset();
		Assert.Equal( 0, owner.Count );
		Assert.Equal( 4, owner.FreeTreeCount );
		Assert.Same( owner.BaseRoot, owner.Root );
		Assert.Equal( new[] { 1, 3, 4, 2 }, factory.Released );
	}

	private static InputCommandKey Key( uint value ) => new( value, 0, 0, 0, 0, 0 );
	private static AnimationChannelReplacement Request( int definition ) =>
		new( definition, 1f, false, true, .1f, 0f, .3f, 0x10000000, true );
	private static void Advance( AnimationOverlayOwner owner )
	{
		var completion = AnimationPlaybackCompletion.Empty;
		owner.Root.Advance( .01f, 0f, ref completion );
	}

	private sealed class Factory( List<int> events ) : IAnimationOverlayTreeFactory
	{
		internal readonly List<int> Released = new();
		public IAnimationPlaybackTree Create( int storage, AnimationChannelReplacement request ) =>
			new Tree( new Node( request.Definition, events ) );
		public void Release( int storage, IAnimationPlaybackTree tree ) => Released.Add( ((Node)tree.Root).Id );
	}

	private sealed record Tree( IAnimationPlaybackNode Root ) : IAnimationPlaybackTree;
	private sealed class Node( int id, List<int> events ) : IAnimationPosePlaybackNode
	{
		internal int Id => id;
		public SkeletonPose Pose { get; } = new( 1 );
		public float Duration => 100f;
		public float Elapsed => 0f;
		public bool Prepare( bool enabled, bool consumeHistory, uint flags ) => true;
		public void SamplePoseStreams( AnimationPoseStreamContext context ) { }
		public void Advance( float delta, float phase, ref AnimationPlaybackCompletion completion ) => events.Add( id );
		public void Seek( float time ) { }
	}
}
