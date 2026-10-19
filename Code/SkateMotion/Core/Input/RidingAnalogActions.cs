namespace SkateMotion.Core.Input;

/// <summary>Analog requests retain presence separately where zero values are meaningful.</summary>
internal readonly record struct RidingAnalogActions
{
	internal float BodySpin { get; init; }
	internal bool HasTweak { get; init; }
	internal float TweakX { get; init; }
	internal float TweakY { get; init; }
	internal bool HasBoardAdjust { get; init; }
	internal float BoardAdjustAngle { get; init; }
	internal float BoardAdjustMagnitude { get; init; }
	internal float KickTurn { get; init; }
	internal float GrindBalance { get; init; }
	internal bool HasAnticipation { get; init; }
	internal float AnticipationAngle { get; init; }
	internal float AnticipationMagnitude { get; init; }
	internal float GrindTranslation { get; init; }
	internal float GrindStabilityNudge { get; init; }
	internal float GrindUpDown { get; init; }
	internal bool HasAutoPump { get; init; }
	internal float AutoPumpAngle { get; init; }
	internal float AutoPumpMagnitude { get; init; }
	internal bool HasHardTurn { get; init; }
	internal float HardTurn { get; init; }
	internal float HardTurnCrouch { get; init; }
	internal bool HasCrouch { get; init; }
	internal float Crouch { get; init; }
	internal float Turn { get; init; }
	internal bool RightSlideStart { get; init; }
	internal bool LeftSlideStart { get; init; }
	internal float LeftSlide { get; init; }
	internal float RightSlide { get; init; }
}
