namespace SkateMotion.Core.Animation;

/// <summary>Authored body and drive values for one named canonical bone.</summary>
internal sealed class PhysicalRigBoneSettingsDefinition
{
	public string ClipBone { get; set; }
	public float PrimaryMassCoefficient { get; set; }
	public float AlternateMassCoefficient { get; set; }
	public int[] BodyFlags { get; set; } = Array.Empty<int>();
	public int Shape { get; set; }
	public float SizeScale { get; set; }
	public uint BodyValue { get; set; }
	public float[] DriveCoefficients { get; set; } = Array.Empty<float>();
	public float DriveValue { get; set; }
	public float FirstWeight { get; set; }
	public float SecondWeight { get; set; }
	public byte FeedbackFlag { get; set; }
	public float FeedbackWeight { get; set; }
}
