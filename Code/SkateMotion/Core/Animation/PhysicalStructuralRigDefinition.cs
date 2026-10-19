namespace SkateMotion.Core.Animation;

/// <summary>Optional versioned structural data accompanying the canonical bind metadata.</summary>
internal sealed class PhysicalStructuralRigDefinition
{
	/// <summary>Structural metadata format version.</summary>
	public int Version { get; set; } = 1;
	/// <summary>Authored orientations and limits in canonical bone order.</summary>
	public PhysicalStructuralBoneDefinition[] Bones { get; set; } = Array.Empty<PhysicalStructuralBoneDefinition>();
}
