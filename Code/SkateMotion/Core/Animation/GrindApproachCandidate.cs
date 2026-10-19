namespace SkateMotion.Core.Animation;

/// <summary>A board-point candidate with angular, distance and response parameters.</summary>
internal readonly record struct GrindApproachCandidate(
	float TargetAngle, float AngleTolerance, float DistanceLimit, float ResponseFactor, int PointIndex );
