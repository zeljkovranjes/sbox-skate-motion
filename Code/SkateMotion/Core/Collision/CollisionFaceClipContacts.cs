using static SkateMotion.Core.Collision.CollisionArithmetic;

namespace SkateMotion.Core.Collision;

// A successful clipped face pair bypasses the more general separating-edge fallback.
internal static class CollisionFaceClipContacts
{
	internal static bool TryAssemble( ReadOnlySpan<uint> first, ReadOnlySpan<uint> second,
		ReadOnlySpan<uint> direction, Span<uint> contacts )
	{
		if ( first.Length < 144 || second.Length < 144 || direction.Length < 4 || contacts.Length < 136 )
			throw new ArgumentException( "Face clipping requires complete feature, direction and contact buffers." );
		var quadFirst = first[ 140 ] == 4 && second[ 140 ] is 3 or 4;
		if ( !quadFirst && !(first[ 140 ] == 3 && second[ 140 ] == 4) )
			throw new ArgumentException( "Face clipping requires a quadrilateral paired with a triangle or quadrilateral." );
		var quad = quadFirst ? first : second;
		var secondFace = quadFirst ? second : first;
		var firstOffset = quadFirst ? 0 : 64;
		var secondOffset = quadFirst ? 64 : 0;
		var count = 0;
		for ( var firstEdge = 0; firstEdge < 4; ++firstEdge )
		{
			var firstRecord = 4 + firstEdge * 16;
			var firstStart = Read( quad, firstRecord );
			var firstTangent = Read( quad, firstRecord + 4 );
			var firstAuxiliary = Read( quad, firstRecord + 8 );
			for ( var secondEdge = 0; secondEdge < secondFace[ 140 ]; ++secondEdge )
			{
				var secondRecord = 4 + secondEdge * 16;
				var secondStart = Read( secondFace, secondRecord );
				var secondTangent = Read( secondFace, secondRecord + 4 );
				var denominator = Dot( secondTangent, firstAuxiliary );
				var numerator = Dot( firstStart.Subtract( secondStart ), firstAuxiliary );
				var denominatorLimit = Multiply( secondFace[ secondRecord + 12 ], denominator );
				var valid = Greater( Abs( denominator ), 0x34000000u )
					&& !Greater( Abs( numerator ), Abs( denominatorLimit ) )
					&& !Greater( 0, Multiply( numerator, denominator ) );
				var reciprocal = Interpolate( denominator, ReciprocalEstimate( denominator ) );
				reciprocal = MultiplyAdd( reciprocal, NegativeMultiplySubtract( reciprocal, denominator, 0x3f800000u ), reciprocal );
				var point = secondTangent.MultiplyAdd( Multiply( reciprocal, numerator ), secondStart );
				var along = Dot( point.Subtract( firstStart ), firstTangent );
				if ( valid && !Greater( along, quad[ firstRecord + 12 ] ) && !Greater( 0, along ) )
					Append( quad, secondFace, point, contacts, firstOffset, secondOffset, ref count );
			}
		}
		var normal = Read( direction, 0 );
		AppendInterior( quad, secondFace, normal, quad, secondFace, contacts, firstOffset, secondOffset, ref count );
		AppendInterior( secondFace, quad, normal, quad, secondFace, contacts, firstOffset, secondOffset, ref count );
		contacts[ 132 ] = (uint)count;
		return count != 0;
	}

	private static void AppendInterior( ReadOnlySpan<uint> vertices, ReadOnlySpan<uint> boundary,
		CollisionWordVector direction, ReadOnlySpan<uint> first, ReadOnlySpan<uint> second,
		Span<uint> contacts, int firstOffset, int secondOffset, ref int count )
	{
		var planePoint = Read( boundary, 4 );
		for ( var index = 0; index < vertices[ 140 ]; ++index )
		{
			var point = Read( vertices, 4 + index * 16 );
			var distance = OrderedDot( planePoint.Subtract( point ), direction );
			point = direction.MultiplyAdd( distance, point );
			point = new( point.X, point.Y, point.Z, 0x3f800000u );
			var inside = true;
			for ( var edge = 0; edge < boundary[ 140 ]; ++edge )
			{
				var record = 4 + edge * 16;
				var separation = OrderedDot( point.Subtract( Read( boundary, record ) ), Read( boundary, record + 8 ) );
				if ( !Greater( 0, separation ) )
				{
					inside = false;
					break;
				}
			}
			if ( inside )
				Append( first, second, point, contacts, firstOffset, secondOffset, ref count );
		}
	}

	private static void Append( ReadOnlySpan<uint> first, ReadOnlySpan<uint> second, CollisionWordVector point,
		Span<uint> contacts, int firstOffset, int secondOffset, ref int count )
	{
		ProjectPlane( first, point ).Write( contacts, firstOffset + count * 4 );
		ProjectPlane( second, point ).Write( contacts, secondOffset + count * 4 );
		++count;
	}

	private static CollisionWordVector ProjectPlane( ReadOnlySpan<uint> feature, CollisionWordVector point )
	{
		var normal = Read( feature, 132 );
		return normal.MultiplyAdd( Dot( normal, Read( feature, 4 ).Subtract( point ) ), point );
	}

	// The transposed vertex batches accumulate Y, X, then Z with separate multiply-adds.
	private static uint OrderedDot( CollisionWordVector a, CollisionWordVector b ) =>
		MultiplyAdd( a.Z, b.Z, MultiplyAdd( a.X, b.X, Multiply( a.Y, b.Y ) ) );
	private static uint Dot( CollisionWordVector a, CollisionWordVector b ) =>
		Add( Add( Multiply( a.X, b.X ), Multiply( a.Y, b.Y ) ), Multiply( a.Z, b.Z ) );
	private static uint Abs( uint value ) => value & 0x7fffffffu;
	private static CollisionWordVector Read( ReadOnlySpan<uint> buffer, int offset ) => CollisionWordVector.Read( buffer, offset );
}
