using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class AnimationPlayActorContextTests
{
	[Fact]
	public void EntryReadsSharedStateAndConsumesOnlyTheTransitionLatch()
	{
		var key = new AnimationAttributeKey( 1, 2, 3, 4, 5 );
		var first = new AnimationAttributeKey( 6, 7, 8, 9, 10 );
		var next = new AnimationAttributeKey( 11, 12, 13, 14, 15 );
		var transition = new AnimationPlayTransition( 3, .7f, true, AnimationTransitionMode.MatchTime, 0x80000000 );
		var state = new AnimationPlayActorState( first, 1, (1UL << 30) | 0x20, transition );
		var selectors = new Dictionary<AnimationAttributeKey, AnimationAttributeKey>();
		var physical = new BoardHandlingInput { HoldingBoard = true };
		var indexed = new AnimationIndexedReferenceState( _ => Reference( 1f ) );
		var context = new AnimationPlayActorContext( state, selectors, key, () => physical, indexed );
		context.RefreshCharacterSelection();
		Assert.Equal( first, selectors[ key ] );
		Assert.True( context.IsAlternateStance() );
		Assert.True( context.IsMirrored() );
		Assert.False( context.UseOffBoardVariant() );
		Assert.Equal( transition, context.ReadTransitionOverride() );
		context.ConsumeTransitionOverride();
		Assert.Equal( new AnimationPlayTransition( 0, .2f, false, AnimationTransitionMode.Both, 0 ), context.ReadTransitionOverride() );
		Assert.Equal( (1UL << 30) | 0x20, state.Flags );
		state.CharacterSelection = next;
		state.AlternateStanceValue = 2;
		state.Flags = 1UL << 31;
		physical = physical with { HoldingBoard = false };
		context.RefreshCharacterSelection();
		Assert.Single( selectors );
		Assert.Equal( next, selectors[ key ] );
		Assert.False( context.IsAlternateStance() );
		Assert.False( context.IsMirrored() );
		Assert.True( context.UseOffBoardVariant() );
		state.SetTransitionOverride( transition );
		Assert.Equal( transition, context.ReadTransitionOverride() );
	}

	[Fact]
	public void ActorModeArmsTheSameIndexedReferenceConsumedByConstruction()
	{
		var state = new AnimationPlayActorState( default, 0, 0, default );
		var indices = new List<int>();
		var indexed = new AnimationIndexedReferenceState( index =>
		{
			indices.Add( index );
			return Reference( 7f );
		} ) { BaseIndex = 4, MaximumIndex = 5, Offset = 3 };
		var context = new AnimationPlayActorContext( state,
			new Dictionary<AnimationAttributeKey, AnimationAttributeKey>(), default, () => default, indexed );
		var motion = Reference( 1f );
		context.SetActorMode( 255 );
		Assert.True( indexed.Pending );
		Assert.Null( indexed.Compose( null ) );
		Assert.True( indexed.Pending );
		var root = indexed.Compose( motion );
		Assert.False( indexed.Pending );
		Assert.Equal( new[] { 5 }, indices );
		Assert.True( root.Prepare( true, false, 1 ) );
		Assert.Equal( 8f, root.Pose.Positions[ 0 ].X );
		context.SetActorMode( 1 );
		context.SetActorMode( 0 );
		Assert.Same( motion, indexed.Compose( motion ) );
		Assert.Single( indices );
	}

	private static IAnimationPosePlaybackNode Reference( float x ) => new AnimationReferencePosePlayback(
		new[] { new AnimationBoneTransform( new Vector4( x, 0f, 0f, 1f ), Quaternion.Identity, Vector4.One ) } );
}
