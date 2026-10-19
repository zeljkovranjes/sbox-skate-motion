namespace SkateMotion.Core.Constraints;

using Matrix4x4 = System.Numerics.Matrix4x4;
using Vector3 = System.Numerics.Vector3;

internal static class GeneratedContactRecord
{
	internal const int WordCount = 64;

	// Identity resolution belongs to the world owner. Cached fields are the producer's snapshot.
	internal static ContactInputs Decode( ReadOnlySpan<uint> words, int bodyA, int bodyB )
	{
		if ( words.Length < WordCount )
			throw new ArgumentException( "A generated contact requires a complete record.", nameof( words ) );
		ArgumentOutOfRangeException.ThrowIfNegative( bodyA );
		ArgumentOutOfRangeException.ThrowIfNegative( bodyB );
		var geometry = new ContactGeometry( bodyA, bodyB, Vector( words, 0 ), Vector( words, 4 ),
			Vector( words, 8 ), Vector( words, 12 ), Vector( words, 16 ),
			Value( words[ 15 ] ), Value( words[ 19 ] ), Value( words[ 11 ] ), Vector( words, 20 ) )
		{
			Materials = words[ 23 ]
		};
		return new ContactInputs( Body( words, 0 ), Body( words, 4 ), geometry );
	}

	private static ContactBody Body( ReadOnlySpan<uint> words, int offset )
	{
		var first = Vector( words, 32 + offset );
		var second = Vector( words, 40 + offset );
		var inertia = new Matrix4x4(
			first.X, first.Y, first.Z, 0f,
			first.Y, second.Y, second.Z, 0f,
			first.Z, second.Z, second.X, 0f,
			0f, 0f, 0f, 1f );
		var flags = unchecked((int)words[ 43 + offset ]);
		return new ContactBody( Vector( words, 24 + offset ), Value( words[ 35 + offset ] ), inertia,
			Vector( words, 48 + offset ), Vector( words, 56 + offset ), (flags & 4) != 0 )
		{
			MotionFlags = flags
		};
	}

	private static float Value( uint word ) => BitConverter.UInt32BitsToSingle( word );

	private static Vector3 Vector( ReadOnlySpan<uint> words, int offset ) =>
		new( Value( words[ offset ] ), Value( words[ offset + 1 ] ), Value( words[ offset + 2 ] ) );
}
