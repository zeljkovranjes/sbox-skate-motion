namespace SkateMotion.Core.Animation;

/// <summary>Optional cylinder transform and dimensions for the canonical compound body.</summary>
internal sealed class PhysicalCompoundRigDefinition
{
	public float[] Angles { get; set; } = Array.Empty<float>();
	public float[] Position { get; set; } = Array.Empty<float>();
	public float CylinderHeight { get; set; }
	public float CylinderRadius { get; set; }
}
