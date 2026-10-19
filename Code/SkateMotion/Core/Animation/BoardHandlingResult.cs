namespace SkateMotion.Core.Animation;

/// <summary>
/// Publish the retrieving/dropping attributes first, then the channel command, then the retrieve request,
/// and finally yaw/pitch parameters when requested. Clip identifiers belong to the supplied animation data.
/// </summary>
internal readonly record struct BoardHandlingResult(
	bool Retrieving, bool Dropping, bool RetrieveRequested, bool PublishOrientation,
	float YawDegrees, float PitchDegrees, BoardHandlingCommand Command, BoardHandlingMotion Motion,
	float BlendSeconds );
