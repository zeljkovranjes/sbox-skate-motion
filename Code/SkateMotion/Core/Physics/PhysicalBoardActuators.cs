namespace SkateMotion.Core.Physics;

using SkateMotion.Core.Animation;
using Vector3 = System.Numerics.Vector3;

/// <summary>Body operations supplied by the physical adapter; no operation has an implicit fallback.</summary>
internal sealed record PhysicalBoardActuators(
	Action PrepareHeldBodies,
	Action PrepareReleasedBodies,
	Action<bool> SetJointsEnabled,
	Action ClearContactDirectionFlag,
	Func<ulong> ReadBodyFlags,
	Action<BoardContactFeedback> WriteContactFeedback,
	Action ClearVelocity,
	Action<LimbRigidTransform> WriteTargetFrame,
	Action<Vector3> Relocate,
	Action<Vector3> ApplyRetrievalFeedback,
	Action BeginThrow,
	Action<int> UpdateThrow );
