using System;
using System.Collections.Generic;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class AnimationChannelTreeOwnerTests
{
	[Fact]
	public void TransitionCleanupWaitsPastTheEndpointAndPromotesBeforeRelease()
	{
		var factory = new Factory();
		var owner = factory.Owner;
		Assert.Equal( AnimationChannelReplaceResult.Success, owner.Replace( Request( 1 ) ) );
		var result = AnimationPlaybackCompletion.Empty;
		owner.Advance( .25f, 0f, ref result );
		Assert.False( owner.CleanupCompleted() );
		owner.Advance( .125f, 0f, ref result );
		Assert.True( owner.HasPending );
		Assert.True( owner.CleanupCompleted() );
		Assert.Same( factory.Trees[ 1 ].Root, owner.Root );
		Assert.False( owner.HasPending );
		Assert.Equal( new[] { "create:1", "commit:1", "release:0:root:1:pending:True" }, factory.Events );
		Assert.False( owner.CleanupCompleted() );
	}

	[Fact]
	public void SequenceUsesPreviousCompletionBeforeItsTreeCanBeReleased()
	{
		var factory = new Factory();
		var owner = factory.Owner;
		owner.Replace( Request( 1 ) with { Transition = false } );
		var result = AnimationPlaybackCompletion.Empty;
		owner.Advance( .5f, 0f, ref result );
		Assert.False( owner.CleanupCompleted() );
		Assert.Equal( 0f, factory.Trees[ 1 ].Root.Elapsed );
		owner.Advance( .125f, 0f, ref result );
		Assert.Equal( .125f, factory.Trees[ 1 ].Root.Elapsed );
		Assert.True( owner.CleanupCompleted() );
		Assert.Same( factory.Trees[ 1 ].Root, owner.Root );
	}

	[Fact]
	public void InterruptedReplacementPromotesTheSuccessorBeforeCreatingTheNextTree()
	{
		var factory = new Factory();
		var owner = factory.Owner;
		owner.Replace( Request( 1 ) );
		var firstComposite = owner.Root;
		var result = AnimationPlaybackCompletion.Empty;
		owner.Advance( .125f, 0f, ref result );
		factory.BeforeCreate = () =>
		{
			Assert.Same( factory.Trees[ 1 ].Root, owner.Root );
			Assert.False( owner.HasPending );
		};
		Assert.Equal( AnimationChannelReplaceResult.Success, owner.Replace( Request( 2 ) ) );
		Assert.Same( firstComposite, owner.Root );
		Assert.Equal( 0f, ((AnimationTransitionPlayback)owner.Root).BlendElapsed );
		Assert.Equal( new[] { "create:1", "commit:1", "release:0:root:1:pending:True", "create:2", "commit:2" }, factory.Events );
	}

	[Fact]
	public void FailedFactoryRetainsPromotionAndDoesNotCommit()
	{
		var factory = new Factory();
		var owner = factory.Owner;
		owner.Replace( Request( 1 ) );
		factory.Fail = true;
		Assert.Equal( AnimationChannelReplaceResult.MissingTree, owner.Replace( Request( 2 ) ) );
		Assert.Same( factory.Trees[ 1 ].Root, owner.Root );
		Assert.False( owner.HasPending );
		Assert.Equal( new[] { "create:1", "commit:1", "release:0:root:1:pending:True", "create:2" }, factory.Events );
	}

	[Fact]
	public void AdmissionFailureDoesNotReleasePendingTreesOrCallTheFactory()
	{
		var factory = new Factory();
		factory.Owner.Replace( Request( 1 ) );
		var root = factory.Owner.Root;
		factory.Blend.Stop( .5f, true );
		factory.Blend.Update( .125f, 1f, 0f );
		var before = factory.Blend.State;
		Assert.Equal( AnimationChannelReplaceResult.FadingOut, factory.Owner.Replace( Request( 2 ) ) );
		Assert.Same( root, factory.Owner.Root );
		Assert.True( factory.Owner.HasPending );
		Assert.Equal( before, factory.Blend.State );
		Assert.Equal( new[] { "create:1", "commit:1" }, factory.Events );
	}

	[Fact]
	public void AdmittedReplacementDoesNotRepeatAdmissionAfterFactoryCallbacks()
	{
		var factory = new Factory();
		factory.BeforeCreate = () =>
		{
			factory.Blend.Stop( .5f, true );
			factory.Blend.Update( .125f, 1f, 0f );
		};
		Assert.Equal( AnimationChannelReplaceResult.Success, factory.Owner.Replace( Request( 1 ) ) );
		Assert.True( factory.Owner.HasPending );
		Assert.Equal( 0f, factory.Blend.State.BlendOutElapsed );
		Assert.Equal( 0f, factory.Blend.State.RecoveryDuration );
		Assert.Equal( new[] { "create:1", "commit:1" }, factory.Events );
	}

	[Fact]
	public void FailedFirstReplacementKeepsCurrentTreeAndEnvelope()
	{
		var factory = new Factory { Fail = true };
		var before = factory.Blend.State;
		Assert.Equal( AnimationChannelReplaceResult.MissingTree, factory.Owner.Replace( Request( 1 ) ) );
		Assert.Same( factory.Trees[ 0 ].Root, factory.Owner.Root );
		Assert.False( factory.Owner.HasPending );
		Assert.Equal( before, factory.Blend.State );
		Assert.Equal( new[] { "create:1" }, factory.Events );
	}

	[Fact]
	public void CommitObservesPublishedCompositeAndEnvelopeAndFactoryReceivesTheRequest()
	{
		var factory = new Factory();
		var request = Request( 2 ) with { Rate = 2f, UsePosture = true, ExitDuration = .75f, Flags = 0x10000000 };
		factory.OnCommit = () =>
		{
			Assert.IsType<AnimationTransitionPlayback>( factory.Owner.Root );
			Assert.True( factory.Owner.HasPending );
			Assert.Equal( .75f, factory.Blend.State.BlendOutDuration );
			Assert.Equal( 0x18000000u, factory.Blend.State.Flags );
		};
		factory.Owner.Replace( request );
		Assert.Equal( request, factory.LastRequest );
	}

	private static AnimationChannelReplacement Request( int definition )
		=> new( definition, 1f, false, true, .25f, .25f, 0f, 0u, false );

	private sealed class Tree : IAnimationPlaybackTree
	{
		internal Tree( int id )
		{
			Id = id;
			Root = new AnimationClipClock( 16f, 30f, 1f, false, false );
		}

		internal int Id { get; }
		public IAnimationPlaybackNode Root { get; }
	}

	private sealed class Factory : IAnimationChannelTreeFactory
	{
		internal readonly Tree[] Trees = [ new( 0 ), new( 1 ), new( 2 ) ];
		internal readonly List<string> Events = new();
		internal readonly AnimationChannelBlend Blend = new( new AnimationChannelBlendState( 0f, 1f, 0f, 0f, 0f, 0f, 0f, 0f, 0u ) );
		internal readonly AnimationChannelTreeOwner Owner;
		internal bool Fail;
		internal Action BeforeCreate;
		internal Action OnCommit;
		internal AnimationChannelReplacement LastRequest;

		internal Factory() => Owner = new AnimationChannelTreeOwner( Trees[ 0 ], this, Blend );

		public IAnimationPlaybackTree Create( AnimationChannelReplacement request )
		{
			Events.Add( $"create:{request.Definition}" );
			LastRequest = request;
			BeforeCreate?.Invoke();
			return Fail ? null : Trees[ request.Definition ];
		}

		public void Commit( IAnimationPlaybackTree tree )
		{
			Events.Add( $"commit:{((Tree)tree).Id}" );
			OnCommit?.Invoke();
		}

		public void Release( IAnimationPlaybackTree tree )
		{
			var promoted = Array.FindIndex( Trees, item => ReferenceEquals( item.Root, Owner.Root ) );
			Events.Add( $"release:{((Tree)tree).Id}:root:{promoted}:pending:{Owner.HasPending}" );
		}
	}
}
