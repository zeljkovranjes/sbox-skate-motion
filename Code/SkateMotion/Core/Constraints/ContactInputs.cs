namespace SkateMotion.Core.Constraints;

using Vector3 = System.Numerics.Vector3;

internal readonly record struct ContactInputs( ContactBody A, ContactBody B, ContactGeometry Geometry )
{
	internal static ContactInputs FromGeneratedManifold( in DynamicBody a, in DynamicBody b, in ContactManifoldPoint contact ) =>
		new( Body( a ), Body( b ), GeneratedContactPreparation.Prepare( a, b, contact ) );

	internal static ContactInputs FromManifold( in DynamicBody a, in DynamicBody b, in ContactManifoldPoint contact ) =>
		new( Body( a ), Body( b ), ContactPreparation.Prepare( a, b, contact ) );

	internal static ContactInputs FromBodies( in DynamicBody a, in DynamicBody b, in CollisionContact contact )
	{
		var velocityA = a.Velocity + Vector3.Cross( a.AngularVelocity, contact.PointA - a.Position );
		var velocityB = b.Velocity + Vector3.Cross( b.AngularVelocity, contact.PointB - b.Position );
		// Kinematic surface velocity remains available even when its inverse response is zero.
		var geometry = new ContactGeometry( contact.BodyA, contact.BodyB, contact.PointA, contact.PointB,
			contact.Normal, contact.TangentA, contact.TangentB,
			contact.StaticFriction, contact.SlidingFriction, contact.Restitution, velocityB - velocityA )
		{
			Materials = contact.Materials
		};
		return new ContactInputs( Body( a ), Body( b ), geometry );
	}

	private static ContactBody Body( in DynamicBody body ) => new(
		body.Position, body.InverseMass, body.WorldInverseInertia,
		body.Acceleration, body.AngularAcceleration, body.InverseMass > 0f )
	{
		MotionFlags = body.MotionFlags
	};
}
