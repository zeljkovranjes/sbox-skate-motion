namespace SkateMotion.Core.Collision;

using Vector3 = System.Numerics.Vector3;
using Matrix4x4 = System.Numerics.Matrix4x4;
using static SkateMotion.Core.Animation.LimbSolverMath;

/// <summary>Ordered affine operations shared by mesh preparation and triangle decoding.</summary>
internal static class MeshQueryMath
{
	internal static Vector3 TransformPoint( Vector3 point, Matrix4x4 transform )
	{
		var result = AddScaled( new( transform.M11, transform.M12, transform.M13 ), point.X,
			new( transform.M41, transform.M42, transform.M43 ) );
		result = AddScaled( new( transform.M21, transform.M22, transform.M23 ), point.Y, result );
		return AddScaled( new( transform.M31, transform.M32, transform.M33 ), point.Z, result );
	}

	internal static Vector3 TransformExtent( Vector3 extent, Matrix4x4 inverse )
	{
		var result = Scale( Vector3.Abs( new( inverse.M21, inverse.M22, inverse.M23 ) ), extent.Y );
		result = AddScaled( Vector3.Abs( new( inverse.M11, inverse.M12, inverse.M13 ) ), extent.X, result );
		return AddScaled( Vector3.Abs( new( inverse.M31, inverse.M32, inverse.M33 ) ), extent.Z, result );
	}

	internal static bool Overlaps( Vector3 aMinimum, Vector3 aMaximum, Vector3 bMinimum, Vector3 bMaximum ) =>
		!(aMinimum.X > bMaximum.X || aMinimum.Y > bMaximum.Y || aMinimum.Z > bMaximum.Z
		|| bMinimum.X > aMaximum.X || bMinimum.Y > aMaximum.Y || bMinimum.Z > aMaximum.Z);

	internal static Vector3 Minimum( Vector3 a, Vector3 b ) => new(
		a.X < b.X ? a.X : b.X, a.Y < b.Y ? a.Y : b.Y, a.Z < b.Z ? a.Z : b.Z );

	internal static Vector3 Maximum( Vector3 a, Vector3 b ) => new(
		a.X > b.X ? a.X : b.X, a.Y > b.Y ? a.Y : b.Y, a.Z > b.Z ? a.Z : b.Z );
}
