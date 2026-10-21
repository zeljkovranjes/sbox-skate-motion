namespace SkateMotion.Core.Animation;

/// <summary>Graph data naming one hand's pose bank in the data folder and the clip stream that drives it.</summary>
internal sealed class HandPoseDefinition
{
	/// <summary>Clip skeleton bone the bank's fingers hang from, e.g. "left_hand".</summary>
	public string Hand { get; set; } = "";

	/// <summary>Raw-pose file in the data folder holding the finger bones, their reference pose and the pose clip.</summary>
	public string Bank { get; set; } = "";

	/// <summary>Clip in the bank whose frames are the poses.</summary>
	public string Clip { get; set; } = "poses";

	/// <summary>Name of the per-clip stream that selects this hand's pose.</summary>
	public string Stream { get; set; } = "";
}
