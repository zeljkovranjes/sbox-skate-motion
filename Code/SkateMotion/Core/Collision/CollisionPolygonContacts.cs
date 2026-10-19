using static SkateMotion.Core.Collision.CollisionArithmetic;

namespace SkateMotion.Core.Collision;

internal static partial class CollisionPolygonContacts
{
	internal static void Assemble( Span<uint> first, Span<uint> second, Span<uint> contacts )
	{
		// Degenerate boundaries can generate more pairs than the fixed contact layout holds.
		// Keep the caller's buffer intact if those inputs exceed its capacity.
		Span<uint> pending = stackalloc uint[ 136 ];
		contacts.Slice( 0, 136 ).CopyTo( pending );
		AssemblePending( first, second, pending );
		pending.CopyTo( contacts );
	}

	private static void AssemblePending( Span<uint> first, Span<uint> second, Span<uint> contacts )
	{
		var firstCount = (int)first[ 140 ];
		var secondCount = (int)second[ 140 ];
		Span<uint> crossings = stackalloc uint[ 8 ];
		crossings.Clear();
		contacts[ 132 ] = 0;
		var bestMinimum = 0xff7fffffu;
		var selectedVertex = 0;
		var selectedEdge = 0;
		var selectedSide = 0;
		for ( var edge = 0; edge < firstCount; ++edge )
		{
			var start = Read( first, 4 + edge * 16 );
			var auxiliary = Read( first, 12 + edge * 16 );
			var previousIndex = secondCount - 1;
			var previous = Dot( auxiliary, Read( second, 4 + previousIndex * 16 ).Subtract( start ) );
			var minimum = previous;
			var minimumIndex = previousIndex;
			for ( var vertex = 0; vertex < secondCount; ++vertex )
			{
				var distance = Dot( auxiliary, Read( second, 4 + vertex * 16 ).Subtract( start ) );
				if ( Greater( minimum, distance ) )
				{
					minimum = distance;
					minimumIndex = vertex;
				}
				if ( Greater( distance, 0 ) )
					crossings[ vertex ] |= 0x20000000u;
				if ( !Greater( Multiply( distance, previous ), 0 ) )
					crossings[ previousIndex ] |= 1u << edge;
				previous = distance;
				previousIndex = vertex;
			}
			if ( Greater( minimum, bestMinimum ) )
			{
				bestMinimum = minimum;
				selectedVertex = minimumIndex;
				selectedEdge = edge;
				selectedSide = 1;
			}
		}
		for ( var vertex = 0; vertex < secondCount; ++vertex )
		{
			if ( (crossings[ vertex ] & 0x20000000u) != 0 )
				continue;
			var point = Read( second, 4 + vertex * 16 );
			Append( ProjectPlane( first, point ), point, contacts );
		}
		if ( contacts[ 132 ] == secondCount )
			return;
		for ( var edge = 0; edge < secondCount; ++edge )
		{
			var start = Read( second, 4 + edge * 16 );
			var auxiliary = Read( second, 12 + edge * 16 );
			var previousIndex = firstCount - 1;
			var previous = Dot( auxiliary, Read( first, 4 + previousIndex * 16 ).Subtract( start ) );
			var minimum = previous;
			var minimumIndex = previousIndex;
			for ( var vertex = 0; vertex < firstCount; ++vertex )
			{
				var distance = Dot( auxiliary, Read( first, 4 + vertex * 16 ).Subtract( start ) );
				if ( Greater( minimum, distance ) )
				{
					minimum = distance;
					minimumIndex = vertex;
				}
				if ( Greater( distance, 0 ) )
					crossings[ vertex ] |= 0x40000000u;
				var difference = Subtract( previous, distance );
				if ( !Greater( Multiply( distance, previous ), 0 )
					&& (crossings[ edge ] & (1u << previousIndex)) != 0
					&& Greater( difference & 0x7fffffffu, 0x007fffffu ) )
				{
					var record = 4 + previousIndex * 16;
					var along = Multiply( Reciprocal( difference ), Multiply( first[ record + 12 ], previous ) );
					var point = Read( first, record + 4 ).MultiplyAdd( along, Read( first, record ) );
					Append( point, ProjectPlane( second, point ), contacts );
				}
				previous = distance;
				previousIndex = vertex;
			}
			if ( Greater( minimum, bestMinimum ) )
			{
				bestMinimum = minimum;
				selectedVertex = minimumIndex;
				selectedEdge = edge;
				selectedSide = 2;
			}
		}
		for ( var vertex = 0; vertex < firstCount; ++vertex )
		{
			if ( (crossings[ vertex ] & 0x40000000u) != 0 )
				continue;
			var point = Read( first, 4 + vertex * 16 );
			Append( point, ProjectPlane( second, point ), contacts );
		}
		if ( contacts[ 132 ] != 0 )
			return;
		contacts[ 132 ] = selectedSide == 1
			? ClosestFeatures( second, first, selectedVertex, selectedEdge, contacts, 64, 0 )
			: ClosestFeatures( first, second, selectedVertex, selectedEdge, contacts, 0, 64 );
		var delta = Read( contacts, 64 ).Subtract( Read( contacts, 0 ) );
		var squared = delta.LengthSquared();
		var length = Multiply( squared, InverseRoot( squared ) );
		if ( Greater( length, 0x007fffffu ) )
		{
			contacts[ 133 ] = 1;
			delta.Scale( Reciprocal( length ) ).Write( contacts, 128 );
		}
	}

	private static void Append( CollisionWordVector first, CollisionWordVector second, Span<uint> contacts )
	{
		if ( contacts[ 132 ] >= 16 )
			throw new ArgumentException( "Polygon contacts exceed the fixed capacity of sixteen pairs." );
		var index = (int)contacts[ 132 ] * 4;
		first.Write( contacts, index );
		second.Write( contacts, 64 + index );
		++contacts[ 132 ];
	}

	private static CollisionWordVector ProjectPlane( ReadOnlySpan<uint> feature, CollisionWordVector point )
	{
		var normal = Read( feature, 132 );
		return normal.MultiplyAdd( Dot( normal, Read( feature, 4 ).Subtract( point ) ), point );
	}

	private static uint Reciprocal( uint value )
	{
		var estimate = Interpolate( value, ReciprocalEstimate( value ) );
		var initial = Multiply( 0x3f800000u, estimate );
		return MultiplyAdd( NegativeMultiplySubtract( initial, value, 0x3f800000u ), estimate, initial );
	}

	private static uint InverseRoot( uint squared )
	{
		var estimate = Interpolate( squared, RootEstimate( squared ) );
		for ( var i = 0; i < 2; ++i )
			estimate = MultiplyAdd( Multiply( estimate, 0x3f000000u ),
				NegativeMultiplySubtract( squared, Multiply( estimate, estimate ), 0x3f800000u ), estimate );
		return estimate;
	}

	private static CollisionWordVector Read( ReadOnlySpan<uint> buffer, int offset ) => CollisionWordVector.Read( buffer, offset );
	private static uint Dot( CollisionWordVector a, CollisionWordVector b ) =>
		Add( Add( Multiply( a.X, b.X ), Multiply( a.Y, b.Y ) ), Multiply( a.Z, b.Z ) );
}
