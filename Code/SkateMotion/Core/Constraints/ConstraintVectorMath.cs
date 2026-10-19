namespace SkateMotion.Core.Constraints;

using Vector3 = System.Numerics.Vector3;
using Matrix4x4 = System.Numerics.Matrix4x4;

/// <summary>Ordered vector operations used when compiling and solving constraints.</summary>
internal static class ConstraintVectorMath
{
	internal static Vector3 Cross( Vector3 a, Vector3 b ) => new(
		MathF.FusedMultiplyAdd( -b.Y, a.Z, a.Y * b.Z ),
		MathF.FusedMultiplyAdd( -b.Z, a.X, a.Z * b.X ),
		MathF.FusedMultiplyAdd( -b.X, a.Y, a.X * b.Y ) );

	internal static Vector3 Transform( Vector3 value, Matrix4x4 matrix ) => new(
		MathF.FusedMultiplyAdd( matrix.M31, value.Z, MathF.FusedMultiplyAdd( matrix.M21, value.Y, matrix.M11 * value.X ) ),
		MathF.FusedMultiplyAdd( matrix.M32, value.Z, MathF.FusedMultiplyAdd( matrix.M22, value.Y, matrix.M12 * value.X ) ),
		MathF.FusedMultiplyAdd( matrix.M33, value.Z, MathF.FusedMultiplyAdd( matrix.M23, value.Y, matrix.M13 * value.X ) ) );

	internal static Vector3 Project( Vector3 value, Matrix4x4 axes ) => new(
		(value.X * axes.M11 + value.Y * axes.M12) + value.Z * axes.M13,
		(value.X * axes.M21 + value.Y * axes.M22) + value.Z * axes.M23,
		(value.X * axes.M31 + value.Y * axes.M32) + value.Z * axes.M33 );

	// Pair solving accumulates the positive cross term first, then subtracts the negative term.
	internal static Vector3 CrossAdd( Vector3 a, Vector3 b, Vector3 addend ) => new(
		MathF.FusedMultiplyAdd( -a.Z, b.Y, MathF.FusedMultiplyAdd( a.Y, b.Z, addend.X ) ),
		MathF.FusedMultiplyAdd( -a.X, b.Z, MathF.FusedMultiplyAdd( a.Z, b.X, addend.Y ) ),
		MathF.FusedMultiplyAdd( -a.Y, b.X, MathF.FusedMultiplyAdd( a.X, b.Y, addend.Z ) ) );

	internal static Vector3 TransformAdd( Vector3 value, Matrix4x4 matrix, Vector3 addend ) => new(
		MathF.FusedMultiplyAdd( matrix.M31, value.Z, MathF.FusedMultiplyAdd( matrix.M21, value.Y, MathF.FusedMultiplyAdd( matrix.M11, value.X, addend.X ) ) ),
		MathF.FusedMultiplyAdd( matrix.M32, value.Z, MathF.FusedMultiplyAdd( matrix.M22, value.Y, MathF.FusedMultiplyAdd( matrix.M12, value.X, addend.Y ) ) ),
		MathF.FusedMultiplyAdd( matrix.M33, value.Z, MathF.FusedMultiplyAdd( matrix.M23, value.Y, MathF.FusedMultiplyAdd( matrix.M13, value.X, addend.Z ) ) ) );

	internal static Vector3 MultiplyAdd( Vector3 value, float scale, Vector3 addend ) => new(
		MathF.FusedMultiplyAdd( value.X, scale, addend.X ),
		MathF.FusedMultiplyAdd( value.Y, scale, addend.Y ),
		MathF.FusedMultiplyAdd( value.Z, scale, addend.Z ) );
}
