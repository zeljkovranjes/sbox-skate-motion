namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;

/// <summary>Placement and physical inputs supplied to the complete character update.</summary>
internal readonly record struct PhysicalCharacterInput(
	LimbRigidTransform SourceFrame, LimbRigidTransform LocalFrame, Vector3 ReferencePosition,
	PhysicalRigInput RigInput, float BoardDeltaTime, float BoardFeedbackGain );
