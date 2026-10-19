namespace SkateMotion.Core.Input;

using Vector3 = System.Numerics.Vector3;

internal readonly record struct WalkingInputContext(
	Vector3 Facing,
	bool ProjectAgainstObstruction,
	Vector3 ObstructionNormal,
	float NormalizationMinimum,
	float ObstructionAlignmentLimit );
