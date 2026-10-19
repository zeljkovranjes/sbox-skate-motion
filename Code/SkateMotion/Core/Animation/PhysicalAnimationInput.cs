namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;
using Vector4 = System.Numerics.Vector4;

/// <summary>Producer-owned inputs for body preparation, attachment and motion publication.</summary>
internal readonly record struct PhysicalAnimationInput(
	PhysicalAnimationPoseInput Pose, int RiderBodyCount, BoardAttachmentInput Attachment,
	int FirstHandBone, int SecondHandBone, Vector3 VerticalAxis, Vector3 VerticalVelocity,
	float[] Weights, LimbRigidTransform CacheFrame, bool ContactActive,
	bool PublishExternalMotion, Vector4 ExternalMotion );
