using static SkateMotion.Core.Collision.CollisionArithmetic;

namespace SkateMotion.Core.Collision;

internal static partial class CollisionPairAxes
{
	internal static void CylinderPair( ReadOnlySpan<uint> first, ReadOnlySpan<uint> second, Span<uint> output )
	{
		Validate( first, second, output );
		Span<uint> axes = stackalloc uint[ 64 ];
		var count = BuildCylinderCandidates( first, second, axes );
		Span<uint> firstIntervals = stackalloc uint[ 192 ];
		Span<uint> secondIntervals = stackalloc uint[ 192 ];
		CollisionShapeProjection.ProjectBatch( first, axes[ ..(count * 4) ], firstIntervals );
		CollisionShapeProjection.ProjectBatch( second, axes[ ..(count * 4) ], secondIntervals );
		var selected = 0;
		var separation = 0u;
		var flipped = false;
		for ( var i = 0; i < count; ++i )
		{
			var offset = i * 12;
			var reverse = Subtract( firstIntervals[ offset ], secondIntervals[ offset + 4 ] );
			var forward = Subtract( secondIntervals[ offset ], firstIntervals[ offset + 4 ] );
			var flip = Greater( reverse, forward );
			var gap = flip ? reverse : forward;
			if ( i == 0 || Greater( gap, separation ) )
			{
				selected = i;
				separation = gap;
				flipped = flip;
			}
		}
		output[ ..4 ].Fill( separation );
		var normal = Read( axes, selected * 4 );
		(flipped ? CylinderNegate( normal ) : normal).Write( output, 4 );
	}

	internal static void ReversedCylinderPair( ReadOnlySpan<uint> first, ReadOnlySpan<uint> second, Span<uint> output )
	{
		CylinderPair( second, first, output );
		for ( var i = 4; i < 8; ++i )
			output[ i ] ^= 0x80000000u;
	}

	internal static int BuildCylinderCandidates( ReadOnlySpan<uint> cylinder, ReadOnlySpan<uint> other, Span<uint> axes )
	{
		Validate( cylinder, other, axes );
		if ( cylinder[ 36 ] != 5 || axes.Length < 64 )
			throw new ArgumentException( "Cylinder candidates require a cylinder first and sixteen output rows." );
		var count = 0;
		var axis = Read( cylinder, 16 );
		Append( axis, axes, ref count );
		for ( var face = 1; face <= other[ 35 ] >> 24; ++face )
			Append( Read( other, face * 4 ), axes, ref count );
		if ( other[ 36 ] == 1 )
		{
			var cross = axis.Cross( Read( other, 0 ).Subtract( Read( cylinder, 0 ) ) );
			if ( Greater( 0x007fffffu, cross.LengthSquared() ) )
			{
				var basis = Greater( axis.X, 0x3f000000u )
					? new CollisionWordVector( 0, 0x3f800000u, 0, 0 )
					: new CollisionWordVector( 0x3f800000u, 0, 0, 0 );
				cross = axis.Cross( basis );
			}
			Append( axis.Cross( cross.Scale( InverseRoot( cross.LengthSquared() ) ) ), axes, ref count );
			return count;
		}
		if ( other[ 36 ] == 5 )
			AppendCylinderCaps( cylinder, other, axes, ref count );
		if ( ((other[ 35 ] >> 16) & 0xffu) == 1 )
			AppendCylinderSegment( cylinder, other, axes, ref count );
		else if ( other[ 36 ] == 4 )
			AppendCylinderBox( cylinder, other, axes, ref count );
		else if ( other[ 36 ] == 3 )
			AppendCylinderTriangle( cylinder, other, axes, ref count );
		return count;
	}

	private static void AppendCylinderCircleSegment( CollisionWordVector center, CollisionWordVector axis,
		uint radius, CollisionWordVector first, CollisionWordVector second, Span<uint> axes, ref int count )
	{
		Span<uint> inputs = stackalloc uint[ 20 ];
		center.Write( inputs, 0 );
		axis.Write( inputs, 4 );
		inputs.Slice( 8, 4 ).Fill( radius );
		first.Write( inputs, 12 );
		second.Write( inputs, 16 );
		CollisionCylinderAxes.CircleSegmentCandidate( inputs, axes.Slice( count * 4, 4 ) );
		++count;
	}

	private static void AppendCylinderBoundedSegment( CollisionWordVector center, CollisionWordVector axis,
		CollisionWordVector point, CollisionWordVector edge, uint offset, uint halfLength, Span<uint> axes, ref int count )
	{
		Span<uint> inputs = stackalloc uint[ 16 ];
		center.Write( inputs, 0 );
		axis.Write( inputs, 4 );
		point.Write( inputs, 8 );
		edge.Write( inputs, 12 );
		count = CollisionCylinderAxes.AppendSegmentCandidate( inputs, offset, halfLength, axes, count );
	}

	private static CollisionWordVector CylinderNegate( CollisionWordVector value ) =>
		new( value.X ^ 0x80000000u, value.Y ^ 0x80000000u, value.Z ^ 0x80000000u, value.W ^ 0x80000000u );

	private static CollisionWordVector CylinderSubtractProduct( CollisionWordVector axis, uint scale, CollisionWordVector center ) => new(
		NegativeMultiplySubtract( axis.X, scale, center.X ), NegativeMultiplySubtract( axis.Y, scale, center.Y ),
		NegativeMultiplySubtract( axis.Z, scale, center.Z ), NegativeMultiplySubtract( axis.W, scale, center.W ) );

	private static CollisionWordVector CylinderAdd( CollisionWordVector a, CollisionWordVector b ) =>
		new( Add( a.X, b.X ), Add( a.Y, b.Y ), Add( a.Z, b.Z ), Add( a.W, b.W ) );
}
