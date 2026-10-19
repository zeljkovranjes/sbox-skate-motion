namespace SkateMotion.Core.Animation;

/// <summary>Authored angular reduction, release and interpolation constants.</summary>
internal readonly record struct BoardFrameCorrectionSettings(
	float FullTurn, float InverseFullTurn, float ReleaseWeight,
	float LinearCosineThreshold, float QuaternionMatrixScale );
