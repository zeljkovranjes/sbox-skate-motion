namespace SkateMotion.Core.Animation;

/// <summary>Optional graph metadata describing a rig independently of clip bone order.</summary>
internal sealed class EffectorRigDefinition
{
	/// <summary>Metadata format version.</summary>
	public int Version { get; set; } = 1;
	/// <summary>Clip X/Y/Z as signed one-based rig axes; dimensions and binds remain in rig space.</summary>
	public int[] ClipAxes { get; set; } = new[] { 1, 2, 3 };
	/// <summary>Bones in solver order.</summary>
	public EffectorRigBoneDefinition[] Bones { get; set; } = Array.Empty<EffectorRigBoneDefinition>();
	/// <summary>Left foot, right foot, left hand and right hand bindings, in that order.</summary>
	public EffectorRigSlotDefinition[] Effectors { get; set; } = Array.Empty<EffectorRigSlotDefinition>();
	/// <summary>Optional versioned structural orientations and angular ranges in the same bone order.</summary>
	public PhysicalStructuralRigDefinition PhysicalJoints { get; set; }
	/// <summary>Optional versioned body, drive, joint range and contact settings.</summary>
	public PhysicalRigSettingsDefinition PhysicalSettings { get; set; }
}
