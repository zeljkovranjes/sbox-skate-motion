using SkateMotion.Core.Animation;

namespace SkateMotion.Core.Collision;

using Vector3 = System.Numerics.Vector3;

/// <summary>Builds swept primitive bounds from the current rigid-body snapshot.</summary>
internal static class CollisionBodyBounds
{
	private static readonly float BoundsStep = BitConverter.UInt32BitsToSingle( 0x3c888889u );
	private static readonly float ExtentScale = BitConverter.UInt32BitsToSingle( 0x3f866666u );

	internal static void Write( int kind, ReadOnlySpan<uint> shape, ReadOnlySpan<uint> transform,
		ReadOnlySpan<uint> body, Span<uint> destination )
	{
		if ( shape.Length < 24 || transform.Length < 16 || body.Length < 44 || destination.Length < 8 )
			throw new ArgumentException( "Bounds require a shape, affine transform, body cache and two output rows." );
		if ( kind != 1 && kind != 2 && kind != 4 && kind != 5 )
			throw new ArgumentOutOfRangeException( nameof( kind ), "Only sphere, capsule, box and cylinder bounds are supported." );
		var center = Transform( Read( shape, 12 ), transform, true );
		var extent = kind switch
		{
			1 => new Vector3( Float( shape[ 20 ] ) ),
			2 => CapsuleExtent( shape, transform ),
			4 => BoxExtent( shape, transform ),
			_ => CylinderExtent( shape, transform )
		};
		var minimum = center - extent;
		var maximum = center + extent;
		var dimension = maximum - minimum;
		var anisotropy = MathF.Max( MathF.Max( MathF.Abs( dimension.Y - dimension.Z ),
			MathF.Abs( dimension.Z - dimension.X ) ), MathF.Abs( dimension.X - dimension.Y ) );
		var angular = Displacement( Read( body, 12 ), Read( body, 40 ) );
		var amount = Multiply( anisotropy, MathF.Min( LimbSolverMath.SquareRoot( LimbSolverMath.Dot( angular, angular ) ), 1f ) );
		var expansion = new Vector3( amount );
		minimum -= expansion;
		maximum += expansion;
		var movement = Displacement( Read( body, 8 ), Read( body, 36 ) );
		minimum = Vector3.Min( minimum, minimum + movement );
		maximum = Vector3.Max( maximum, maximum + movement );
		center = LimbSolverMath.Scale( maximum + minimum, .5f );
		extent = LimbSolverMath.Scale( maximum - center, ExtentScale );
		Write( minimum: center - extent, maximum: center + extent, destination );
	}

	private static Vector3 BoxExtent( ReadOnlySpan<uint> shape, ReadOnlySpan<uint> transform )
	{
		var x = Vector3.Abs( Transform( Read( shape, 0 ), transform, false ) );
		var y = Vector3.Abs( Transform( Read( shape, 4 ), transform, false ) );
		var z = Vector3.Abs( Transform( Read( shape, 8 ), transform, false ) );
		var extent = LimbSolverMath.Scale( y, Float( shape[ 18 ] ) );
		extent = LimbSolverMath.AddScaled( x, Float( shape[ 17 ] ), extent );
		extent = LimbSolverMath.AddScaled( z, Float( shape[ 19 ] ), extent );
		return extent + new Vector3( Float( shape[ 20 ] ) );
	}

	private static Vector3 CapsuleExtent( ReadOnlySpan<uint> shape, ReadOnlySpan<uint> transform )
	{
		var axis = Vector3.Abs( Transform( Read( shape, 8 ), transform, false ) );
		return LimbSolverMath.AddScaled( axis, Float( shape[ 17 ] ), new Vector3( Float( shape[ 20 ] ) ) );
	}

	private static Vector3 CylinderExtent( ReadOnlySpan<uint> shape, ReadOnlySpan<uint> transform )
	{
		var axis = Transform( Read( shape, 8 ), transform, false );
		var length = Float( shape[ 17 ] ) + Float( shape[ 20 ] );
		var radius = Float( shape[ 18 ] ) + Float( shape[ 20 ] );
		return new Vector3( CylinderAxisExtent( axis.X, length, radius ),
			CylinderAxisExtent( axis.Y, length, radius ), CylinderAxisExtent( axis.Z, length, radius ) );
	}

	private static float CylinderAxisExtent( float axis, float length, float radius )
	{
		var perpendicular = MathF.Abs( 1f - Multiply( axis, axis ) );
		var axial = Multiply( length, MathF.Abs( axis ) );
		return LimbSolverMath.MultiplyAdd( LimbSolverMath.SquareRoot( perpendicular ), radius, axial );
	}

	private static Vector3 Displacement( Vector3 velocity, Vector3 acceleration )
	{
		var stepVelocity = LimbSolverMath.Scale( velocity, BoundsStep );
		var stepAcceleration = LimbSolverMath.Scale( LimbSolverMath.Scale( acceleration, BoundsStep ), BoundsStep );
		var gate = Math.Clamp( LimbSolverMath.Dot( stepVelocity, stepAcceleration ), 0f, 1f );
		var gated = LimbSolverMath.Scale( acceleration, gate );
		return stepVelocity + LimbSolverMath.Scale( LimbSolverMath.Scale( gated, BoundsStep ), BoundsStep );
	}

	private static Vector3 Transform( Vector3 value, ReadOnlySpan<uint> transform, bool point )
	{
		var result = LimbSolverMath.AddScaled( Read( transform, 0 ), value.X, point ? Read( transform, 12 ) : Vector3.Zero );
		result = LimbSolverMath.AddScaled( Read( transform, 4 ), value.Y, result );
		return LimbSolverMath.AddScaled( Read( transform, 8 ), value.Z, result );
	}

	private static Vector3 Read( ReadOnlySpan<uint> words, int offset ) =>
		new( Float( words[ offset ] ), Float( words[ offset + 1 ] ), Float( words[ offset + 2 ] ) );

	private static float Float( uint word ) => BitConverter.UInt32BitsToSingle( word );
	private static float Multiply( float a, float b ) => LimbSolverMath.MultiplyAdd( a, b, 0f );

	private static void Write( Vector3 minimum, Vector3 maximum, Span<uint> destination )
	{
		destination[ 0 ] = BitConverter.SingleToUInt32Bits( minimum.X );
		destination[ 1 ] = BitConverter.SingleToUInt32Bits( minimum.Y );
		destination[ 2 ] = BitConverter.SingleToUInt32Bits( minimum.Z );
		destination[ 3 ] = 0;
		destination[ 4 ] = BitConverter.SingleToUInt32Bits( maximum.X );
		destination[ 5 ] = BitConverter.SingleToUInt32Bits( maximum.Y );
		destination[ 6 ] = BitConverter.SingleToUInt32Bits( maximum.Z );
		destination[ 7 ] = 0;
	}
}
