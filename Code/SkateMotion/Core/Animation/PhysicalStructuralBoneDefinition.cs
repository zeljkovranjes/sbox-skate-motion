namespace SkateMotion.Core.Animation;

/// <summary>Authored structural orientations and ranges for a named clip bone.</summary>
internal sealed class PhysicalStructuralBoneDefinition
{
	/// <summary>Clip bone name matching the corresponding canonical bind record.</summary>
	public string ClipBone { get; set; }
	/// <summary>Parent-to-bone quaternion X/Y/Z/W, preserved in rig axes.</summary>
	public float[] ParentToBoneRotation { get; set; }
	/// <summary>Local joint quaternion X/Y/Z/W, preserved in rig axes.</summary>
	public float[] JointRotation { get; set; }
	/// <summary>Authored swing range in radians before per-joint scaling.</summary>
	public float SwingAngle { get; set; }
	/// <summary>Authored twist range in radians before per-joint scaling.</summary>
	public float TwistAngle { get; set; }
}
