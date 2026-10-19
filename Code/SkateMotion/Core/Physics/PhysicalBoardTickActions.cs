namespace SkateMotion.Core.Physics;

/// <summary>Caller-owned physical operations invoked synchronously by the board tick.</summary>
internal sealed record PhysicalBoardTickActions(
	Func<bool> IsBeyondPlacementRange,
	Action<int, int> ChangeState,
	Action ClearVelocity,
	Action UpdateHeldFeedback,
	Action<float> UpdateContactDirections,
	Action UpdateHeldFrame,
	Action UpdateFreeBoard,
	Action UpdatePlacement,
	Action UpdateRetrieval );
