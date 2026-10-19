namespace SkateMotion.Core.Animation;

/// <summary>Frames, requests and activation policy for one physical rig update.</summary>
internal readonly record struct PhysicalRigInput(
	EffectorRigFrameSources Frames,
	EffectorRigInput RigInput,
	PhysicalPoseSettings JointSettings,
	byte[] JointMask,
	float DeltaTime,
	float Gravity,
	bool SolveRig,
	bool BindAnimatedRoot,
	bool MarkReset );
