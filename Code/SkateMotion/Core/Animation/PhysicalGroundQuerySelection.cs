namespace SkateMotion.Core.Animation;

using Vector4 = System.Numerics.Vector4;
using M = LimbSolverMath;

internal static partial class PhysicalGroundQuerySelection
{
	internal const int OwnerWordCount = 0x7708 / 4;

	/// <summary>Selects and publishes the final ground record from already ordered candidates and retained query state.</summary>
	/// <remarks>Candidate ordering is ascending float at byte 60; callers preserve the producer's tie order.</remarks>
	internal static int SelectOrdered( Span<uint> owner )
	{
		Validate( owner );
		SetFloat( owner, 0x118, 0f );
		Write( owner, 0xf0, Read( owner, 0 ) );
		Write( owner, 0x100, Read( owner, 0x20 ) );
		if ( owner[ 0x6290 / 4 ] != 0 )
			PrepareSegmentResponse( owner );

		var selected = SelectCandidate( owner );
		if ( selected < 0 )
		{
			if ( Float( owner, 0x11c ) < Value( 0x3e99999a ) )
				owner[ 0x120 / 4 ] |= 0x400;
			else
				owner[ 0x120 / 4 ] &= ~1u;
			return -1;
		}

		var candidate = 0x62a0 + selected * 80;
		owner.Slice( candidate / 4, 18 ).CopyTo( owner.Slice( 0x76a0 / 4, 18 ) );
		Write( owner, 0xd0, Read( owner, candidate ) );
		Write( owner, 0xe0, Read( owner, candidate + 16 ) );
		owner[ 0x120 / 4 ] |= 2u | owner[ (candidate + 48) / 4 ];
		if ( (owner[ 0x120 / 4 ] & 1u) == 0 )
		{
			owner[ 0x120 / 4 ] |= 0x39;
			Write( owner, 0x70, Read( owner, 0 ) );
			Write( owner, 0x80, Read( owner, 0x20 ) );
		}
		UpdateSlopeHistory( owner, Read( owner, candidate + 32 ) );
		return selected;
	}

	private static int SelectCandidate( ReadOnlySpan<uint> owner )
	{
		var count = (int)owner[ 0x76f0 / 4 ];
		var position = Read( owner, 0 );
		var axis = Read( owner, 16 );
		var velocity = Read( owner, 64 );
		var reach = M.MultiplyAdd( M.SquareRoot( Dot( velocity, velocity ) ), Value( 0x3c888889 ), 0f );
		for ( var index = 0; index < count; index++ )
		{
			var offset = 0x62a0 + index * 80;
			var skip = index < count - 1 && reach > Dot( Read( owner, offset ) - position, axis );
			if ( owner[ (offset + 68) / 4 ] != 1 )
			{
				if ( !skip )
					return index;
				continue;
			}

			var next = index + 1;
			if ( next >= count )
				return index;
			if ( !skip )
			{
				for ( ; next < count; next++ )
				{
					var following = 0x62a0 + next * 80;
					if ( Float( owner, following + 52 ) > Float( owner, offset + 56 ) )
						return index;
					var kind = owner[ (following + 68) / 4 ];
					if ( kind is 4 or 7 or 8 )
						break;
				}
			}
			for ( ; next < count; next++ )
			{
				if ( owner[ (0x62a0 + next * 80 + 64) / 4 ] == owner[ (offset + 64) / 4 ] )
					return next;
			}
			return index;
		}
		return -1;
	}

