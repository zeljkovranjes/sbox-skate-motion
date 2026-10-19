using static SkateMotion.Core.Collision.CollisionArithmetic;

namespace SkateMotion.Core.Collision;

internal static partial class CollisionPolygonContacts
{
	private static uint ClosestFeatures( Span<uint> first, Span<uint> second, int vertex, int edge,
		Span<uint> contacts, int firstOffset, int secondOffset )
	{
		var previous = vertex == 0 ? (int)first[ 140 ] - 1 : vertex - 1;
		var secondRecord = 4 + edge * 16;
		var secondTangent = Read( second, secondRecord + 4 );
		var chosen = previous;
		if ( !Parallel( first, previous, secondTangent ) )
		{
			chosen = vertex;
			if ( !Parallel( first, vertex, secondTangent ) )
				return VertexEdge( first, second, vertex, edge, contacts, firstOffset, secondOffset );
		}
		first[ 0 ] = (uint)(chosen * 2 + 2);
		second[ 0 ] = (uint)(edge * 2 + 2);
		var firstRecord = 4 + chosen * 16;
		var a = Read( first, firstRecord );
		var b = Read( second, secondRecord );
		var u = Read( first, firstRecord + 4 );
		var v = secondTangent;
		var lengthA = first[ firstRecord + 12 ];
		var lengthB = second[ secondRecord + 12 ];
		var distance = Dot( u, b.Subtract( a ) );
		if ( Greater( 0x007fffffu, distance ) )
		{
			a.Write( contacts, firstOffset );
			b.Write( contacts, secondOffset );
			--first[ 0 ];
			--second[ 0 ];
			return 1;
		}
		if ( Greater( 0x007fffffu, Subtract( Add( lengthA, lengthB ), distance ) ) )
		{
			u.MultiplyAdd( lengthA, a ).Write( contacts, firstOffset );
			v.MultiplyAdd( lengthB, b ).Write( contacts, secondOffset );
			++first[ 0 ];
			++second[ 0 ];
			return 1;
		}
		var firstRemainder = Subtract( lengthA, distance );
		if ( Greater( 0x007fffffu, firstRemainder ) )
		{
			u.MultiplyAdd( lengthA, a ).Write( contacts, firstOffset );
			SubtractScaled( b, v, firstRemainder ).Write( contacts, secondOffset );
		}
		else
		{
			u.MultiplyAdd( distance, a ).Write( contacts, firstOffset );
			b.Write( contacts, secondOffset );
		}
		var secondRemainder = Subtract( lengthB, distance );
		if ( Greater( 0x007fffffu, secondRemainder ) )
		{
			SubtractScaled( a, u, secondRemainder ).Write( contacts, firstOffset + 4 );
			v.MultiplyAdd( lengthB, b ).Write( contacts, secondOffset + 4 );
		}
		else
		{
			a.Write( contacts, firstOffset + 4 );
			v.MultiplyAdd( distance, b ).Write( contacts, secondOffset + 4 );
		}
		return 2;
	}

	private static uint VertexEdge( Span<uint> first, Span<uint> second, int vertex, int edge,
		Span<uint> contacts, int firstOffset, int secondOffset )
	{
		first[ 0 ] = (uint)(vertex * 2 + 1);
		second[ 0 ] = (uint)(edge * 2 + 2);
		var point = Read( first, 4 + vertex * 16 );
		var record = 4 + edge * 16;
		var start = Read( second, record );
		var tangent = Read( second, record + 4 );
		var along = Dot( tangent, point.Subtract( start ) );
		if ( Greater( 0x007fffffu, along ) )
		{
			along = 0;
			--second[ 0 ];
		}
		else if ( Greater( along, second[ record + 12 ] ) )
		{
			along = second[ record + 12 ];
			++second[ 0 ];
		}
		point.Write( contacts, firstOffset );
		tangent.MultiplyAdd( along, start ).Write( contacts, secondOffset );
		return 1;
	}

	private static bool Parallel( ReadOnlySpan<uint> feature, int edge, CollisionWordVector tangent ) =>
		Greater( 0x3d4cccccu, Dot( Read( feature, 12 + edge * 16 ), tangent ) & 0x7fffffffu );
	private static CollisionWordVector SubtractScaled( CollisionWordVector value, CollisionWordVector axis, uint scale ) => new(
		NegativeMultiplySubtract( axis.X, scale, value.X ), NegativeMultiplySubtract( axis.Y, scale, value.Y ),
		NegativeMultiplySubtract( axis.Z, scale, value.Z ), NegativeMultiplySubtract( axis.W, scale, value.W ) );
}
