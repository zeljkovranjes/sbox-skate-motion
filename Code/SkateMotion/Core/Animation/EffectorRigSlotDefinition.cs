namespace SkateMotion.Core.Animation;

/// <summary>Separate helper clip bone, solver endpoint and optional linked reference.</summary>
internal sealed class EffectorRigSlotDefinition
{
	/// <summary>Authored target helper name in the clip skeleton.</summary>
	public string HelperBone { get; set; }
	/// <summary>Endpoint name among the solver bones.</summary>
	public string EndBone { get; set; }
	/// <summary>Linked bone name among solver bones, or empty when absent.</summary>
	public string ReferenceBone { get; set; }
}
