namespace SkateMotion.Core.Animation;

/// <summary>Completed physical observations and request presence for board handling.</summary>
internal readonly record struct BoardHandlingInput(
	bool GrabbingObject, bool HoldingBoard, bool RetrievalBlocked, bool RetrievalActive,
	bool DropRequested, bool ThrowRequested, bool RetrieveRequested,
	float YawRadians, float PitchRadians, bool Mirrored );
