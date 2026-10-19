using SkateMotion.Core.Animation;

namespace SkateMotion.Core.Physics;

using Vector4 = System.Numerics.Vector4;

/// <summary>Second moments, first moments and volume retained as a homogeneous tensor.</summary>
internal readonly record struct PhysicalMassMoments( Vector4 X, Vector4 Y, Vector4 Z, Vector4 W )
{
	internal static PhysicalMassMoments FromPrimitive( PhysicalShapeMassProperties properties )
	{
		var inertia = properties.UnitInertia;
		var volume = properties.Volume;
		return new(
			new Vector4( volume * (((inertia.Y + inertia.Z) - inertia.X) * .5f), 0f, 0f, 0f ),
			new Vector4( 0f, volume * (((inertia.X + inertia.Z) - inertia.Y) * .5f), 0f, 0f ),
			new Vector4( 0f, 0f, volume * (((inertia.X + inertia.Y) - inertia.Z) * .5f), 0f ),
			new Vector4( 0f, 0f, 0f, volume ) );
	}

	internal PhysicalMassMoments Add( PhysicalMassMoments other ) => new( X + other.X, Y + other.Y, Z + other.Z, W + other.W );

	internal PhysicalMassMoments Transform( LimbRigidTransform frame )
	{
		var transform = new PhysicalMassMoments( new( frame.Rotation.X, 0f ), new( frame.Rotation.Y, 0f ),
			new( frame.Rotation.Z, 0f ), new( frame.Translation, 1f ) );
		var first = new PhysicalMassMoments( transform.Multiply( X ), transform.Multiply( Y ),
			transform.Multiply( Z ), transform.Multiply( W ) );
		return new(
			first.Multiply( new Vector4( transform.X.X, transform.Y.X, transform.Z.X, transform.W.X ) ),
			first.Multiply( new Vector4( transform.X.Y, transform.Y.Y, transform.Z.Y, transform.W.Y ) ),
			first.Multiply( new Vector4( transform.X.Z, transform.Y.Z, transform.Z.Z, transform.W.Z ) ),
			first.Multiply( new Vector4( transform.X.W, transform.Y.W, transform.Z.W, transform.W.W ) ) );
	}

	internal void WriteInertia( Span<float> matrix )
	{
		if ( matrix.Length != 9 )
			throw new ArgumentException( "Inertia requires nine elements.", nameof( matrix ) );
		matrix[ 0 ] = Y.Y + Z.Z;
		matrix[ 4 ] = X.X + Z.Z;
		matrix[ 8 ] = X.X + Y.Y;
		matrix[ 1 ] = matrix[ 3 ] = (Y.X + X.Y) * -.5f;
		matrix[ 2 ] = matrix[ 6 ] = (Z.X + X.Z) * -.5f;
		matrix[ 5 ] = matrix[ 7 ] = (Z.Y + Y.Z) * -.5f;
	}

	private Vector4 Multiply( Vector4 value )
	{
		var result = MultiplyAdd( X, value.X, Vector4.Zero );
		result = MultiplyAdd( Y, value.Y, result );
		result = MultiplyAdd( Z, value.Z, result );
		return MultiplyAdd( W, value.W, result );
	}

	private static Vector4 MultiplyAdd( Vector4 value, float scale, Vector4 addend ) => new(
		MathF.FusedMultiplyAdd( value.X, scale, addend.X ), MathF.FusedMultiplyAdd( value.Y, scale, addend.Y ),
		MathF.FusedMultiplyAdd( value.Z, scale, addend.Z ), MathF.FusedMultiplyAdd( value.W, scale, addend.W ) );
}
