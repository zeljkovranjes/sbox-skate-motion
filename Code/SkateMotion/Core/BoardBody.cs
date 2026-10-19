namespace SkateMotion.Core;

using Vector3 = System.Numerics.Vector3;
using Quaternion = System.Numerics.Quaternion;

internal readonly record struct BoardBody(
	Vector3 Position, Quaternion Rotation, Vector3 Velocity, Vector3 AngularVelocity, Vector3 Inertia )
{
	internal Vector3 Forward => Vector3.Transform( Vector3.UnitX, Rotation );
	internal Vector3 Left => Vector3.Transform( Vector3.UnitY, Rotation );
	internal Vector3 Up => Vector3.Transform( Vector3.UnitZ, Rotation );
	internal Vector3 PointVelocity( Vector3 point ) => Velocity + Vector3.Cross( AngularVelocity, point - Position );
	internal Vector3 LocalPoint( Vector3 point ) => Position + Vector3.Transform( point, Rotation );
	internal Vector3 AngularImpulse( Vector3 worldDelta )
	{
		var local = Vector3.Transform( worldDelta, Quaternion.Conjugate( Rotation ) );
		return Vector3.Transform( local * Inertia, Rotation );
	}

	internal float ContactMass( Vector3 point, Vector3 direction, float mass )
	{
		var lever = Vector3.Cross( point - Position, direction );
		var local = Vector3.Transform( lever, Quaternion.Conjugate( Rotation ) );
		var inverse = new Vector3( 1f / Math.Max( Inertia.X, 0.0001f ),
			1f / Math.Max( Inertia.Y, 0.0001f ), 1f / Math.Max( Inertia.Z, 0.0001f ) );
		return 1f / (1f / mass + Vector3.Dot( local * inverse, local ));
	}

	internal float ContactCoupling( Vector3 pointA, Vector3 normalA, Vector3 pointB, Vector3 normalB, float mass )
	{
		var inverseRotation = Quaternion.Conjugate( Rotation );
		var a = Vector3.Transform( Vector3.Cross( pointA - Position, normalA ), inverseRotation );
		var b = Vector3.Transform( Vector3.Cross( pointB - Position, normalB ), inverseRotation );
		return Vector3.Dot( normalA, normalB ) / mass
			+ Vector3.Dot( a / Vector3.Max( Inertia, new Vector3( 0.0001f ) ), b );
	}
}
