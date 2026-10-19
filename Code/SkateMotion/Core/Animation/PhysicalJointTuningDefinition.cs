namespace SkateMotion.Core.Animation;

/// <summary>Authored structural joint speeds before fixed-step conversion and angular locks.</summary>
internal sealed class PhysicalJointTuningDefinition
{
	public float[] LinearSpeed { get; set; } = Array.Empty<float>();
	public float SwingSpeed { get; set; }
	public float TwistSpeed { get; set; }
	public bool LockSwing { get; set; }
	public bool LockTwist { get; set; }
}
