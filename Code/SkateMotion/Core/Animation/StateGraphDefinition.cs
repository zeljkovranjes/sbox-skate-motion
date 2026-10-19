using System.Collections.Generic;

namespace SkateMotion.Core.Animation;

/// <summary>
/// One state of a hierarchical animation state graph. A state is entered when its guard's entry tests pass
/// (children are tried in order, the first that passes wins), stays while its staying tests pass, and can be
/// left early by a transition or, when interruptible, by an earlier sibling whose entry tests start passing.
/// </summary>
internal sealed class StateGraphDefinition
{
	public string Name { get; set; }

	/// <summary>"true" (default), "false", or the name of a child during which the state may be interrupted.</summary>
	public string Interruptible { get; set; } = "true";

	/// <summary>"priority" lets an earlier child take over from a later one as soon as it may be entered.</summary>
	public string Policy { get; set; }

	/// <summary>False for states that are only reached through transitions.</summary>
	public bool Active { get; set; } = true;

	public StateTestDefinition Guard { get; set; }

	/// <summary>Clip or blend played on entry.</summary>
	public StatePlayDefinition Play { get; set; }

	/// <summary>Named side effects of the state, for the host to act on; the graph itself ignores them.</summary>
	public List<string> Actions { get; set; } = new();

	public List<StateTransitionDefinition> Transitions { get; set; } = new();

	public List<StateGraphDefinition> States { get; set; } = new();
}

/// <summary>A jump to a state named by a dotted path, found from the owning state outward.</summary>
internal sealed class StateTransitionDefinition
{
	public string Target { get; set; }

	/// <summary>0 none, 1 medium, 2 high, 3 urgent; higher priorities are tried first.</summary>
	public int Priority { get; set; }

	public StateTestDefinition Guard { get; set; }
}

/// <summary>What a state plays: the clip, and the clips used instead when mirrored, riding switch or boardless.</summary>
internal sealed class StatePlayDefinition
{
	public string Clip { get; set; }
	public string MirrorClip { get; set; }
	public string SwitchClip { get; set; }
	public string NoBoardClip { get; set; }
	public float BlendTime { get; set; } = 0.2f;
	public float Speed { get; set; } = 1f;

	/// <summary>"play", "blend", "channelblend" or "sequence"; null retains legacy playback.</summary>
	public string Transition { get; set; }

	/// <summary>Clock relationship while a transition retains its previous tree.</summary>
	public AnimationTransitionMode Synchronization { get; set; }

	/// <summary>Insert below the most recently retained wrapper instead of replacing the root.</summary>
	public bool TransitionUnder { get; set; }

	/// <summary>Use the previous tree's channel weights for a channel transition.</summary>
	public bool UseChannelFromWeights { get; set; }

	/// <summary>Start the new clip at the current clip's phase instead of its beginning.</summary>
	public bool KeepPhase { get; set; }
}

/// <summary>
/// Either a group ("Op": and, or, not over "Items") or one test ("Test"). Tests:
/// clip-ending (Within, Wait), intent (Intent, Compare, Absolute), grind (Grind), physics (State),
/// previous-state and in-state (State, a dotted path), time-in-state (State, Compare), time-in-parent (Compare),
/// speed (Compare, Absolute), clip-flag (Flag), random, and fact (Fact, Compare, Arguments).
/// "Mask" says when a test applies: 1 on entry, 2 while staying, 3 both, 4 on leaving.
/// </summary>
internal sealed class StateTestDefinition
{
	public string Op { get; set; }
	public List<StateTestDefinition> Items { get; set; } = new();
	public string Test { get; set; }
	public int Mask { get; set; } = 1;
	public float Within { get; set; }
	public bool Wait { get; set; } = true;
	public string Intent { get; set; }
	public string Grind { get; set; }
	public string State { get; set; }
	public string Flag { get; set; }
	public string Fact { get; set; }
	public bool Absolute { get; set; }

	/// <summary>Comparisons the value must satisfy: keys &lt;, &lt;=, &gt;, &gt;=, ==, &gt;| (absolute greater).</summary>
	public Dictionary<string, float?> Compare { get; set; } = new();

	public Dictionary<string, string> Arguments { get; set; } = new();
}
