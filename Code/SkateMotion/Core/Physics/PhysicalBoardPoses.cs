namespace SkateMotion.Core.Physics;

using SkateMotion.Core.Animation;
using Vector3 = System.Numerics.Vector3;

/// <summary>Distinct processed, attachment and hand frames observed for one board tick.</summary>
internal readonly record struct PhysicalBoardPoses(
	LimbRigidTransform ProcessedBoard,
	LimbRigidTransform AnimatedBoard,
	LimbRigidTransform RetrievalAttachment,
	LimbRigidTransform FirstHand,
	LimbRigidTransform SecondHand,
	Vector3 FirstFeedbackHand,
	Vector3 SecondFeedbackHand,
	Vector3 ReferencePosition,
	Vector3 PlacementPosition,
	Vector3 PlacementDirection,
	Vector3 RetrievalFallback,
	Vector3 ContactDirection );
