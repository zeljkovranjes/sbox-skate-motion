namespace SkateMotion.Core.Animation;

using Vector4 = System.Numerics.Vector4;

/// <summary>Retained physical requests and control caches; reset by the owning update stage.</summary>
internal sealed class PhysicalAnimationAttributeState
{
	internal ulong MotionFlags { get; set; }
	internal ulong ActionFlags { get; set; }
	internal ulong RigFlags { get; set; }
	internal ulong TimingFlags { get; set; }
	internal int BoardAdjustment { get; set; }
	internal int ContactBone { get; set; }
	internal float PushSpeed { get; set; }
	internal float JumpStrength { get; set; }
	internal float JumpControlX { get; set; }
	internal float JumpControlY { get; set; }
	internal float BodySpin { get; set; }
	internal float Spin { get; set; }
	internal float Turn { get; set; }
	internal float OffboardTurn { get; set; }
	internal float OffboardMagnitude { get; set; }
	internal float BipedWorldZ { get; set; }
	internal float BipedWorldX { get; set; }
	internal float LookX { get; set; }
	internal float LookY { get; set; }
	internal float RawTurn { get; set; }
	internal float HardTurn { get; set; }
	internal float Balance { get; set; }
	internal float Brake { get; set; }
	internal float Slide { get; set; }
	internal float RevertDirection { get; set; }
	internal float ObjectMoveZ { get; set; }
	internal float ObjectMoveX { get; set; }
	internal float ObjectMoveRotation { get; set; }
	internal float GrindTranslation { get; set; }
	internal float GrindStabilityNudge { get; set; }
	internal float GrindUpDown { get; set; }
	internal float GrindGrabMinimumHeight { get; set; }
	internal float PhysicalBodySpin { get; set; }
	internal float WorldGrabY { get; set; }
	internal float WorldGrabZ { get; set; }
	internal float WipeoutControlX { get; set; }
	internal float WipeoutControlY { get; set; }
	internal float OffboardJumpX { get; set; }
	internal float OffboardJumpZ { get; set; }
	internal Vector4 AnimationTranslation { get; set; }
	internal Vector4 AnimationEndCenter { get; set; }
	internal float AnimationTime { get; set; }
	internal float CadenceEndPercent { get; set; }
	internal float PhysicsBlendSeconds { get; set; }
	internal float TurnScale { get; set; }
	internal float MagnitudeScale { get; set; }
	internal float WipeoutGestureX { get; set; }
	internal float WipeoutGestureY { get; set; }
	internal float BodyAdjustX { get; set; }
	internal float BodyAdjustZ { get; set; }
	internal float BipedStartAngle { get; set; }
	internal float BipedSpinAngle { get; set; }
	internal float BipedAnimationTime { get; set; }
	internal AnimationAttributeKey GrindName { get; set; }
	internal uint ControlFlags { get; set; }
	internal float FootstepStrength { get; set; }
	internal float PreparedControlX { get; set; }
	internal float PreparedControlY { get; set; }
	internal float HeightOverride { get; set; }
	internal bool HeightOverrideActive { get; set; }
}
