namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;

/// <summary>Supplies airborne placement requests and physical update inputs.</summary>
internal readonly record struct PhysicalAirCharacterInput(
	LimbRigidTransform OrientationSource,
	Vector3 PredictedPosition,
	PhysicalRigInput RigInput,
	bool AdvanceRotation,
	bool PositiveRotation,
	uint RotationSteps,
	float BoardDeltaTime,
	float BoardFeedbackGain );
