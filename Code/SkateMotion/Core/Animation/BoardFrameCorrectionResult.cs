namespace SkateMotion.Core.Animation;

using Vector4 = System.Numerics.Vector4;

/// <summary>The corrected board frame and retained interpolation state.</summary>
internal readonly record struct BoardFrameCorrectionResult(
	LimbRigidTransform Frame, Vector4 Orientation, byte CorrectionFlag );
