namespace SkateMotion.Core.Animation;

/// <summary>A blended pose and the rotation angle remaining before its target.</summary>
internal readonly record struct LimbPoseBlendResult( LimbRigidTransform Transform, float RemainingAngle );
