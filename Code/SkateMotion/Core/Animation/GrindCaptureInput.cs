namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;

/// <summary>Selection state and directions used by the capture eligibility gate.</summary>
internal readonly record struct GrindCaptureInput(
	bool Suppressed, bool ProducerAccepted, ushort UpdateCount, short CandidateCount, short SelectedIndex,
	Vector3 SelectedDirection, Vector3 ReferenceDirection );
