namespace SkateMotion.Core.Animation;

/// <summary>Construction lock and primary/alternate swing and twist multipliers.</summary>
internal sealed class PhysicalRigJointSettingsDefinition
{
	public bool Locked { get; set; }
	public float[] Ranges { get; set; } = Array.Empty<float>();
}
