namespace SkateMotion.Core.Animation;

/// <summary>Caller-supplied edge sampling lengths, clearance and sample rate.</summary>
internal readonly record struct GrindEdgeSampleSettings(
	float MaximumEdgeVectorLength, float MinimumDirectionLength, float EndpointExtension,
	float ContactOffset, float SamplesPerSecond, float HalfTurn );
