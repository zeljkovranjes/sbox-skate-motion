using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public sealed class StatePathPlannerTests
{
	private static StateSelectionRecord[] Tree() =>
	[
		new( -1, -1, 1, 2, 0, 0, true, StateSelectionMode.Reselect ),
		new( 0, -1, 3, 2, 0, 0, true, StateSelectionMode.Reselect ),
		new( 0, -1, 5, 1, 0, 0, true, StateSelectionMode.Reselect ),
		new( 1, -1, -1, 0, 0, 0, true, StateSelectionMode.Retain ),
		new( 1, -1, -1, 0, 0, 0, true, StateSelectionMode.Retain ),
		new( 2, -1, -1, 0, 0, 0, true, StateSelectionMode.Retain )
	];

	[Fact]
	public void InitialSelectionEntersEveryAncestorFromRootToLeaf()
	{
		var planner = new StatePathPlanner( Tree() );
		planner.Plan( -1, 4 );
		Assert.Empty( planner.PreviousPath.ToArray() );
		Assert.Empty( planner.RetainedStates.ToArray() );
		Assert.Empty( planner.ExitedStates.ToArray() );
		Assert.Equal( new[] { 0, 1, 4 }, planner.EnteredStates.ToArray() );
	}

	[Fact]
	public void BranchChangeRetainsOnlyCommonAncestors()
	{
		var planner = new StatePathPlanner( Tree() );
		planner.Plan( 3, 5 );
		Assert.Equal( new[] { 0, 1, 3 }, planner.PreviousPath.ToArray() );
		Assert.Equal( new[] { 0 }, planner.RetainedStates.ToArray() );
		Assert.Equal( new[] { 1, 3 }, planner.ExitedStates.ToArray() );
		Assert.Equal( new[] { 2, 5 }, planner.EnteredStates.ToArray() );
	}

	[Fact]
	public void SelfTransitionCanResetItsParentAndLeaf()
	{
		var planner = new StatePathPlanner( Tree() );
		planner.Plan( 4, 4, 1 );
		Assert.Equal( new[] { 0 }, planner.RetainedStates.ToArray() );
		Assert.Equal( new[] { 1, 4 }, planner.ExitedStates.ToArray() );
		Assert.Equal( new[] { 1, 4 }, planner.EnteredStates.ToArray() );
	}

	[Fact]
	public void ResetBoundaryUsesStateIdsRatherThanDepthOrMembership()
	{
		var planner = new StatePathPlanner( Tree() );
		planner.Plan( 4, 4, 2 );
		Assert.Equal( new[] { 0, 1 }, planner.RetainedStates.ToArray() );
		Assert.Equal( new[] { 4 }, planner.ExitedStates.ToArray() );
		Assert.Equal( new[] { 4 }, planner.EnteredStates.ToArray() );
	}

	[Fact]
	public void RootBoundaryRestartsTheWholePath()
	{
		var planner = new StatePathPlanner( Tree() );
		planner.Plan( 4, 4, 0 );
		Assert.Empty( planner.RetainedStates.ToArray() );
		Assert.Equal( new[] { 0, 1, 4 }, planner.ExitedStates.ToArray() );
		Assert.Equal( new[] { 0, 1, 4 }, planner.EnteredStates.ToArray() );
	}

	[Fact]
	public void ReusedPlannerClearsOldSuffixesAndRetainsAnUnchangedPath()
	{
		var planner = new StatePathPlanner( Tree() );
		planner.Plan( 4, 5, 0 );
		planner.Plan( 2, 2 );
		Assert.Equal( new[] { 0, 2 }, planner.PreviousPath.ToArray() );
		Assert.Equal( new[] { 0, 2 }, planner.RetainedStates.ToArray() );
		Assert.Empty( planner.ExitedStates.ToArray() );
		Assert.Empty( planner.EnteredStates.ToArray() );
	}

	[Fact]
	public void CapacityComesFromTheDeepestPath()
	{
		var states = new StateSelectionRecord[ 12 ];
		for ( var index = 0; index < states.Length; index++ )
		{
			states[ index ] = new( index - 1, -1, -1, 0, 0, 0, true, StateSelectionMode.Retain );
		}
		var planner = new StatePathPlanner( states );
		planner.Plan( 11, 11, 7 );
		Assert.Equal( Enumerable.Range( 0, 7 ), planner.RetainedStates.ToArray() );
		Assert.Equal( Enumerable.Range( 7, 5 ), planner.ExitedStates.ToArray() );
		Assert.Equal( Enumerable.Range( 7, 5 ), planner.EnteredStates.ToArray() );
	}

	[Fact]
	public void InvalidInputsAreRejectedWithoutReplacingTheLastPlan()
	{
		var planner = new StatePathPlanner( Tree() );
		planner.Plan( 3, 4 );
		Assert.Throws<ArgumentOutOfRangeException>( () => planner.Plan( -2, 4 ) );
		Assert.Throws<ArgumentOutOfRangeException>( () => planner.Plan( 6, 4 ) );
		Assert.Throws<ArgumentOutOfRangeException>( () => planner.Plan( 3, -1 ) );
		Assert.Throws<ArgumentOutOfRangeException>( () => planner.Plan( 3, 6 ) );
		Assert.Throws<ArgumentOutOfRangeException>( () => planner.Plan( 3, 4, -2 ) );
		Assert.Throws<ArgumentOutOfRangeException>( () => planner.Plan( 3, 4, 6 ) );
		Assert.Equal( new[] { 3 }, planner.ExitedStates.ToArray() );
		Assert.Equal( new[] { 4 }, planner.EnteredStates.ToArray() );
	}

	[Fact]
	public void InvalidParentChainsAreRejected()
	{
		Assert.Throws<ArgumentNullException>( () => new StatePathPlanner( null ) );
		Assert.Throws<ArgumentException>( () => new StatePathPlanner( [] ) );
		var states = Tree();
		states[ 0 ] = states[ 0 ] with { Parent = 0 };
		Assert.Throws<ArgumentException>( () => new StatePathPlanner( states ) );
		states = Tree();
		states[ 2 ] = states[ 2 ] with { Parent = -1 };
		Assert.Throws<ArgumentException>( () => new StatePathPlanner( states ) );
		states = Tree();
		states[ 2 ] = states[ 2 ] with { Parent = 3 };
		Assert.Throws<ArgumentException>( () => new StatePathPlanner( states ) );
	}

	[Fact]
	public void PlanningDoesNotAllocateAfterConstruction()
	{
		var planner = new StatePathPlanner( Tree() );
		for ( var index = 0; index < 1000; index++ )
		{
			planner.Plan( 3, 5, 2 );
		}
		var before = GC.GetAllocatedBytesForCurrentThread();
		for ( var index = 0; index < 1000; index++ )
		{
			planner.Plan( 3, 5, 2 );
		}
		Assert.Equal( 0, GC.GetAllocatedBytesForCurrentThread() - before );
	}
}
