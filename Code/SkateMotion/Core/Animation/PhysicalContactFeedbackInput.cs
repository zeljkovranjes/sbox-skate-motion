namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;

/// <summary>Movement-state gates and reference motion for one contact-feedback update.</summary>
internal readonly record struct PhysicalContactFeedbackInput(
	Vector3 Position, Vector3 Up, Vector3 Velocity, Vector3 PreviousVelocity, float DeltaTime,
	bool SecondaryMode, bool ActorGate, bool AlternateObjectScale, bool SpecialState,
	bool AlternateReference, bool Mode600, bool ResetTimer );
