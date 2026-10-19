namespace SkateMotion.Core.Animation;

using Vector4 = System.Numerics.Vector4;

/// <summary>Candidate contact, validity time and query inputs for fallback rejection.</summary>
internal readonly record struct GrindCandidateRejectionInput(
	Vector4 Point, Vector4 Direction, Vector4 Time, int SampleIndex,
	bool Preliminary, int Filter, float CorrectionAuxiliary );