	private static void PrepareSegmentResponse( Span<uint> owner )
	{
		var position = Read( owner, 0 );
		var axis = Read( owner, 16 );
		var segmentCount = (int)owner[ 0x6294 / 4 ];
		var lastNeutralEnd = (int)owner[ 0x6290 / 4 ];
		var reach = Dot( Read( owner, 0x4a90 + (lastNeutralEnd - 1) * 96 + 32 ) - position, axis );
		SetFloat( owner, 0x118, reach >= Value( 0x3fd33333 ) ? Value( 0x501502f9 ) : reach );
		for ( var index = 0; index < segmentCount; index++ )
		{
			var offset = 0x4a90 + index * 96;
			var category = owner[ offset / 4 ];
			if ( category is 1 or 2 && Float( owner, offset + 80 ) > Value( 0x3d4ccccd ) )
			{
				SetFloat( owner, 0x118, Dot( Read( owner, offset + 16 ) - position, axis ) );
				break;
			}
		}

		if ( (owner[ 0x120 / 4 ] & 1u) != 0 && axis.Y > Value( 0x3e4ccccd )
			&& Read( owner, 0x80 ).Y < Value( 0x3f333333 ) )
		{
			MarkOverhead( owner, 0x2a50, (int)owner[ 0x3a54 / 4 ] );
			MarkOverhead( owner, 0x3a60, (int)owner[ 0x4a60 / 4 ] );
		}

		var velocity = Read( owner, 64 );
		var remaining = M.MultiplyAdd( M.SquareRoot( Dot( velocity, velocity ) ), .25f, 0f );
		for ( var index = 0; index < segmentCount; index++ )
		{
			var offset = 0x4a90 + index * 96;
			var normal = Read( owner, offset + 64 );
			if ( owner[ offset / 4 ] != 0 && !(normal.Y > Value( 0x3f666666 )) )
				continue;
			Write( owner, 0x100, normal );
			remaining -= Float( owner, offset + 80 );
			if ( remaining <= 0f )
			{
				Write( owner, 0xf0, AddScaled( Read( owner, offset + 48 ), remaining, Read( owner, offset + 32 ) ) );
				break;
			}
			Write( owner, 0xf0, Read( owner, offset + 32 ) );
			owner[ 0x120 / 4 ] |= 4;
		}
	}

	private static void MarkOverhead( Span<uint> owner, int candidates, int count )
	{
		for ( var index = 0; index < count; index++ )
		{
			var offset = candidates + index * 64;
			if ( !(Read( owner, offset + 16 ).Y > Value( 0x3f733333 )) && (owner[ (offset + 48) / 4 ] & 16u) == 0 )
				continue;
			if ( Dot( Read( owner, offset ) - Read( owner, 0 ), Read( owner, 32 ) ) > 0f )
				owner[ 0x120 / 4 ] |= 0x200;
			break;
		}
	}

	private static void Validate( ReadOnlySpan<uint> owner )
	{
		if ( owner.Length < OwnerWordCount )
			throw new ArgumentException( "The complete retained query owner is required.", nameof( owner ) );
		if ( owner[ 0x3a54 / 4 ] > 64 || owner[ 0x4a60 / 4 ] > 64 || owner[ 0x6290 / 4 ] > 64
			|| owner[ 0x6294 / 4 ] > 64 || owner[ 0x76f0 / 4 ] > 64 || owner[ 0x7704 / 4 ] > 2 )
			throw new ArgumentException( "A query count or history index exceeds its backing storage.", nameof( owner ) );
		if ( owner[ 0x6290 / 4 ] > owner[ 0x6294 / 4 ] )
			throw new ArgumentException( "The neutral segment end exceeds the segment count.", nameof( owner ) );
	}

	private static float Value( uint word ) => BitConverter.UInt32BitsToSingle( word );
	private static float Float( ReadOnlySpan<uint> words, int offset ) => Value( words[ offset / 4 ] );
	private static void SetFloat( Span<uint> words, int offset, float value ) => words[ offset / 4 ] = BitConverter.SingleToUInt32Bits( value );
	private static Vector4 Read( ReadOnlySpan<uint> words, int offset ) => new(
		Float( words, offset ), Float( words, offset + 4 ), Float( words, offset + 8 ), Float( words, offset + 12 ) );
	private static void Write( Span<uint> words, int offset, Vector4 value )
	{
		SetFloat( words, offset, value.X );
		SetFloat( words, offset + 4, value.Y );
		SetFloat( words, offset + 8, value.Z );
		SetFloat( words, offset + 12, value.W );
	}
	private static float Dot( Vector4 left, Vector4 right ) =>
		(M.MultiplyAdd( left.X, right.X, 0f ) + M.MultiplyAdd( left.Y, right.Y, 0f )) + M.MultiplyAdd( left.Z, right.Z, 0f );
	private static Vector4 AddScaled( Vector4 value, float scale, Vector4 add ) => new(
		M.MultiplyAdd( value.X, scale, add.X ), M.MultiplyAdd( value.Y, scale, add.Y ),
		M.MultiplyAdd( value.Z, scale, add.Z ), M.MultiplyAdd( value.W, scale, add.W ) );
}
