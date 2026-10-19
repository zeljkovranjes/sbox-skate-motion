using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class StateGraphTests
{
	[Fact]
	public void EntersTheFirstChildThatLeadsToALeaf()
	{
		// Riding has no tests of its own but every child needs an intent, so it is skipped while none is held.
		var graph = new StateGraph( Root(
			State( "Riding", null, null, State( "Push", Intent( "push" ), "push" ) ),
			State( "Idle", null, "idle" ) ) );
		var facts = new StateFacts();

		graph.Update( facts, 0.1f );
		Assert.Equal( "Idle", graph.State );
		Assert.Equal( "idle", graph.Clip );
	}

	[Fact]
	public void GuardedEarlierSiblingsTakeOverButUnguardedOnesDoNot()
	{
		var graph = new StateGraph( Root(
			State( "Brake", Intent( "brake" ), "brake" ),
			State( "Turning", null, null,
				State( "Idle", null, "idle" ),
				State( "Manual", Intent( "manual" ), "manual" ) ) ) );
		var facts = new StateFacts();
		graph.Update( facts, 0.1f );
		Assert.Equal( "Turning.Idle", graph.State );

		// Manual is only reached by entering Turning afresh; Idle never takes back over on its own.
		facts.Intents[ "manual" ] = 1f;
		graph.Update( facts, 0.1f );
		Assert.Equal( "Turning.Idle", graph.State );

		facts.Intents[ "brake" ] = 1f;
		graph.Update( facts, 0.1f );
		Assert.Equal( "Brake", graph.State );
	}

	[Fact]
	public void TransitionsNeedTheirTargetToBeEnterable()
	{
		var idle = State( "Idle", null, "idle" );
		idle.Transitions.Add( new StateTransitionDefinition { Target = "Push" } );
		var graph = new StateGraph( Root( State( "Turning", null, null, idle, State( "Push", Intent( "push" ), "push" ) ) ) );
		var facts = new StateFacts();
		graph.Update( facts, 0.1f );
		Assert.Equal( "Turning.Idle", graph.State );

		facts.Intents[ "push" ] = 1f;
		graph.Update( facts, 0.1f );
		Assert.Equal( "Turning.Push", graph.State );
	}

	[Fact]
	public void StayingTestsAndEmptiedParentsHandControlBack()
	{
		var manual = State( "Manual", null, null, State( "Tail", Intent( "manual", 3 ), "tail" ) );
		var idle = State( "Idle", null, "idle" );
		idle.Transitions.Add( new StateTransitionDefinition { Target = "Manual" } );
		var graph = new StateGraph( Root( State( "Turning", null, null, idle, manual ) ) );
		var facts = new StateFacts();
		facts.Intents[ "manual" ] = 1f;
		graph.Update( facts, 0.1f );
		Assert.Equal( "Turning.Manual.Tail", graph.State );

		facts.Intents.Clear();
		graph.Update( facts, 0.1f );
		Assert.Equal( "Turning.Idle", graph.State );
		Assert.Equal( "Turning.Manual.Tail", graph.PreviousState );
	}

	[Fact]
	public void ClipEndingTransitionsWaitForTheClipAndPathsResolveFromAncestors()
	{
		var into = State( "Into", null, "into" );
		into.Interruptible = "false";
		into.Transitions.Add( new StateTransitionDefinition
		{
			Target = "Cycle",
			Guard = new StateTestDefinition { Test = "clip-ending", Within = 0.05f, Wait = false }
		} );
		var cycle = State( "Cycle", null, "cycle" );
		cycle.Active = false;
		var graph = new StateGraph( Root( State( "Push", null, null, into, cycle ) ) );
		var facts = new StateFacts();
		graph.ClipLength = 0.5f;
		graph.Update( facts, 0.2f );
		Assert.Equal( "Push.Into", graph.State );
		graph.ClipLength = 0.5f;
		graph.Update( facts, 0.28f );
		Assert.Equal( "Push.Cycle", graph.State );
		Assert.Equal( "cycle", graph.Clip );
	}

	[Fact]
	public void MaskedOutTestsDoNotFlipNegations()
	{
		// "not speed below 0.15" checked on entry only: once moving, slowing down does not throw the state out.
		var moving = State( "Moving", new StateTestDefinition
		{
			Op = "not",
			Items = { new StateTestDefinition { Test = "speed", Mask = 1, Absolute = true, Compare = { [ "<" ] = 0.15f } } }
		}, "moving" );
		var graph = new StateGraph( Root( State( "Brake", null, null, moving, State( "Stopped", null, "stopped" ) ) ) );
		var facts = new StateFacts { Speed = 3f };
		graph.Update( facts, 0.1f );
		Assert.Equal( "Brake.Moving", graph.State );
		facts.Speed = 0.05f;
		graph.Update( facts, 0.1f );
		Assert.Equal( "Brake.Moving", graph.State );
	}

	[Fact]
	public void MirroredRidersPlayTheMirrorClip()
	{
		var push = State( "Push", null, null );
		push.Play = new StatePlayDefinition { Clip = "mongo", MirrorClip = "regular" };
		var graph = new StateGraph( Root( push ) );
		graph.Update( new StateFacts { Mirrored = true }, 0.1f );
		Assert.Equal( "mongo", graph.Clip );
		var mirrored = new StateGraph( Root( push ) );
		var facts = new StateFacts { Mirrored = true };
		mirrored.Update( facts, 0.1f );
		// The first entry happens at construction, before any facts; re-entering applies them.
		var again = State( "Again", null, null );
		again.Play = push.Play;
		var idle = State( "Idle", null, "idle" );
		idle.Transitions.Add( new StateTransitionDefinition { Target = "Again", Guard = Intent( "go" ) } );
		var graph2 = new StateGraph( Root( idle, Inactive( again ) ) );
		facts.Intents[ "go" ] = 1f;
		graph2.Update( facts, 0.1f );
		Assert.Equal( "regular", graph2.Clip );
	}

	[Fact]
	public void TransitionTimeInParentCountsTimeInTheOwningState()
	{
		// A landing hands over to a push only after it has played a moment, however long its parent has been current.
		var land = State( "Land", null, "land" );
		land.Transitions.Add( new StateTransitionDefinition
		{
			Target = "Push",
			Guard = new StateTestDefinition { Test = "time-in-parent", Mask = 1, Compare = { [ ">" ] = 0.1f } }
		} );
		var air = State( "Air", null, "air" );
		air.Transitions.Add( new StateTransitionDefinition { Target = "Land", Guard = Intent( "land" ) } );
		var graph = new StateGraph( Root( State( "OnBoard", null, null, air, land, State( "Push", Intent( "push" ), "push" ) ) ) );
		var facts = new StateFacts();
		graph.Update( facts, 0.1f );
		graph.Update( facts, 1f );
		Assert.Equal( "OnBoard.Air", graph.State );

		facts.Intents[ "land" ] = 1f;
		facts.Intents[ "push" ] = 1f;
		graph.Update( facts, 0.02f );
		Assert.Equal( "OnBoard.Land", graph.State );
		graph.Update( facts, 0.05f );
		Assert.Equal( "OnBoard.Land", graph.State );
		graph.Update( facts, 0.1f );
		Assert.Equal( "OnBoard.Push", graph.State );
	}

	private static StateGraphDefinition Inactive( StateGraphDefinition state )
	{
		state.Active = false;
		return state;
	}

	private static StateGraphDefinition Root( params StateGraphDefinition[] children ) => State( "Root", null, null, children );

	private static StateGraphDefinition State( string name, StateTestDefinition guard, string clip, params StateGraphDefinition[] children ) => new()
	{
		Name = name,
		Guard = guard,
		Play = clip is null ? null : new StatePlayDefinition { Clip = clip, BlendTime = 0f },
		States = children.ToList()
	};

	private static StateTestDefinition Intent( string name, int mask = 1 ) => new() { Test = "intent", Intent = name, Mask = mask };
}
