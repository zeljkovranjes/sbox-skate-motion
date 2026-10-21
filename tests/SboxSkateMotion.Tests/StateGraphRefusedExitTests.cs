using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class StateGraphRefusedExitTests
{
	[Fact]
	public void ACatchEndingOnARailItsAirExitRefusesChoosesAgainFromWhatStillApplies()
	{
		var graph = new StateGraph( Board() );
		var facts = new StateFacts();
		facts.Intents[ "trick" ] = 1f;
		graph.ClipLength = 0.25f;
		graph.Update( facts, 0.02f );
		Assert.Equal( "Trick.Kickflip.Catch", graph.State );

		// The trick is over and the board is on a rail before the catch has played: the catch still holds.
		facts.Intents.Clear();
		facts.Physics = "grind";
		graph.Update( facts, 0.1f, new AnimationPlaybackStatus( 0.15f, false ) );
		Assert.Equal( "Trick.Kickflip.Catch", graph.State );

		// Its clip has ended but the air it hands over to cannot be entered on a rail.
		graph.Update( facts, 0.1f, new AnimationPlaybackStatus( 0f, false ) );
		Assert.Equal( "Grinding.Slide", graph.State );
		Assert.Equal( "slide", graph.Clip );
	}

	[Fact]
	public void ACatchEndingInTheAirStillHandsOverToItsOwnExit()
	{
		var graph = new StateGraph( Board() );
		var facts = new StateFacts();
		facts.Intents[ "trick" ] = 1f;
		graph.ClipLength = 0.25f;
		graph.Update( facts, 0.02f );
		facts.Intents.Clear();
		facts.Physics = "air";
		graph.Update( facts, 0.1f, new AnimationPlaybackStatus( 0f, false ) );
		Assert.Equal( "InAir.Dark", graph.State );
	}

	[Fact]
	public void AFinishedStateWhoseExitIsRefusedHoldsWhileEverythingAboveItStillApplies()
	{
		var catchState = Catch();
		var graph = new StateGraph( State( "Root", null, null,
			State( "Hold", null, null, catchState ),
			State( "InAir", Physics( "air" ), null, State( "Dark", null, "dark" ) ) ) );
		var facts = new StateFacts();
		graph.ClipLength = 0.25f;
		graph.Update( facts, 0.02f );
		graph.Update( facts, 0.3f, new AnimationPlaybackStatus( 0f, false ) );
		Assert.Equal( "Hold.Catch", graph.State );
	}

	[Fact]
	public void AnUnguardedExitThatCannotBeTakenDoesNotReleaseAHeldState()
	{
		var hold = State( "Hold", null, "hold" );
		hold.Interruptible = "false";
		hold.Transitions.Add( new StateTransitionDefinition { Target = "InAir.Dark" } );
		var graph = new StateGraph( State( "Root", null, null,
			State( "Trick", Intent( "trick" ), null, hold ),
			State( "InAir", Physics( "air" ), null, State( "Dark", null, "dark" ) ),
			State( "Riding", null, "ride" ) ) );
		var facts = new StateFacts();
		facts.Intents[ "trick" ] = 1f;
		graph.Update( facts, 0.02f );
		facts.Intents.Clear();
		graph.Update( facts, 0.1f, new AnimationPlaybackStatus( 0f, false ) );
		Assert.Equal( "Trick.Hold", graph.State );
	}

	private static StateGraphDefinition Board() => State( "Root", null, null,
		State( "Trick", Intent( "trick" ), null, State( "Kickflip", null, null, Catch() ) ),
		State( "InAir", Physics( "air" ), null, State( "Dark", null, "dark" ) ),
		State( "Grinding", Physics( "grind" ), null, State( "Slide", null, "slide" ) ),
		State( "Riding", null, "ride" ) );

	private static StateGraphDefinition Catch()
	{
		var state = State( "Catch", null, "catch" );
		state.Interruptible = "false";
		state.Transitions.Add( new StateTransitionDefinition
		{
			Target = "InAir.Dark",
			Guard = new StateTestDefinition { Op = "and", Items = { new StateTestDefinition { Test = "clip-ending", Mask = 1, Within = 0.01f } } }
		} );
		return state;
	}

	private static StateGraphDefinition State( string name, StateTestDefinition guard, string clip, params StateGraphDefinition[] children ) => new()
	{
		Name = name,
		Guard = guard,
		Play = clip is null ? null : new StatePlayDefinition { Clip = clip, BlendTime = 0f },
		States = children.ToList()
	};

	private static StateTestDefinition Intent( string name ) => new() { Test = "intent", Intent = name, Mask = 1 };

	private static StateTestDefinition Physics( string state ) => new() { Test = "physics", State = state, Mask = 3 };
}
