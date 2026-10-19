namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;

/// <summary>Owner and direction fields consumed by the board's non-launch motor.</summary>
internal readonly record struct PhysicalBoardForceInput( ulong MotionFlags, ulong ActionFlags,
	LimbRigidTransform ReferenceFrame, LimbRigidTransform PhysicalFrame, Vector3 SurfaceNormal,
	float LateralSuppression, Vector3 FilteredDirection, Vector3 TargetDirection,
	Vector3 Velocity, Vector3 TravelDirection, float Mass, float DeltaTime );
