using System.Collections.Generic;

namespace SkateMotion.Core.Animation;

/// <summary>
/// Data that drives the animation graph. Motions tag every clip; states pick motions by matching tags against
/// parameters. The first state whose conditions hold wins, so states are listed in priority order.
/// </summary>
internal sealed class AnimationGraphDefinition
{
	/// <summary>Stance the clips were authored in, "regular" or "goofy"; riders in the other stance are mirrored.</summary>
	public string Stance { get; set; } = "regular";

	/// <summary>Optional raw-pose file in the data folder, matched to the keyed clip set before blends are built.</summary>
	public string RawPosePath { get; set; } = "";

	/// <summary>Attribute bank accompanying the raw poses, with an explicit entry for every clip.</summary>
	public string AttributeBankPath { get; set; } = "";

	/// <summary>Optional resolved posture profile, validated against the clip skeleton before tree construction.</summary>
	public string PostureProfilePath { get; set; } = "";

	/// <summary>Optional retained base playback with an explicit owner reference and parameter bindings.</summary>
	public BaseAnimationPlaybackDefinition BasePlayback { get; set; }

	/// <summary>Optional board handling motions layered over the locomotion graph.</summary>
	public BoardHandlingAnimationDefinition BoardHandling { get; set; }

	/// <summary>Optional caller-owned retrieval curves and angular profile in the local data folder.</summary>
	public string BoardRetrievalProfilePath { get; set; } = "";

	/// <summary>
	/// Tag whose change within one family plays a joining motion first: going from "walk" to "stand" plays the
	/// motion tagged "walk-stand" when there is one. Empty disables transitions.
	/// </summary>
	public string TransitionTag { get; set; } = "";

	/// <summary>
	/// Clip bone that represents the board. While riding, the avatar is placed so this bone sits on the real
	/// deck instead of the clip's root. Empty anchors the root.
	/// </summary>
	public string AnchorBone { get; set; } = "";

	/// <summary>Hand role holding the authored board, "left_hand" or "right_hand". Empty keeps the fallback carry pose.</summary>
	public string CarryHand { get; set; } = "";

	/// <summary>Size of the board the clips were made on; empty keeps the default board's (see <see cref="BoardFit"/>).</summary>
	public ClipBoardDefinition Board { get; set; }

	/// <summary>Optional finger pose banks, one per hand, driven by per-clip streams (see <see cref="HandPoseLayer"/>).</summary>
	public List<HandPoseDefinition> HandPoses { get; set; } = new();

	/// <summary>
	/// Clips that turn the rider round to ride switch (the other foot forward, the board's tail leading). Each start of
	/// one swaps the rider's stance; the clips that follow it play mirrored and facing the other way. Empty disables
	/// switch riding.
	/// </summary>
	public List<string> StanceSwapClips { get; set; } = new();

	/// <summary>Optional physical bind metadata, independent of the model used to display the clips.</summary>
	public EffectorRigDefinition Rig { get; set; }

	/// <summary>Optional per-clip looping permissions. Omitted clips retain caller-selected looping.</summary>
	public Dictionary<string, bool> ClipLooping { get; set; } = new();

	/// <summary>Optional per-clip layer weights in skeleton order. Omitted clips influence every bone fully.</summary>
	public Dictionary<string, float[]> ClipBoneWeights { get; set; } = new();

	/// <summary>Every clip the graph may play, with its descriptive tags.</summary>
	public List<AnimationMotionDefinition> Motions { get; set; } = new();

	/// <summary>States in priority order.</summary>
	public List<AnimationStateDefinition> States { get; set; } = new();

	/// <summary>Named blends of clips; a motion's Clip may name one. See <see cref="BlendNode"/>.</summary>
	public Dictionary<string, BlendDefinition> Blends { get; set; } = new();

	/// <summary>
	/// Optional state graph that decides what plays (see <see cref="StateGraph"/>). When present it replaces the
	/// tagged states; motions and blends still supply the clips it names.
	/// </summary>
	public StateGraphDefinition StateMachine { get; set; }
}

