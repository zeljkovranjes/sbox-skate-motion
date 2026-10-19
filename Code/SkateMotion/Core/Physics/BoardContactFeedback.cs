namespace SkateMotion.Core.Physics;

using Vector3 = System.Numerics.Vector3;

/// <summary>Direction pair, alignment threshold and activation state for board contact feedback.</summary>
internal readonly record struct BoardContactFeedback(
	Vector3 FirstDirection, Vector3 SecondDirection, float AlignmentThreshold, bool Enabled, ulong BodyFlags );
