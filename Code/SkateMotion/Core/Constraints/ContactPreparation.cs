using SkateMotion.Core.Collision;

namespace SkateMotion.Core.Constraints;

using Vector3 = System.Numerics.Vector3;

internal static class ContactPreparation
{
	internal static ContactGeometry Prepare( in DynamicBody a, in DynamicBody b, in ContactManifoldPoint contact )
	{
		var velocityA = PointVelocity( a, contact.PointA );
		var velocityB = PointVelocity( b, contact.PointB );
		var velocity = velocityB - velocityA;
		var normal = contact.Normal;
		var tangent = CrossFromZero( velocity, normal );
		var lengthSquared = SquaredLength( tangent );
		// Preserve the writer's largest-subnormal cutoff, including its equality case.
		if ( lengthSquared < 1.1754942106924411e-38 )
		{
			if ( MathF.FusedMultiplyAdd( -normal.X, normal.X, .5f ) >= -0f )
			{
				tangent = ProjectAxis( Vector3.UnitX, normal, normal.X );
				lengthSquared = tangent.X;
			}
			else
			{
				tangent = ProjectAxis( Vector3.UnitZ, normal, normal.Z );
				lengthSquared = tangent.Z;
				tangent = -tangent;
			}
		}
		// This finite arithmetic model uses an exact reciprocal-root estimate.
		var inverseLength = (float)(1.0 / Math.Sqrt( lengthSquared ));
		var second = new Vector3(
			MathF.FusedMultiplyAdd( -tangent.Y, normal.Z, tangent.Z * normal.Y ),
			MathF.FusedMultiplyAdd( -tangent.Z, normal.X, tangent.X * normal.Z ),
			MathF.FusedMultiplyAdd( -tangent.X, normal.Y, tangent.Y * normal.X ) );
		return new ContactGeometry( contact.BodyA, contact.BodyB, contact.PointA, contact.PointB,
			normal, tangent * inverseLength, second * inverseLength,
			contact.StaticFriction, contact.SlidingFriction, contact.Restitution, velocity )
		{
			Materials = contact.Materials
		};
	}

	private static double SquaredLength( Vector3 value )
	{
		var result = MathF.FusedMultiplyAdd( value.Z, value.Z,
			MathF.FusedMultiplyAdd( value.Y, value.Y, value.X * value.X ) );
		if ( BitConverter.SingleToUInt32Bits( result ) >= 0x00800000u )
			return result;
		// Native float modes can erase the cutoff and the squared slip. Retain
		// single-precision rounding in a wider container for this small range.
		var x = CollisionScalarMath.Widen( value.X );
		var y = CollisionScalarMath.Widen( value.Y );
		var z = CollisionScalarMath.Widen( value.Z );
		var xy = CollisionScalarMath.Round( Math.FusedMultiplyAdd( y, y, CollisionScalarMath.Product( x, x ) ) );
		return CollisionScalarMath.Round( Math.FusedMultiplyAdd( z, z, xy ) );
	}

	private static Vector3 PointVelocity( in DynamicBody body, Vector3 point )
	{
		var offset = point - body.Position;
		var angular = body.AngularVelocity;
		return new Vector3(
			MathF.FusedMultiplyAdd( offset.Z, angular.Y,
				MathF.FusedMultiplyAdd( -offset.Y, angular.Z, body.Velocity.X ) ),
			MathF.FusedMultiplyAdd( offset.X, angular.Z,
				MathF.FusedMultiplyAdd( -offset.Z, angular.X, body.Velocity.Y ) ),
			MathF.FusedMultiplyAdd( offset.Y, angular.X,
				MathF.FusedMultiplyAdd( -offset.X, angular.Y, body.Velocity.Z ) ) );
	}

	private static Vector3 CrossFromZero( Vector3 a, Vector3 b ) => new(
		MathF.FusedMultiplyAdd( a.Y, b.Z, MathF.FusedMultiplyAdd( -a.Z, b.Y, 0f ) ),
		MathF.FusedMultiplyAdd( a.Z, b.X, MathF.FusedMultiplyAdd( -a.X, b.Z, 0f ) ),
		MathF.FusedMultiplyAdd( a.X, b.Y, MathF.FusedMultiplyAdd( -a.Y, b.X, 0f ) ) );

	private static Vector3 ProjectAxis( Vector3 axis, Vector3 normal, float component ) => new(
		MathF.FusedMultiplyAdd( -normal.X, component, axis.X ),
		MathF.FusedMultiplyAdd( -normal.Y, component, axis.Y ),
		MathF.FusedMultiplyAdd( -normal.Z, component, axis.Z ) );
}
