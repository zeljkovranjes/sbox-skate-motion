namespace SkateMotion.Core.Physics;

/// <summary>Capsule geometry and descriptor settings for a noncolliding feedback body.</summary>
internal readonly record struct PhysicalFeedbackBodyConstructionResult(
	float Radius, float HalfLength, PhysicalBodyMassResult Descriptor,
	float LinearDamping, float AngularDamping, bool CollisionEnabled );
