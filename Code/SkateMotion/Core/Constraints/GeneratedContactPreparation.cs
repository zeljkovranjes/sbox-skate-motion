using static SkateMotion.Core.Collision.CollisionArithmetic;

namespace SkateMotion.Core.Constraints;

using Vector3 = System.Numerics.Vector3;

// Preparation for generated contacts under the retained runtime arithmetic profile.
internal static class GeneratedContactPreparation
{
	internal static ContactGeometry Prepare( in DynamicBody a, in DynamicBody b, in ContactManifoldPoint contact )
	{
		var velocity = Difference( PointVelocity( b, contact.PointB ), PointVelocity( a, contact.PointA ) );
		var normal = contact.Normal;
		var tangent = CrossFromZero( velocity, normal );
		var length = MultiplyAdd( Word( tangent.Z ), Word( tangent.Z ),
			MultiplyAdd( Word( tangent.Y ), Word( tangent.Y ), Multiply( Word( tangent.X ), Word( tangent.X ) ) ) );
		if ( Greater( 0x007fffffu, length ) )
		{
			if ( !Greater( 0x80000000u, NegativeMultiplySubtract( Word( normal.X ), Word( normal.X ), 0x3f000000u ) ) )
			{
				tangent = ProjectAxis( Vector3.UnitX, normal, normal.X );
				length = Word( tangent.X );
			}
			else
			{
				tangent = ProjectAxis( Vector3.UnitZ, normal, normal.Z );
				length = Word( tangent.Z );
				tangent = new( Value( Word( tangent.X ) ^ 0x80000000u ),
					Value( Word( tangent.Y ) ^ 0x80000000u ), Value( Word( tangent.Z ) ^ 0x80000000u ) );
			}
		}
		var inverseLength = Interpolate( length, RootEstimate( length ) );
		var second = new Vector3(
			Value( NegativeMultiplySubtract( Word( tangent.Y ), Word( normal.Z ), Multiply( Word( tangent.Z ), Word( normal.Y ) ) ) ),
			Value( NegativeMultiplySubtract( Word( tangent.Z ), Word( normal.X ), Multiply( Word( tangent.X ), Word( normal.Z ) ) ) ),
			Value( NegativeMultiplySubtract( Word( tangent.X ), Word( normal.Y ), Multiply( Word( tangent.Y ), Word( normal.X ) ) ) ) );
		return new ContactGeometry( contact.BodyA, contact.BodyB, contact.PointA, contact.PointB,
			normal, Scale( tangent, inverseLength ), Scale( second, inverseLength ),
			contact.StaticFriction, contact.SlidingFriction, contact.Restitution, velocity )
		{
			Materials = contact.Materials
		};
	}

	private static Vector3 PointVelocity( in DynamicBody body, Vector3 point )
	{
		var offset = Difference( point, body.Position );
		var angular = body.AngularVelocity;
		return new Vector3(
			PointComponent( offset.Z, angular.Y, offset.Y, angular.Z, body.Velocity.X ),
			PointComponent( offset.X, angular.Z, offset.Z, angular.X, body.Velocity.Y ),
			PointComponent( offset.Y, angular.X, offset.X, angular.Y, body.Velocity.Z ) );
	}

	private static float PointComponent( float a, float b, float c, float d, float velocity ) =>
		Value( MultiplyAdd( Word( a ), Word( b ), NegativeMultiplySubtract( Word( c ), Word( d ), Word( velocity ) ) ) );

	private static Vector3 CrossFromZero( Vector3 a, Vector3 b ) => new(
		PointComponent( a.Y, b.Z, a.Z, b.Y, 0f ),
		PointComponent( a.Z, b.X, a.X, b.Z, 0f ),
		PointComponent( a.X, b.Y, a.Y, b.X, 0f ) );

	private static Vector3 ProjectAxis( Vector3 axis, Vector3 normal, float component ) => new(
		Value( NegativeMultiplySubtract( Word( normal.X ), Word( component ), Word( axis.X ) ) ),
		Value( NegativeMultiplySubtract( Word( normal.Y ), Word( component ), Word( axis.Y ) ) ),
		Value( NegativeMultiplySubtract( Word( normal.Z ), Word( component ), Word( axis.Z ) ) ) );

	private static Vector3 Difference( Vector3 a, Vector3 b ) => new(
		Value( Subtract( Word( a.X ), Word( b.X ) ) ),
		Value( Subtract( Word( a.Y ), Word( b.Y ) ) ),
		Value( Subtract( Word( a.Z ), Word( b.Z ) ) ) );

	private static Vector3 Scale( Vector3 value, uint scale ) => new(
		Value( Multiply( Word( value.X ), scale ) ),
		Value( Multiply( Word( value.Y ), scale ) ),
		Value( Multiply( Word( value.Z ), scale ) ) );
}
