namespace SkateMotion.Core.Physics;

/// <summary>Independent mass switches, damping and contact response for the secondary mode.</summary>
internal readonly record struct PhysicalRigSecondaryMode(
	bool AlternateMass, bool AlternateInertia, bool SpecialContactResponse,
	float LinearDamping, float AngularDamping, float ContactResponse, float SpecialResponse );
