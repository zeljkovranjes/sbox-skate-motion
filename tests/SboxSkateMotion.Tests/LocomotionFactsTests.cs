using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class LocomotionFactsTests
{
	[Theory]
	[InlineData( 0f, "stand" )]
	[InlineData( 1f, "walk" )]
	[InlineData( 3f, "run" )]
	[InlineData( 5f, "sprint" )]
	public void WalkingPublishesItsOwnSpeedAndOnlyTheMatchingGait( float speed, string expected )
	{
		var facts = new StateFacts();
		new RiderStateFacts().Fill( facts, new RiderAnimationInput
		{
			Activity = RiderActivity.Walking, Speed = 12f, MoveSpeed = speed, MoveAmount = 0.3f, Steer = -1f
		}, false, 1f / 60f );
		Assert.Equal( speed, facts.Speed );
		Assert.Equal( 0.3f, facts.Intents[ "ob-steer-magnitude" ] );
		Assert.True( facts.NoBoard );
		foreach ( var gait in new[] { "stand", "walk", "run", "sprint" } )
			Assert.Equal( gait == expected, facts.MatchesArguments( Gait( gait ) ) );
	}

	[Fact]
	public void CarryAndPickupUseActualBoardPossession()
	{
		var facts = new StateFacts();
		var source = new RiderStateFacts();
		source.Fill( facts, new RiderAnimationInput { Activity = RiderActivity.Carrying }, false, 0.01f );
		Assert.False( facts.NoBoard );
		Assert.Equal( 1f, facts.Facts[ "is-holding-skateboard" ] );
		Assert.Equal( 1f, facts.Intents[ "ob-hold-board" ] );
		source.Fill( facts, new RiderAnimationInput { Activity = RiderActivity.PickingUp }, false, 0.01f );
		Assert.True( facts.NoBoard );
		Assert.Equal( 1f, facts.Intents[ "ob-retrieve-board" ] );
		source.Fill( facts, new RiderAnimationInput { Activity = RiderActivity.PickingUp, HoldingBoard = true }, false, 0.01f );
		Assert.False( facts.NoBoard );
		source.Fill( facts, new RiderAnimationInput { Activity = RiderActivity.Walking }, false, 0.01f );
		Assert.Equal( 0f, facts.Intents[ "ob-steer-magnitude" ] );
		Assert.False( facts.Intents.ContainsKey( "ob-retrieve-board" ) );
		Assert.Equal( 0f, facts.Facts[ "is-holding-skateboard" ] );
	}

	[Fact]
	public void GraphRejectsWrongOrMissingCategoryAndReevaluatesAfterClear()
	{
		var definition = new StateGraphDefinition
		{
			Name = "Root",
			States =
			{
				new() { Name = "Walk", Guard = Gait( "walk" ), Play = new() { Clip = "walk" } },
				new() { Name = "Run", Guard = Gait( "run" ), Play = new() { Clip = "run" } },
				new() { Name = "Idle", Play = new() { Clip = "idle" } }
			}
		};
		var graph = new StateGraph( definition );
		var facts = new StateFacts();
		facts.Facts[ "loco-state" ] = 1f;
		facts.SetArgument( "loco-state", "locostate", "RUN" );
		graph.Update( facts, .1f );
		Assert.Equal( "run", graph.Clip );
		facts.Clear();
		facts.Facts[ "loco-state" ] = 1f;
		graph.Update( facts, .1f );
		Assert.Equal( "idle", graph.Clip );
	}

	private static StateTestDefinition Gait( string value ) => new()
	{
		Test = "fact", Fact = "loco-state", Mask = 3, Arguments = { [ "locostate" ] = value }
	};
}
