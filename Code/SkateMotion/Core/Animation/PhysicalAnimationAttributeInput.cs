namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;

/// <summary>Live animation, contact and control sources for one ordered attribute update.</summary>
/// <remarks>Controls contain the primary action, preparation secondary action and publication secondary action, in that order.</remarks>
/// <remarks>BoardDisplacement is the extracted motion translation, independent of the mapped board pose.</remarks>
internal readonly record struct PhysicalAnimationAttributeInput(
	PhysicalAnimationAttribute[] Attributes, AnimationAttributeKey[] BoneNames,
	Vector3 BoardDisplacement, int RightToeBone, float DeltaTime,
	bool HasController, Vector3 Controls, bool UsePreparedControls,
	bool SelectJumpExtremes, float LowJumpThreshold, float HighJumpThreshold,
	bool AllowHeightOverride, bool ExternalImpulseActive, ulong AnimationFlags );
