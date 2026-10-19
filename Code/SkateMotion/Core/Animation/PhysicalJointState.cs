namespace SkateMotion.Core.Animation;

/// <summary>Attachment bindings and retained output for one physical pose joint.</summary>
internal sealed class PhysicalJointState
{
	internal int ChildBone { get; set; }
	internal int FirstBodyBone { get; set; }
	internal int SecondBodyBone { get; set; }
	internal bool FirstEnabled { get; set; }
	internal bool SecondEnabled { get; set; }
	internal float FirstWeight { get; set; }
	internal float SecondWeight { get; set; }
	internal float VelocityGain { get; set; }
	internal float PositionGain { get; set; }
	internal int Mode { get; set; }
	internal float FirstStrength { get; set; }
	internal float SecondStrength { get; set; }
	internal PhysicalJointFrame FirstFrame { get; set; }
	internal PhysicalJointFrame SecondFrame { get; set; }
	internal PhysicalJointDrive FirstConstraint { get; set; }
	internal PhysicalJointDrive SecondConstraint { get; set; }
	internal PhysicalJointDrive ThirdConstraint { get; set; }
	internal PhysicalJointDrive FourthConstraint { get; set; }
	internal bool Dirty { get; set; }
}
