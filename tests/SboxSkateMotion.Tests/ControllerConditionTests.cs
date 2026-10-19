using SkateMotion.Core.Animation;
using SkateMotion.Core.Input;
using Xunit;

namespace SboxSkateMotion.Tests;

public sealed class ControllerConditionTests
{
	[Fact]
	public void ComparisonModesPreserveStrictAndInclusiveBoundaries()
	{
		var below = new[] { false, false, true, false, true, false, true, false };
		var equal = new[] { false, true, false, false, false, true, true, false };
		var above = new[] { false, false, true, true, false, true, false, true };
		for ( var mode = 0; mode < 8; mode++ )
		{
			var comparison = new ScalarConditionComparison( (ConditionComparisonMode)mode, 1f );
			Assert.Equal( below[ mode ], comparison.Evaluate( 0f ) );
			Assert.Equal( equal[ mode ], comparison.Evaluate( 1f ) );
			Assert.Equal( above[ mode ], comparison.Evaluate( 2f ) );
		}
	}

	[Fact]
	public void UnorderedValuesPassOnlyNegatedComparisons()
	{
		for ( var mode = 0; mode < 8; mode++ )
		{
			var expected = mode is 2 or 5 or 6;
			Assert.Equal( expected, new ScalarConditionComparison( (ConditionComparisonMode)mode, 1f ).Evaluate( float.NaN ) );
			Assert.Equal( expected, new ScalarConditionComparison( (ConditionComparisonMode)mode, float.NaN ).Evaluate( 1f ) );
		}
	}

	[Fact]
	public void AbsoluteInputDoesNotAlterThresholdAndAbsoluteGreaterIsAlwaysAbsolute()
	{
		Assert.False( new ScalarConditionComparison( ConditionComparisonMode.Greater, 1f ).Evaluate( -2f ) );
		Assert.True( new ScalarConditionComparison( ConditionComparisonMode.Greater, 1f, true ).Evaluate( -2f ) );
		Assert.True( new ScalarConditionComparison( ConditionComparisonMode.AbsoluteGreater, 1f ).Evaluate( -2f ) );
		Assert.True( new ScalarConditionComparison( ConditionComparisonMode.Greater, -1f, true ).Evaluate( 0f ) );
		Assert.True( new ScalarConditionComparison( ConditionComparisonMode.Equal, 0f, true ).Evaluate( -0f ) );
		Assert.True( new ScalarConditionComparison( ConditionComparisonMode.Equal, float.PositiveInfinity, true ).Evaluate( float.NegativeInfinity ) );
	}

	[Fact]
	public void UnknownComparisonModesFail()
	{
		Assert.False( new ScalarConditionComparison( (ConditionComparisonMode)8, 0f ).Evaluate( 1f ) );
		Assert.False( new ScalarConditionComparison( (ConditionComparisonMode)uint.MaxValue, 0f, true ).Evaluate( float.NaN ) );
	}

	[Fact]
	public void PresenceAcceptsZeroAndNanWhileMissingRequestsFail()
	{
		var requests = new InputRequestBuffer( 1 );
		var condition = new InputRequestCondition( default, default );
		Assert.Equal( 0u, condition.OwnerState );
		Assert.False( condition.Evaluate( requests, 17 ) );
		Assert.Equal( 17u, condition.OwnerState );
		requests.Add( default, 0f );
		Assert.True( condition.Evaluate( requests, uint.MaxValue ) );
		Assert.Equal( uint.MaxValue, condition.OwnerState );
		requests.Set( default, float.NaN );
		Assert.True( condition.Evaluate( requests, 0 ) );
		Assert.Equal( 0u, condition.OwnerState );
	}

	[Fact]
	public void MatchingUsesEveryKeyWord()
	{
		var requests = new InputRequestBuffer( 7 );
		requests.Add( new( 8, 0, 0, 0, 0, 0 ), 1f );
		requests.Add( new( 0, 8, 0, 0, 0, 0 ), 1f );
		requests.Add( new( 0, 0, 8, 0, 0, 0 ), 1f );
		requests.Add( new( 0, 0, 0, 8, 0, 0 ), 1f );
		requests.Add( new( 0, 0, 0, 0, 8, 0 ), 1f );
		requests.Add( new( 0, 0, 0, 0, 0, 8 ), 1f );
		var condition = new InputRequestCondition( default, default );
		Assert.False( condition.Evaluate( requests, 3 ) );
		requests.Add( default, 0f );
		Assert.True( condition.Evaluate( requests, 4 ) );
		Assert.Equal( 7, requests.Count );
	}

	[Fact]
	public void RetainedConditionUsesCurrentValueAndUpdatesOwnerAfterClear()
	{
		var requests = new InputRequestBuffer( 1 );
		var condition = new InputRequestCondition( default, new( ConditionComparisonMode.Greater, .5f ) );
		requests.Add( default, 0f );
		Assert.False( condition.Evaluate( requests, 4 ) );
		requests.Set( default, 1f );
		Assert.True( condition.Evaluate( requests, 5 ) );
		Assert.Equal( 1f, requests[ 0 ].Value );
		requests.Clear();
		Assert.False( condition.Evaluate( requests, 6 ) );
		Assert.Equal( 6u, condition.OwnerState );
	}

	[Fact]
	public void MissingCollectionIsRejectedWithoutChangingOwner()
	{
		var condition = new InputRequestCondition( default, default );
		condition.Evaluate( new InputRequestBuffer( 0 ), 4 );
		Assert.Throws<ArgumentNullException>( () => condition.Evaluate( null!, 5 ) );
		Assert.Equal( 4u, condition.OwnerState );
	}

	[Fact]
	public void EvaluationDoesNotAllocateAfterConstruction()
	{
		var requests = new InputRequestBuffer( 1 );
		requests.Add( default, 1f );
		var condition = new InputRequestCondition( default, new( ConditionComparisonMode.Greater, 0f ) );
		for ( var i = 0; i < 1000; i++ )
			condition.Evaluate( requests, (uint)i );
		var before = GC.GetAllocatedBytesForCurrentThread();
		var passed = 0;
		for ( var i = 0; i < 1000; i++ )
			if ( condition.Evaluate( requests, (uint)i ) )
				passed++;
		var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
		Assert.Equal( 1000, passed );
		Assert.Equal( 0, allocated );
	}
}
