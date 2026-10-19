namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;
using static LimbSolverMath;

/// <summary>A three-column limb frame that preserves the scale of its perpendicular axes.</summary>
internal readonly record struct LimbFrame( Vector3 X, Vector3 Y, Vector3 Z )
{
	internal Vector3 Transform( Vector3 point, Vector3 translation )
	{
		var result = AddScaled( X, point.X, translation );
		result = AddScaled( Y, point.Y, result );
		return AddScaled( Z, point.Z, result );
	}

	internal LimbFrame Inverse()
	{
		var rowX = Cross( Y, Z );
		var rowY = Cross( Z, X );
		var rowZ = Cross( X, Y );
		var reciprocal = Reciprocal( Dot( X, rowX ) );
		return new LimbFrame(
			Scale( new Vector3( rowX.X, rowY.X, rowZ.X ), reciprocal ),
			Scale( new Vector3( rowX.Y, rowY.Y, rowZ.Y ), reciprocal ),
			Scale( new Vector3( rowX.Z, rowY.Z, rowZ.Z ), reciprocal ) );
	}

	internal LimbFrame Orthonormalized()
	{
		var z = Normalize( Z );
		var y = Normalize( Y - Scale( z, Dot( z, Y ) ) );
		// Both projections use the original X column, preserving classical Gram-Schmidt order.
		var x = X - Scale( z, Dot( z, X ) );
		x -= Scale( y, Dot( y, X ) );
		return new LimbFrame( Normalize( x ), y, z );
	}
}
