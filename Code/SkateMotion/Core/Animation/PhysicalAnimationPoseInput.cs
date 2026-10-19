namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;

/// <summary>Clip poses, rig mappings and independent animation and physical time steps.</summary>
internal readonly record struct PhysicalAnimationPoseInput(
	LimbRigidTransform[] Clips, int MotionBone, int[] SolverToClip,
	int[] HelperClipBones, int[] EndBones, LimbRigidTransform ModelToWorld,
	float DeltaTime, float OwnerDeltaTime, Vector3 BoardVelocity,
	PhysicalAnimationAttributeInput Attributes );
