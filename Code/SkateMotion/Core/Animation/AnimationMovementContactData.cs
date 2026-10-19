namespace SkateMotion.Core.Animation;

/// <summary>Retained contact metadata copied by the movement classifier.</summary>
internal readonly record struct AnimationMovementContactData(
	int Kind, int Index, byte Flag, float Value,
	ulong FirstFlags, ulong SecondFlags,
	AnimationMovementContactKey FirstKey, AnimationMovementContactKey SecondKey );
