namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;

/// <summary>Active approach inputs in one consistent rig coordinate frame.</summary>
internal readonly record struct GrindApproachInput(
	LimbRigidTransform Board, Vector3 Velocity, Vector3 AngularVelocity,
	Vector3 Normal, Vector3 EdgeStart, Vector3 EdgeEnd, float TimeStep, bool CandidateZero );
