namespace SkateMotion.Core.Animation;

using Vector4 = System.Numerics.Vector4;
using M = LimbSolverMath;

internal static partial class PhysicalGroundQuerySamples
{
	/// <summary>Consumes the completed retained query requests and publishes their selected support owner.</summary>
	internal static int Update( Span<uint> owner, PhysicalGroundQueryResults results, LimbSolverMath angles )
	{
		if ( owner.Length < PhysicalGroundQuerySelection.OwnerWordCount
			|| owner[ 0x2a40 / 4 ] > 64 || owner[ 0x2a44 / 4 ] > 64 )
			throw new ArgumentException( "A complete retained query owner with valid request counts is required.", nameof( owner ) );
		ArgumentNullException.ThrowIfNull( results );
		ArgumentNullException.ThrowIfNull( angles );
		ResetOutput( owner );
		if ( Byte( owner, 0x13c ) == 0 )
			return -1;
		SetByte( owner, 0x13c, 0 );
		results.CompleteSupportQueries();
		results.CompleteSampleQueries();
		var primary = results.GetSupport( owner[ 0x2994 / 4 ] ).Span;
		if ( !primary.IsEmpty )
		{
			RequireHit( primary, 35 );
			var normal = Read( primary, 32 );
			Append( owner, Read( primary, 0 ), ref normal, 1 );
			AdoptSupport( owner, primary );
			owner[ 0x124 / 4 ] = primary[ 0x88 / 4 ];
			SetByte( owner, 0x299c, 1 );
		}
		AdoptSide( owner, results.GetSupport( owner[ 0x29e4 / 4 ] ).Span, !primary.IsEmpty, 0x10, 0x29ec );
		AdoptSide( owner, results.GetSupport( owner[ 0x2a34 / 4 ] ).Span, !primary.IsEmpty, 0x20, 0x2a3c );
		Span<Vector4> edgePoints = stackalloc Vector4[ 64 ];
		Span<bool> edgeFound = stackalloc bool[ 64 ];
		edgeFound.Clear();
		CollectEdges( owner, results, angles, edgePoints, edgeFound );
		for ( var index = 0; index < owner[ 0x2a44 / 4 ]; index++ )
		{
			var request = 0x1550 + index * 80;
			var hit = results.GetSample( owner[ (request + 68) / 4 ] ).Span;
			if ( hit.IsEmpty && !edgeFound[ index ] )
				continue;
			SetByte( owner, request + 76, 1 );
			Vector4 point;
			Vector4 normal;
			if ( !hit.IsEmpty )
			{
				RequireHit( hit, 8 );
				point = Read( hit, 0 );
				normal = Read( hit, 16 );
				if ( edgeFound[ index ] && edgePoints[ index ].Y > point.Y )
				{
					point = edgePoints[ index ];
					normal = Read( owner, 32 );
				}
			}
			else
			{
				point = edgePoints[ index ];
				normal = Read( owner, 32 );
			}
			Append( owner, point, ref normal, 1 );
		}
		for ( var index = 0; index < owner[ 0x2a40 / 4 ]; index++ )
		{
			var request = 0x150 + index * 80;
			var first = results.GetSample( owner[ (request + 68) / 4 ] );
			var secondId = unchecked((int)owner[ (request + 72) / 4 ]);
			var second = secondId < 0 ? ReadOnlyMemory<uint>.Empty : results.GetSample( (uint)secondId );
			AppendRequestHit( owner, request, first.Span );
			AppendRequestHit( owner, request, second.Span );
		}
		Refine( owner );
		return PhysicalGroundQueryPipeline.Select( owner );
	}

	private static void AppendRequestHit( Span<uint> owner, int request, ReadOnlySpan<uint> hit )
	{
		if ( hit.IsEmpty )
			return;
		RequireHit( hit, 8 );
		SetByte( owner, request + 76, 1 );
		var normal = Read( hit, 16 );
		Append( owner, Read( hit, 0 ), ref normal, 0 );
	}

	private static void ResetOutput( Span<uint> owner )
	{
		// Temporary vector W lanes have no defined initialization. Keep caller storage.
		for ( var index = 0; index < 6; index++ )
		{
			var offset = index < 2 ? 0x70 + index * 16 : 0xb0 + index * 16;
			Set( owner, offset, 0f );
			Set( owner, offset + 4, offset == 0x80 || offset == 0xe0 ? 1f : 0f );
			Set( owner, offset + 8, 0f );
		}
		Write( owner, 0x90, Vector4.UnitX );
		Write( owner, 0xa0, Vector4.UnitY );
		Write( owner, 0xb0, Vector4.UnitZ );
		Write( owner, 0xc0, Vector4.Zero );
		owner[ 0x110 / 4 ] = owner[ 0x114 / 4 ] = owner[ 0x120 / 4 ] = owner[ 0x124 / 4 ] = 0;
		Set( owner, 0x118, 10000000000f );
		Set( owner, 0x11c, 10000000000f );
		owner[ 0x140 / 4 ] = 30;
		owner[ 0x3a50 / 4 ] = owner[ 0x3a54 / 4 ] = owner[ 0x4a60 / 4 ] = 0;
	}

	private static void AdoptSupport( Span<uint> owner, ReadOnlySpan<uint> hit )
	{
		Write( owner, 0x70, Read( hit, 0 ) );
		Write( owner, 0x80, Read( hit, 32 ) );
		hit.Slice( 16, 16 ).CopyTo( owner.Slice( 0x90 / 4, 16 ) );
		var flags = owner[ 0x120 / 4 ];
		if ( Dot( Read( hit, 0 ) - Read( owner, 0 ), Read( owner, 32 ) ) < -0.1f )
		{
			flags |= 9;
			if ( (owner[ 0x76d0 / 4 ] & 0x40) != 0 )
				flags |= 0x40;
		}
		else
			flags = (flags | 1) & ~8u;
		owner[ 0x120 / 4 ] = flags;
	}

	private static void AdoptSide( Span<uint> owner, ReadOnlySpan<uint> hit, bool primary, uint flag, int status )
	{
		if ( hit.IsEmpty )
			return;
		RequireHit( hit, 12 );
		var height = Dot( Read( hit, 0 ) - Read( owner, 0 ), Read( owner, 32 ) );
		if ( !(height < 0.2f && height > -0.2f) )
			return;
		owner[ 0x120 / 4 ] |= flag;
		SetByte( owner, status, 1 );
		if ( primary && Read( hit, 32 ).Y > Read( owner, 0x80 ).Y )
			Write( owner, 0x80, Read( hit, 32 ) );
	}

	private static byte Byte( ReadOnlySpan<uint> words, int offset ) => (byte)(words[ offset / 4 ] >> (24 - (offset & 3) * 8));
	private static void SetByte( Span<uint> words, int offset, byte value )
	{
		var shift = 24 - (offset & 3) * 8;
		words[ offset / 4 ] = (words[ offset / 4 ] & ~(255u << shift)) | ((uint)value << shift);
	}
	private static void RequireHit( ReadOnlySpan<uint> hit, int words )
	{
		if ( hit.Length < words )
			throw new ArgumentException( "The result record is incomplete.", nameof( hit ) );
	}
}

