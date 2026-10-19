using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public sealed class HierarchicalStateSelectorTests
{
	private static StateSelectionRecord[] Tree() =>
	[
		new( -1, -1, 1, 2, 0, 0, true, StateSelectionMode.Reselect ),
		new( 0, -1, 3, 2, 0, 0, true, StateSelectionMode.Reselect ),
		new( 0, -1, -1, 0, 0, 0, true, StateSelectionMode.Retain ),
		new( 1, -1, -1, 0, 0, 0, true, StateSelectionMode.Retain ),
		new( 1, -1, -1, 0, 0, 0, true, StateSelectionMode.Retain )
	];

	private static HierarchicalStateSelector Create( StateSelectionRecord[] states,
		int[] ids = null, TransitionSelectionRecord[] transitions = null,
		Func<int, int, bool, bool> guard = null ) =>
		new( states, ids ?? [], transitions ?? [], guard ?? ((_, _, _) => true) );

	[Fact]
	public void ParentUrgentTransitionPrecedesLowerPriorityLeafTransition()
	{
		var states = Tree();
		states[ 0 ] = states[ 0 ] with { FirstTransition = 0, TransitionCount = 1 };
		states[ 3 ] = states[ 3 ] with { FirstTransition = 1, TransitionCount = 1 };
		var selector = Create( states, [ 0, 1 ], [ new( 3, 2, -1 ), new( 2, 4, -1 ) ] );
		Assert.Equal( (2, 0), selector.Select( 3 ) );
	}

	[Fact]
	public void LeafWinsEqualPriorityAndStoredOrderBreaksTies()
	{
		var states = Tree();
		states[ 0 ] = states[ 0 ] with { FirstTransition = 0, TransitionCount = 1 };
		states[ 3 ] = states[ 3 ] with { FirstTransition = 1, TransitionCount = 2 };
		var selector = Create( states, [ 0, 2, 1 ], [ new( 2, 2, -1 ), new( 2, 2, -1 ), new( 2, 4, -1 ) ] );
		Assert.Equal( (4, 2), selector.Select( 3 ) );
	}

	[Fact]
	public void InitialSelectionSkipsInactiveChildrenAndRejectsEmptyBranches()
	{
		var states = Tree();
		states[ 3 ] = states[ 3 ] with { Active = false };
		states[ 4 ] = states[ 4 ] with { Guard = 0 };
		var selector = Create( states, guard: (_, _, _) => false );
		Assert.Equal( (2, -1), selector.Select( -1 ) );
	}

	[Fact]
	public void ExplicitTransitionCanEnterAnInactiveTarget()
	{
		var states = Tree();
		states[ 4 ] = states[ 4 ] with { Active = false };
		states[ 3 ] = states[ 3 ] with { TransitionCount = 1 };
		Assert.Equal( (4, 0), Create( states, [ 0 ], [ new( 0, 4, -1 ) ] ).Select( 3 ) );
	}

	[Fact]
	public void SelfTransitionUsesEntryGuardEvenWhenStayingIsAllowed()
	{
		var states = Tree();
		states[ 3 ] = states[ 3 ] with { Guard = 0, TransitionCount = 1 };
		var selector = Create( states, [ 0 ], [ new( 3, 3, -1 ) ], (_, state, staying) => state == 3 && staying );
		Assert.Equal( (3, -1), selector.Select( 3 ) );
	}

	[Fact]
	public void TransitionGuardReceivesItsOwnerAndCommonAncestorUsesStayingGuard()
	{
		var states = Tree();
		states[ 1 ] = states[ 1 ] with { Guard = 0, TransitionCount = 1 };
		var calls = new List<(int Guard, int State, bool Staying)>();
		var selector = Create( states, [ 0 ], [ new( 3, 4, 1 ) ], (guard, state, staying) =>
		{
			calls.Add( (guard, state, staying) );
			return guard == 1 || staying;
		} );
		Assert.Equal( (4, 0), selector.Select( 3 ) );
		Assert.Equal( new[] { (1, 1, false), (0, 1, true) }, calls );
	}

	[Fact]
	public void NewTargetAncestorsRequireEntryGuards()
	{
		var states = Tree();
		states[ 1 ] = states[ 1 ] with { Guard = 0 };
		states[ 2 ] = states[ 2 ] with { TransitionCount = 1 };
		var selector = Create( states, [ 0 ], [ new( 3, 4, -1 ) ], (_, _, staying) => staying );
		Assert.Equal( (2, -1), selector.Select( 2 ) );
	}

	[Fact]
	public void FailedRetainedGuardFallsBackToRoot()
	{
		var states = Tree();
		states[ 1 ] = states[ 1 ] with { Guard = 0 };
		var selector = Create( states, guard: (_, _, _) => false );
		Assert.Equal( (2, -1), selector.Select( 3 ) );
	}

	[Fact]
	public void ReselectionUsesStayingGuardsOnlyOnTheCurrentPath()
	{
		var states = Tree();
		states[ 3 ] = states[ 3 ] with { Guard = 0 };
		states[ 4 ] = states[ 4 ] with { Guard = 1, Mode = StateSelectionMode.Reselect };
		var selector = Create( states, guard: (guard, _, staying) => guard == 0 ? staying : !staying );
		Assert.Equal( (2, -1), selector.Select( 4 ) );
	}

	[Fact]
	public void AncestorReselectionStaysWithinItsBranchBeforeRootFallback()
	{
		var states = Tree();
		states[ 4 ] = states[ 4 ] with { Mode = StateSelectionMode.ReselectAncestor, RetainedAncestor = 1 };
		Assert.Equal( (3, -1), Create( states ).Select( 4 ) );
		states[ 1 ] = states[ 1 ] with { Guard = 0 };
		Assert.Equal( (2, -1), Create( states, guard: (_, _, _) => false ).Select( 4 ) );
	}

	[Fact]
	public void RootWithNoEligibleLeafReturnsNoState()
	{
		var states = Tree();
		states[ 1 ] = states[ 1 ] with { Active = false };
		states[ 2 ] = states[ 2 ] with { Active = false };
		Assert.Equal( (-1, -1), Create( states ).Select( -1 ) );
	}

	[Fact]
	public void SelectionDoesNotAllocateAfterConstruction()
	{
		var selector = Create( Tree() );
		for ( var i = 0; i < 50; i++ )
		{
			selector.Select( 3 );
		}
		var before = GC.GetAllocatedBytesForCurrentThread();
		for ( var i = 0; i < 100; i++ )
		{
			selector.Select( 3 );
		}
		Assert.Equal( before, GC.GetAllocatedBytesForCurrentThread() );
	}

	[Theory]
	[InlineData( -2 )]
	[InlineData( 5 )]
	public void InvalidCurrentStateIsRejected( int current )
	{
		Assert.Throws<ArgumentOutOfRangeException>( () => Create( Tree() ).Select( current ) );
	}

	[Fact]
	public void InvalidParentAndMissingTransitionAreRejected()
	{
		var states = Tree();
		states[ 1 ] = states[ 1 ] with { Parent = 3 };
		Assert.Throws<ArgumentException>( () => Create( states ) );
		Assert.Throws<ArgumentException>( () => Create( Tree(), [ 0 ] ) );
	}
}
