namespace SkateMotion.Core.Animation;

/// <summary>Prepared owner observations for the non-launch active motor callback.</summary>
internal readonly record struct PhysicalNonJumpMotionInput( int CharacterMode,
	float CharacterVerticalValue, float CharacterVerticalThreshold, ulong RigFlags,
	PhysicalBoardForceInput Forces, PhysicalBoardForceTuning ForceTuning,
	PhysicalRidingCharacterInput Character );