/// <summary>One named blend: a type ("blend", "select" or "switch"), the parameter it reads and its children.</summary>
internal sealed class BlendDefinition
{
	public string Type { get; set; } = "blend";

	/// <summary>Decoded construction kind; zero retains the legacy blend description.</summary>
	public int SourceKind { get; set; }

	/// <summary>Attribute queried from child trees when constructing directional sample positions.</summary>
	public string SampleAttribute { get; set; }

	public string Parameter { get; set; }

	public List<BlendChildDefinition> Children { get; set; } = new();
}

/// <summary>A clip or blend inside a blend, with its place along the parameter or its select key.</summary>
internal sealed class BlendChildDefinition
{
	public string Name { get; set; }

	/// <summary>Parameter value where this child is fully weighted; children are evenly spaced from 0 to 1 when omitted.</summary>
	public float? At { get; set; }

	/// <summary>Text the select parameter must equal for this child.</summary>
	public string Key { get; set; }
}

/// <summary>One clip and the tags states use to find it.</summary>
internal sealed class AnimationMotionDefinition
{
	/// <summary>Clip name in the clip set.</summary>
	public string Clip { get; set; }

	/// <summary>Tag values, e.g. family, kind, side, phase.</summary>
	public Dictionary<string, string> Tags { get; set; } = new();
}

/// <summary>A graph state: when it applies and what it plays.</summary>
internal sealed class AnimationStateDefinition
{
	/// <summary>Name for debugging and events.</summary>
	public string Name { get; set; }

	/// <summary>All conditions must hold for the state to start or continue.</summary>
	public List<AnimationConditionDefinition> When { get; set; } = new();

	/// <summary>Motions played in order; the last one loops or holds.</summary>
	public List<AnimationStepDefinition> Steps { get; set; } = new();

	/// <summary>Whether the last step loops.</summary>
	public bool Loop { get; set; }

	/// <summary>Keeps playing until the steps finish even when the conditions stop holding.</summary>
	public bool Lock { get; set; }

	/// <summary>Optional motion played once when the state is left, before the next state starts.</summary>
	public AnimationStepDefinition Exit { get; set; }

	/// <summary>Crossfade time into this state, in seconds.</summary>
	public float BlendTime { get; set; } = 0.15f;

	/// <summary>Playback rate.</summary>
	public float Speed { get; set; } = 1f;

	/// <summary>Optional parameter multiplying the playback rate.</summary>
	public string SpeedParameter { get; set; }
}

/// <summary>Compares a parameter with a value: ==, !=, &gt;, &lt;, &gt;= or &lt;=.</summary>
internal sealed class AnimationConditionDefinition
{
	/// <summary>Parameter name.</summary>
	public string Parameter { get; set; }

	/// <summary>Comparison operator.</summary>
	public string Op { get; set; } = "==";

	/// <summary>Number or text to compare with.</summary>
	public string Value { get; set; }
}

/// <summary>Selects the motion for one step, optionally blending neighbours along a parameter.</summary>
internal sealed class AnimationStepDefinition
{
	/// <summary>Tags a motion must have. A value starting with $ is replaced by that parameter's text.</summary>
	public Dictionary<string, string> Select { get; set; } = new();

	/// <summary>
	/// Tags a motion should have. The motions matching the most preferences win; "kind" outweighs all others.
	/// Values starting with $ are replaced by parameters, and empty values are ignored.
	/// </summary>
	public Dictionary<string, string> Prefer { get; set; } = new();

	/// <summary>When no motion matches, the step is skipped instead of making the state unavailable.</summary>
	public bool Optional { get; set; }

	/// <summary>Parameter that blends between the motions listed in <see cref="BlendPoints"/>.</summary>
	public string BlendParameter { get; set; }

	/// <summary>Tag whose values identify the blend points.</summary>
	public string BlendTag { get; set; }

	/// <summary>Parameter value at which each tag value is fully weighted.</summary>
	public Dictionary<string, float> BlendPoints { get; set; } = new();
}
