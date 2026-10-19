namespace SkateMotion.Core.Animation;

/// <summary>Requests, animated inputs, contacts and activation values for one invocation.</summary>
internal readonly record struct EffectorRigInput(
	EffectorPoseInput[] Poses,
	bool[] Pinning,
	bool[] Releasing,
	FootReachInput[] Contacts,
	bool FeetEnabled,
	bool LeftHandEnabled,
	bool RightHandEnabled,
	bool DisableFeet,
	int SelectedContactId,
	bool ReactionEnabled,
	bool RequestSuppressed,
	float MinimumDegrees,
	float MaximumDegrees );
