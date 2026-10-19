namespace SkateMotion.Core.Animation;

/// <summary>Solver ordering, collapsed parent links and initial transforms for a supplied clip skeleton.</summary>
internal sealed record EffectorSkeletonHierarchy(
	int[] SolverToClip,
	int[] ClipToSolver,
	int[] Parents,
	int[][] Children,
	int[] EdgeIndices,
	int[] EdgeChildren,
	int[] EdgeParents,
	LimbRigidTransform[] BindTransforms,
	LimbRigidTransform[] InverseBindTransforms,
	LimbRigidTransform[] InitialBones );
