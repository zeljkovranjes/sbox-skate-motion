using SkateMotion.Core.Animation;

namespace SkateMotion.Core.Physics;

using static SkateMotion.Core.Animation.LimbSolverMath;

/// <summary>Constructs the two retained feedback bodies after the canonical physical skeleton.</summary>
internal static class PhysicalFeedbackBodyConstruction
{
	internal static PhysicalFeedbackBodyConstructionResult Create( int canonicalBody, float axisTolerance )
	{
		if ( canonicalBody is not (24 or 25) )
			throw new ArgumentOutOfRangeException( nameof( canonicalBody ) );
		var radius = canonicalBody == 24 ? .23f : .36f;
		var halfLength = canonicalBody == 24 ? .1f : .26f;
		var mass = canonicalBody == 24 ? .1f : .01f;
		var properties = PhysicalPrimitiveMassProperties.Capsule( radius, halfLength );
		var descriptor = PhysicalBodyMassConstruction.Create( properties, LimbRigidTransform.Identity, mass, 1f, axisTolerance );
		var inertia = descriptor.Mass.InverseInertia * Reciprocal( 5f );
		descriptor = descriptor with
		{
			Mass = descriptor.Mass with { InverseInertia = inertia },
			MaximumInertia = 1f / MathF.Min( MathF.Min( inertia.X, inertia.Y ), inertia.Z )
		};
		const float damping = 29.999998092651367f;
		return new( radius, halfLength, descriptor, damping, damping, false );
	}
}
