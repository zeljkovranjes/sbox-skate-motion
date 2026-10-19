using SkateMotion.Core.Constraints;

namespace SkateMotion.Core.Collision;

using Matrix4x4 = System.Numerics.Matrix4x4;

/// <summary>Packs a current owner snapshot into the collision body's eleven raw rows.</summary>
internal static class CollisionBodyCache
{
	internal static void Write( in DynamicBody body, uint motionFlags, Span<uint> destination )
	{
		if ( destination.Length < 44 )
			throw new ArgumentException( "A body cache requires 44 words.", nameof( destination ) );
		var rotation = body.RotationBasis ?? Matrix4x4.CreateFromQuaternion( body.Rotation );
		var inertia = body.WorldInverseInertia;
		Row( destination, 0, body.Rotation.X, body.Rotation.Y, body.Rotation.Z, body.Rotation.W );
		Row( destination, 4, body.Position.X, body.Position.Y, body.Position.Z, 1f );
		Row( destination, 8, body.Velocity.X, body.Velocity.Y, body.Velocity.Z, 0f );
		Row( destination, 12, body.AngularVelocity.X, body.AngularVelocity.Y, body.AngularVelocity.Z, 0f );
		Row( destination, 16, rotation.M11, rotation.M12, rotation.M13, 0f );
		Row( destination, 20, rotation.M21, rotation.M22, rotation.M23, 0f );
		Row( destination, 24, rotation.M31, rotation.M32, rotation.M33, 0f );
		Row( destination, 28, inertia.M11, inertia.M12, inertia.M13, body.InverseMass );
		Row( destination, 32, inertia.M33, inertia.M22, inertia.M23, 0f );
		destination[ 35 ] = motionFlags;
		Row( destination, 36, body.Acceleration.X, body.Acceleration.Y, body.Acceleration.Z, body.MotionMetric );
		Row( destination, 40, body.AngularAcceleration.X, body.AngularAcceleration.Y, body.AngularAcceleration.Z, 0f );
		destination[ 43 ] = unchecked((uint)body.QuietSteps);
	}

	internal static void WriteTransform( in DynamicBody body, Span<uint> destination )
	{
		if ( destination.Length < 16 )
			throw new ArgumentException( "An affine transform requires 16 words.", nameof( destination ) );
		var basis = body.RotationBasis ?? Matrix4x4.CreateFromQuaternion( body.Rotation );
		Row( destination, 0, basis.M11, basis.M12, basis.M13, 0f );
		Row( destination, 4, basis.M21, basis.M22, basis.M23, 0f );
		Row( destination, 8, basis.M31, basis.M32, basis.M33, 0f );
		Row( destination, 12, body.Position.X, body.Position.Y, body.Position.Z, 1f );
	}

	private static void Row( Span<uint> words, int offset, float x, float y, float z, float w )
	{
		words[ offset ] = BitConverter.SingleToUInt32Bits( x );
		words[ offset + 1 ] = BitConverter.SingleToUInt32Bits( y );
		words[ offset + 2 ] = BitConverter.SingleToUInt32Bits( z );
		words[ offset + 3 ] = BitConverter.SingleToUInt32Bits( w );
	}
}
