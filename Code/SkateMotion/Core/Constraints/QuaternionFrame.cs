namespace SkateMotion.Core.Constraints;

using Vector3 = System.Numerics.Vector3;
using Quaternion = System.Numerics.Quaternion;
using Matrix4x4 = System.Numerics.Matrix4x4;

internal readonly record struct QuaternionFrame( Matrix4x4 Axes, Vector3 Error )
{
	internal static Quaternion Compose( Quaternion a, Quaternion b )
	{
		var x = MathF.FusedMultiplyAdd( -b.Y, a.Z, a.Y * b.Z );
		var y = MathF.FusedMultiplyAdd( -b.Z, a.X, a.Z * b.X );
		var z = MathF.FusedMultiplyAdd( -b.X, a.Y, a.X * b.Y );
		x = MathF.FusedMultiplyAdd( b.X, a.W, x );
		y = MathF.FusedMultiplyAdd( b.Y, a.W, y );
		z = MathF.FusedMultiplyAdd( b.Z, a.W, z );
		var dot = (a.X * b.X + a.Y * b.Y) + a.Z * b.Z;
		return new Quaternion(
			MathF.FusedMultiplyAdd( a.X, b.W, x ),
			MathF.FusedMultiplyAdd( a.Y, b.W, y ),
			MathF.FusedMultiplyAdd( a.Z, b.W, z ),
			MathF.FusedMultiplyAdd( a.W, b.W, -dot ) );
	}

	internal static Matrix4x4 Rotation( Quaternion value )
	{
		var x = value.X * 1.4142135381698608f;
		var y = value.Y * 1.4142135381698608f;
		var z = value.Z * 1.4142135381698608f;
		var w = value.W * 1.4142135381698608f;
		var xx = MathF.FusedMultiplyAdd( -x, x, .5f );
		var yy = MathF.FusedMultiplyAdd( -y, y, .5f );
		var zz = MathF.FusedMultiplyAdd( -z, z, .5f );
		var xy = x * y;
		var xz = x * z;
		var yz = y * z;
		var wx = w * x;
		var wy = w * y;
		var wz = w * z;
		return new Matrix4x4(
			yy + zz, xy + wz, xz - wy, 0f,
			xy - wz, xx + zz, yz + wx, 0f,
			xz + wy, yz - wx, xx + yy, 0f, 0f, 0f, 0f, 1f );
	}


	internal static QuaternionFrame Between( Quaternion a, Quaternion b )
	{
		// Round products before the ordered sums. Quaternion multiplication may fuse
		// these operations and create a nonzero error even for identical endpoints.
		var xx = a.X * b.X;
		var xy = a.X * b.Y;
		var xz = a.X * b.Z;
		var xw = a.X * b.W;
		var yx = a.Y * b.X;
		var yy = a.Y * b.Y;
		var yz = a.Y * b.Z;
		var yw = a.Y * b.W;
		var zx = a.Z * b.X;
		var zy = a.Z * b.Y;
		var zz = a.Z * b.Z;
		var zw = a.Z * b.W;
		var wx = a.W * b.X;
		var wy = a.W * b.Y;
		var wz = a.W * b.Z;
		var ww = a.W * b.W;
		return new QuaternionFrame( new Matrix4x4(
			((ww + xx) - yy) - zz, ((yx + xy) + wz) + zw, ((xz + zx) - wy) - yw, 0f,
			((yx + xy) - wz) - zw, ((ww + yy) - zz) - xx, ((yz + zy) + wx) + xw, 0f,
			((xz + zx) + yw) + wy, ((yz + zy) - wx) - xw, ((ww + zz) - xx) - yy, 0f,
			0f, 0f, 0f, 1f ), new Vector3(
			((wx - xw) + zy) - yz, ((wy - yw) + xz) - zx, ((wz - zw) + yx) - xy ) );
	}
}
