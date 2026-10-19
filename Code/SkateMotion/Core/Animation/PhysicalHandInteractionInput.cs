namespace SkateMotion.Core.Animation;

/// <summary>Owner activation and rig flags with current model and candidate geometry.</summary>
internal readonly record struct PhysicalHandInteractionInput(
	ulong ActionFlags, ulong RigFlags, LimbRigidTransform ModelToWorld,
	PhysicalHandCandidateInput Candidate );
