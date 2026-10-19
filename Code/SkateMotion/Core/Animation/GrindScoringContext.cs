namespace SkateMotion.Core.Animation;

using Vector4 = System.Numerics.Vector4;

/// <summary>Caller state controlling production gates and the additional airborne score.</summary>
internal readonly record struct GrindScoringContext(
	ushort UpdateCount, bool Special, ulong ExternalFlags, ulong SecondaryFlags,
	float Orientation, Vector4 Up, Vector4 Velocity, Vector4 Projection, float UpAuxiliary );
