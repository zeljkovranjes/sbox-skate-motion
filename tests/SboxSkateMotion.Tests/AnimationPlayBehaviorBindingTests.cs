using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public sealed class AnimationPlayBehaviorBindingTests
{
	[Fact]
	public void CompiledGraphRunsOrderedPlaysAndSuppressesOnlyTheFirstActionUpdate()
	{
		var factory = new Factory();
		var owner = new AnimationBasePoseTreeOwner( factory );
		var actions = new ActionContext();
		var binding = new AnimationPlayBehaviorBinding( [Play( 1, 0 ), Play( 4, 1 )], [true],
			new Dictionary<int, AnimationEntryAction[]> { [0] = [Action()], [1] = [Action()] },
			owner, 99, actions, new ActorContext(), External( [] ), ( _, _, _ ) => throw new Exception() );
		var definition = new CompiledStateGraphDefinition(
			[new( -1, -1, -1, 0, 0, 0, true, StateSelectionMode.Retain )], [], [], [],
			[new( 0, 2 )], [new( 0, true ), new( 0, true )], [[0, 0]] );
		var runtime = new CompiledStateGraphRuntime( definition, [binding.Bind( 0 ), binding.Bind( 1 )],
			binding.Callbacks.Retain, binding.Callbacks.Release, [],
			[( id, instance ) => binding.Update( 0, id, instance )], () => { }, true );
		runtime.Update( .1f );
		Assert.Equal( new[] { 0, 1 }, factory.Created );
		Assert.Equal( 1, owner.RetainedCount );
		Assert.Equal( 2, actions.Published );
		runtime.Update( .1f );
		Assert.Equal( 4, actions.Published );
		Assert.Equal( new[] { 0, 1 }, factory.Created );
		Assert.True( owner.Prepare( true, true, 1 ) );
		Assert.Equal( 0f, owner.Pose.Positions[ 0 ].X );
	}

	[Fact]
	public void ExternalReferencesKeepTheirHandlesAndMissingActionListsAreRejected()
	{
		var events = new List<string>();
		var owner = new AnimationBasePoseTreeOwner( new Factory() );
		var actions = new Dictionary<int, AnimationEntryAction[]> { [0] = [] };
		var binding = new AnimationPlayBehaviorBinding( [null, Play( 1, 0 )], [false], actions,
			owner, 99, new ActionContext(), new ActorContext(), External( events ),
			( op, definition, instance ) => events.Add( $"update:{op}:{definition}:{instance}" ) );
		var retained = binding.Callbacks.Create( 0 );
		binding.Callbacks.Retain( retained );
		binding.Callbacks.Enter( 0, retained );
		binding.Update( 0, 0, retained );
		binding.Callbacks.Exit( 0, retained );
		binding.Callbacks.Release( retained );
		binding.Callbacks.Release( retained );
		Assert.Equal( new[] { "create:0", "retain:57", "enter:0:57", "update:0:0:57",
			"exit:0:57", "release:57", "release:57" }, events );
		Assert.Throws<InvalidOperationException>( () => binding.Callbacks.Release( retained ) );
		Assert.Throws<ArgumentException>( () => new AnimationPlayBehaviorBinding( [Play( 1, 1 )], [true],
			actions, owner, 99, new ActionContext(), new ActorContext(), External( events ), ( _, _, _ ) => { } ) );
	}

	private static AnimationPlayBehaviorDefinition Play( int kind, int id ) =>
		new( new( kind, .2f, false, AnimationTransitionMode.Both, 0 ), id, id, id, id, 1f, 0, id );

	private static AnimationEntryAction Action() => new( 2, default, default, default, 3f, 0x80000000u );

	private static StateBehaviorCallbacks External( List<string> events ) => new(
		id => { events.Add( $"create:{id}" ); return 57; },
		id => events.Add( $"retain:{id}" ), id => events.Add( $"release:{id}" ),
		( definition, id ) => events.Add( $"enter:{definition}:{id}" ),
		( definition, id ) => events.Add( $"exit:{definition}:{id}" ),
		definition => events.Add( $"transition:{definition}" ) );

	private sealed class ActorContext : IAnimationPlayActorContext
	{
		public void RefreshCharacterSelection() { }
		public AnimationPlayTransition ReadTransitionOverride() => default;
		public void ConsumeTransitionOverride() { }
		public bool IsAlternateStance() => false;
		public bool IsMirrored() => false;
		public bool UseOffBoardVariant() => false;
		public void SetActorMode( byte mode ) { }
	}

	private sealed class ActionContext : IAnimationEntryActionContext
	{
		internal int Published;
		private readonly AnimationActionValueTable _empty = new( [-1, -1], [] );
		public AnimationAttributeKey EmptyTargetKey => default;
		public bool TryGetAttribute( AnimationAttributeKey name, ref AnimationAttributeQueryResult result ) => false;
		public AnimationActionValueTable GetValueTable( uint kind ) => _empty;
		public void SetParameter( AnimationAttributeKey name, float value, bool normalized, int owner ) => Published++;
	}

	private sealed class Factory : IAnimationBasePoseTreeFactory
	{
		internal readonly List<int> Created = new();
		public IAnimationPlaybackTree Create( int slot, int definition )
		{
			Created.Add( definition );
			return new Tree( definition );
		}
		public void Publish( IAnimationPosePlaybackNode root ) { }
	}

	private sealed class Tree : IAnimationPlaybackTree
	{
		internal Tree( int definition )
		{
			var clip = new RawAnimationClip( "motion", 1f, 1f, 2, 0, 0,
				[0, 0, 0], [2, 0, 0], [new Vector4( definition * 10f, 0, 0, 1 ), new Vector4( definition * 10f + 1f, 0, 0, 1 )] );
			Root = new AnimationClipPlayback( clip, 0, new AnimationPoseSamplingContext( 1, false ) );
		}
		public IAnimationPlaybackNode Root { get; }
	}
}
