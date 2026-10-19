namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;
using Vector4 = System.Numerics.Vector4;
using M = LimbSolverMath;

/// <summary>Initializes retained ground-query descriptors and transforms their endpoints in query-space metres.</summary>
internal static class PhysicalGroundQueryRequests
{
	internal const int SupportCapacity = 3;
	internal const int SampleCapacity = 38;

	internal static void Initialize( Span<uint> owner )
	{
		RequireOwner( owner );
		Descriptor( owner, 0x2950, new( 0, 1.2f, 0 ), new( 0, -.8f, 0 ), .01f, 0, -1 );
		Descriptor( owner, 0x29a0, new( -.12f, .8f, 0 ), new( -.17f, -.2f, 0 ), .03f, 1, -1 );
		Descriptor( owner, 0x29f0, new( .12f, .8f, 0 ), new( .17f, -.2f, 0 ), .03f, 2, -1 );
		var id = 0;
		var distance = .0375f;
		for ( var index = 0; index < 23; index++ )
		{
			Descriptor( owner, 0x1550 + index * 80, new( 0, .8f, distance ),
				new( 0, -.8f, distance ), 0f, id++, -1 );
			distance += .075f;
		}
		for ( var index = 0; index < 12; index++ )
		{
			var fraction = index * BitConverter.UInt32BitsToSingle( 0x3dba2e8c );
			var height = M.MultiplyAdd( fraction - 1f, .8f, fraction * .8f );
			var reverse = height < 0f;
			Descriptor( owner, 0x150 + index * 80, new( 0, height, 0 ),
				new( 0, height, 1.8f ), 0f, id, reverse ? id + 1 : -1 );
			id += reverse ? 2 : 1;
		}
		var radius = BitConverter.UInt32BitsToSingle( 0x3dcccccc );
		for ( var index = 0; index < 3; index++ )
		{
			var height = M.MultiplyAdd( index + index, radius, radius ) + .8f;
			Descriptor( owner, 0x150 + (12 + index) * 80, new( 0, height, radius ),
				new( 0, height, BitConverter.UInt32BitsToSingle( 0x3fd99999 ) ), radius, id++, -1 );
		}
		owner[ 0x2a40 / 4 ] = 15;
		owner[ 0x2a44 / 4 ] = 23;
	}

	internal static void Transform( Span<uint> owner, ReadOnlySpan<uint> frame )
	{
		RequireOwner( owner );
		if ( frame.Length != 28 || owner[ 0x2a40 / 4 ] > 64 || owner[ 0x2a44 / 4 ] > 64 )
			throw new ArgumentException( "Seven frame vectors and valid retained descriptor counts are required." );
		frame.CopyTo( owner );
		owner[ 0x140 / 4 ] = 0;
		var position = Read( owner, 0 );
		var forward = Read( owner, 16 );
		var up = Read( owner, 32 );
		var side = Read( owner, 48 );
		TransformDescriptor( owner, 0x2950, position, side, up, forward );
		var alternateUp = Read( owner, 80 );
		var alternateSide = Read( owner, 96 );
		var alternateForward = Cross( alternateSide, alternateUp );
		TransformDescriptor( owner, 0x29a0, position, alternateSide, alternateUp, alternateForward );
		TransformDescriptor( owner, 0x29f0, position, alternateSide, alternateUp, alternateForward );
		for ( var index = 0; index < owner[ 0x2a44 / 4 ]; index++ )
			TransformDescriptor( owner, 0x1550 + index * 80, position, side, up, forward );
		for ( var index = 0; index < owner[ 0x2a40 / 4 ]; index++ )
			TransformDescriptor( owner, 0x150 + index * 80, position, side, up, forward );
	}

	internal static Vector4 Read( ReadOnlySpan<uint> words, int offset ) => new(
		Float( words, offset ), Float( words, offset + 4 ), Float( words, offset + 8 ), Float( words, offset + 12 ) );

	internal static float Float( ReadOnlySpan<uint> words, int offset ) => BitConverter.UInt32BitsToSingle( words[ offset / 4 ] );

	private static void RequireOwner( ReadOnlySpan<uint> owner )
	{
		if ( owner.Length < PhysicalGroundQuerySelection.OwnerWordCount )
			throw new ArgumentException( "A complete retained ground-query owner is required.", nameof( owner ) );
	}

	private static void Descriptor( Span<uint> owner, int offset, Vector3 start, Vector3 end, float radius, int id, int reverse )
	{
		// Descriptor W lanes and the final three status bytes are caller-owned storage.
		WriteXyz( owner, offset, start );
		WriteXyz( owner, offset + 16, end );
		WriteXyz( owner, offset + 32, Vector3.Zero );
		WriteXyz( owner, offset + 48, Vector3.Zero );
		owner[ (offset + 64) / 4 ] = BitConverter.SingleToUInt32Bits( radius );
		owner[ (offset + 68) / 4 ] = (uint)id;
		owner[ (offset + 72) / 4 ] = unchecked((uint)reverse);
		owner[ (offset + 76) / 4 ] &= 0x00ffffff;
	}

	private static void TransformDescriptor( Span<uint> owner, int offset, Vector4 position,
		Vector4 side, Vector4 up, Vector4 forward )
	{
		for ( var endpoint = 0; endpoint < 2; endpoint++ )
		{
			var local = Read( owner, offset + endpoint * 16 );
			var value = AddScaled( forward, local.Z, AddScaled( up, local.Y, AddScaled( side, local.X, position ) ) );
			var target = (offset + 32 + endpoint * 16) / 4;
			owner[ target ] = BitConverter.SingleToUInt32Bits( value.X );
			owner[ target + 1 ] = BitConverter.SingleToUInt32Bits( value.Y );
			owner[ target + 2 ] = BitConverter.SingleToUInt32Bits( value.Z );
			owner[ target + 3 ] = BitConverter.SingleToUInt32Bits( value.W );
		}
		owner[ (offset + 76) / 4 ] &= 0x00ffffff;
	}

	private static Vector4 AddScaled( Vector4 value, float scale, Vector4 add ) => new(
		M.MultiplyAdd( value.X, scale, add.X ), M.MultiplyAdd( value.Y, scale, add.Y ),
		M.MultiplyAdd( value.Z, scale, add.Z ), M.MultiplyAdd( value.W, scale, add.W ) );

	private static Vector4 Cross( Vector4 a, Vector4 b ) => new(
		M.SubtractProduct( b.Y, a.Z, M.MultiplyAdd( a.Y, b.Z, 0f ) ),
		M.SubtractProduct( b.Z, a.X, M.MultiplyAdd( a.Z, b.X, 0f ) ),
		M.SubtractProduct( b.X, a.Y, M.MultiplyAdd( a.X, b.Y, 0f ) ),
		M.SubtractProduct( b.W, a.W, M.MultiplyAdd( a.W, b.W, 0f ) ) );

	private static void WriteXyz( Span<uint> owner, int offset, Vector3 value )
	{
		owner[ offset / 4 ] = BitConverter.SingleToUInt32Bits( value.X );
		owner[ offset / 4 + 1 ] = BitConverter.SingleToUInt32Bits( value.Y );
		owner[ offset / 4 + 2 ] = BitConverter.SingleToUInt32Bits( value.Z );
	}
}
