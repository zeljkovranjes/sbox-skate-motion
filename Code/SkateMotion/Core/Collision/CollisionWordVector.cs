namespace SkateMotion.Core.Collision;

// Components remain words so exceptional encodings survive vector operations.
internal readonly record struct CollisionWordVector( uint X, uint Y, uint Z, uint W )
{
	internal static CollisionWordVector Read( ReadOnlySpan<uint> words, int offset ) =>
		new( words[ offset ], words[ offset + 1 ], words[ offset + 2 ], words[ offset + 3 ] );

	internal void Write( Span<uint> words, int offset )
	{
		words[ offset ] = X;
		words[ offset + 1 ] = Y;
		words[ offset + 2 ] = Z;
		words[ offset + 3 ] = W;
	}

	internal CollisionWordVector Subtract( CollisionWordVector other ) => new(
		CollisionArithmetic.Subtract( X, other.X ), CollisionArithmetic.Subtract( Y, other.Y ),
		CollisionArithmetic.Subtract( Z, other.Z ), CollisionArithmetic.Subtract( W, other.W ) );

	internal CollisionWordVector Scale( uint scale ) => new(
		CollisionArithmetic.Multiply( X, scale ), CollisionArithmetic.Multiply( Y, scale ),
		CollisionArithmetic.Multiply( Z, scale ), CollisionArithmetic.Multiply( W, scale ) );

	internal CollisionWordVector MultiplyAdd( uint scale, CollisionWordVector addend ) => new(
		CollisionArithmetic.MultiplyAdd( X, scale, addend.X ), CollisionArithmetic.MultiplyAdd( Y, scale, addend.Y ),
		CollisionArithmetic.MultiplyAdd( Z, scale, addend.Z ), CollisionArithmetic.MultiplyAdd( W, scale, addend.W ) );

	internal uint LengthSquared() => CollisionArithmetic.Add(
		CollisionArithmetic.Add( CollisionArithmetic.Multiply( X, X ), CollisionArithmetic.Multiply( Y, Y ) ),
		CollisionArithmetic.Multiply( Z, Z ) );

	internal CollisionWordVector Cross( CollisionWordVector other ) => new(
		CollisionArithmetic.NegativeMultiplySubtract( Z, other.Y, CollisionArithmetic.Multiply( Y, other.Z ) ),
		CollisionArithmetic.NegativeMultiplySubtract( X, other.Z, CollisionArithmetic.Multiply( Z, other.X ) ),
		CollisionArithmetic.NegativeMultiplySubtract( Y, other.X, CollisionArithmetic.Multiply( X, other.Y ) ),
		CollisionArithmetic.NegativeMultiplySubtract( W, other.W, CollisionArithmetic.Multiply( W, other.W ) ) );
}
