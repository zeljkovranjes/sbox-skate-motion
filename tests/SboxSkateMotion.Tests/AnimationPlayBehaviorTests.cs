using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public sealed class AnimationPlayBehaviorTests
{
	[Fact]
	public void EntryActionOverrideAndVariantsRunInOrderBeforeFailureFallback()
	{
		var context = new Context { FailFirst = true, Alternate = true, Mirrored = true, OffBoard = true };
		context.ActionOverride = new( 3, .375f, true, AnimationTransitionMode.MatchPhase, 0x80000000u );
		var definition = new AnimationPlayBehaviorDefinition(
			new( 2, .2f, false, AnimationTransitionMode.Both, 0 ), 10, 11, 12, 13, .75f, 0x01e00000u, 40 );
		AnimationPlayBehavior.Enter( definition, 7, 99, context );
		Assert.Equal( new[] { "mark:7", "action:40:7", "selector", "read", "consume", "alternate", "mirror", "offboard", "mode:1", "replace:13", "replace:99" }, context.Events );
		Assert.Equal( new AnimationBasePoseRequest( 13, AnimationBaseTransition.ChannelBlend, .75f, .375f,
			AnimationTransitionMode.MatchPhase, true, 0f, true ), context.Requests[ 0 ] );
		Assert.Equal( new AnimationBasePoseRequest( 99, AnimationBaseTransition.Play, 1f ), context.Requests[ 1 ] );
		Assert.Equal( 0, context.Pending.Kind );
	}

	[Fact]
	public void UnflaggedSequenceSkipsVariantQueriesAndConsumesEmptyOverride()
	{
		var context = new Context();
		var definition = new AnimationPlayBehaviorDefinition(
			new( 4, 8f, true, AnimationTransitionMode.MatchTime, uint.MaxValue ), 10, 11, 12, 13, 1.5f, 0x02000000u, 0 );
		AnimationPlayBehavior.Enter( definition, 5, 99, context );
		Assert.Equal( new[] { "mark:5", "action:0:5", "selector", "read", "consume", "mode:2", "replace:10" }, context.Events );
		Assert.Equal( new AnimationBasePoseRequest( 10, AnimationBaseTransition.Sequence, 1.5f, Under: true ), Assert.Single( context.Requests ) );
	}

	private sealed class Context : IAnimationPlayBehaviorContext
	{
		internal readonly List<string> Events = new();
		internal readonly List<AnimationBasePoseRequest> Requests = new();
		internal AnimationPlayTransition Pending;
		internal AnimationPlayTransition ActionOverride;
		internal bool Alternate;
		internal bool Mirrored;
		internal bool OffBoard;
		internal bool FailFirst;

		public void MarkEntered( int instance ) => Events.Add( $"mark:{instance}" );
		public void ExecuteEntryAction( int action, int instance )
		{
			Events.Add( $"action:{action}:{instance}" );
			Pending = ActionOverride;
		}
		public void RefreshCharacterSelection() => Events.Add( "selector" );
		public AnimationPlayTransition ReadTransitionOverride()
		{
			Events.Add( "read" );
			return Pending;
		}
		public void ConsumeTransitionOverride()
		{
			Events.Add( "consume" );
			Pending = default;
		}
		public bool IsAlternateStance()
		{
			Events.Add( "alternate" );
			return Alternate;
		}
		public bool IsMirrored()
		{
			Events.Add( "mirror" );
			return Mirrored;
		}
		public bool UseOffBoardVariant()
		{
			Events.Add( "offboard" );
			return OffBoard;
		}
		public void SetActorMode( byte mode ) => Events.Add( $"mode:{mode}" );
		public bool TryReplace( AnimationBasePoseRequest request )
		{
			Events.Add( $"replace:{request.Definition}" );
			Requests.Add( request );
			return !FailFirst || Requests.Count > 1;
		}
	}
}
