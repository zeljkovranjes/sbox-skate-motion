namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;

/// <summary>Supplies the predicted anchor, orientation and retained rotation request for airborne placement.</summary>
internal readonly record struct PhysicalAirFrameInput(
	LimbRigidTransform OrientationSource,
	Vector3 PredictedPosition,
	Vector3 ModelOffset,
	bool AdvanceRotation,
	bool PositiveRotation,
	uint RotationSteps );
