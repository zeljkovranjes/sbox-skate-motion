using SkateMotion.Core.Physics;
using Xunit;

namespace SboxSkateMotion.Tests;

public class PhysicalBoardTickTests
{
	[Fact]
	public void DisabledTickDoesNotQueryOrMutateAnything()
	{
		var events = new List<string>();
		Assert.Equal( 4, PhysicalBoardTick.Update( false, Input( 4 ) with { RetrievalProgress = 1f }, Actions( events ) ) );
		Assert.Empty( events );
	}

	[Fact]
	public void CompletionClearsVelocityBeforeEntryAndUpdatesTheNewHeldState()
	{
		var events = new List<string>();
		Assert.Equal( 1, PhysicalBoardTick.Update( true, Input( 4 ) with { RetrievalProgress = 1f }, Actions( events ) ) );
		Assert.Equal( new[] { "clear", "4->1", "frame" }, events );
	}

	[Fact]
	public void RetrievalEntryChecksRangeThenUpdatesRetrievalInTheSameTick()
	{
		var events = new List<string>();
		Assert.Equal( 4, PhysicalBoardTick.Update( true, Input( 2 ) with { ActionFlags = 0x800 }, Actions( events ) ) );
		Assert.Equal( new[] { "range", "2->4", "retrieve" }, events );
	}

	[Fact]
	public void HeldFeedbackPrecedesTheFrameUpdate()
	{
		var events = new List<string>();
		Assert.Equal( 1, PhysicalBoardTick.Update( true, Input( 1 ), Actions( events ) ) );
		Assert.Equal( new[] { "feedback", "frame" }, events );
	}

	[Fact]
	public void ReleaseUpdatesTheFreeBoardWithoutAnExtraHeldFrame()
	{
		var events = new List<string>();
		Assert.Equal( 2, PhysicalBoardTick.Update( true, Input( 1 ) with { ActionFlags = 0x2000 }, Actions( events ) ) );
		Assert.Equal( new[] { "1->2", "free" }, events );
	}

	[Fact]
	public void RangeQueryResultFeedsTheDecision()
	{
		var events = new List<string>();
		Assert.Equal( 3, PhysicalBoardTick.Update( true, Input( 2 ), Actions( events, true ) ) );
		Assert.Equal( new[] { "range", "2->3", "placement" }, events );
	}

	private static PhysicalBoardInput Input( int state ) => new( state, 0, 0, 0, 0f, false, 0, false, false );

	private static PhysicalBoardTickActions Actions( List<string> events, bool beyondRange = false ) => new(
		() =>
		{
			events.Add( "range" );
			return beyondRange;
		},
		(previous, next) => events.Add( $"{previous}->{next}" ),
		() => events.Add( "clear" ), () => events.Add( "feedback" ),
		threshold => events.Add( $"directions:{threshold}" ),
		() => events.Add( "frame" ), () => events.Add( "free" ),
		() => events.Add( "placement" ), () => events.Add( "retrieve" ) );
}
