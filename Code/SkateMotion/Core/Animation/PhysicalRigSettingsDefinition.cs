using SkateMotion.Core.Physics;

namespace SkateMotion.Core.Animation;

/// <summary>Optional canonical construction and mode data supplied with a rig.</summary>
internal sealed class PhysicalRigSettingsDefinition
{
	public int Version { get; set; } = 1;
	public PhysicalRigBoneSettingsDefinition[] Bones { get; set; } = Array.Empty<PhysicalRigBoneSettingsDefinition>();
	public PhysicalRigJointSettingsDefinition[] Joints { get; set; } = Array.Empty<PhysicalRigJointSettingsDefinition>();
	public float[][] Contacts { get; set; } = Array.Empty<float[]>();
	public PhysicalRigBodyTuning? BodyConstruction { get; set; }
	public PhysicalCompoundRigDefinition CompoundBody { get; set; }
	public PhysicalJointTuningDefinition JointConstruction { get; set; }
	public PhysicalJointPreset[] DrivePresets { get; set; }
	public PhysicalJointResponseTuning? JointResponse { get; set; }
}
