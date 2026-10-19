using SkateMotion.Core.Physics;

namespace SkateMotion.Core.Animation;

using Vector4 = System.Numerics.Vector4;

/// <summary>Copied construction data, drive lanes and feedback values for one bone.</summary>
internal readonly record struct PhysicalRigBoneSettings(
	PhysicalRigBodyDefinition Body, byte FirstFlag, byte SecondFlag, uint BodyValue,
	Vector4 DriveCoefficients, float DriveValue, float FirstWeight, float SecondWeight,
	byte FeedbackFlag, float FeedbackWeight );
