using static SkateMotion.Core.Collision.CollisionArithmetic;

namespace SkateMotion.Core.Collision;

internal static partial class CollisionTriangleFilter
{
	private const uint Sign = 0x80000000u;
	private const uint AlignmentThreshold = 0x3f7ff62bu;

	internal static uint Apply( ReadOnlySpan<uint> triangle, Span<uint> normal, bool reverseNormal,
		Span<uint> contacts, TriangleContactFilterContext context )
	{
		if ( triangle.Length < 44 || triangle[ 36 ] != 3 || normal.Length < 4 || contacts.Length < 136 )
			throw new ArgumentException( "Triangle filtering requires a triangle descriptor, a normal and contact storage." );
		if ( contacts[ 132 ] > 16 )
			throw new ArgumentException( "Triangle filtering supports at most sixteen contact pairs." );
		var face = CollisionWordVector.Read( triangle, 4 );
		var direction = CollisionWordVector.Read( normal, 0 );
		if ( reverseNormal )
			direction = Negate( direction );
		var alignment = Dot( direction, face );
		var region = Classify( triangle, direction, face, alignment );
		var flags = triangle[ 37 ];
		var oneSided = (flags & 0x10) != 0;
		if ( (flags & 0x100) == 0 )
		{
			if ( oneSided )
			{
				if ( region == 0 )
					return Greater( alignment, 0 ) ? 1u : 0u;
				if ( Greater( alignment, AlignmentThreshold ) )
					return 1;
				if ( Greater( 0, alignment ) )
					return 0;
			}
			else if ( region == 0 || Greater( alignment & ~Sign, AlignmentThreshold ) )
				return 1;
			return region switch
			{
				1 => flags & 0x20,
				2 => flags & 0x40,
				3 => ((flags >> 10) ^ 1u) & 1u,
				4 => flags & 0x80,
				5 => ((flags >> 9) ^ 1u) & 1u,
				6 => ((flags >> 11) ^ 1u) & 1u,
				_ => 1
			};
		}
		if ( region == 0 )
			return !oneSided || Greater( alignment, 0 ) ? 1u : 0u;
		if ( region is 1 or 2 or 4 )
		{
			var edge = region == 1 ? 0 : region == 2 ? 1 : 2;
			var cosine = triangle[ 38 + edge ];
			var convex = (flags & (0x20u << edge)) != 0;
			if ( !oneSided )
				return Greater( cosine, convex ? alignment : alignment ^ Sign ) ? 0u : 1u;
			return FilterEdge( cosine, convex, face, alignment, normal, reverseNormal, contacts, context );
		}
		// Adjacency slots run opposite to the geometric edge rows.
		var first = region == 3 ? 0 : region == 5 ? 2 : 1;
		var second = region == 3 ? 1 : region == 5 ? 0 : 2;
		var vertexBit = region == 3 ? 0x400u : region == 5 ? 0x200u : 0x800u;
		if ( !oneSided )
		{
			if ( (flags & vertexBit) != 0 )
				return 0;
			return TestTwoSidedVertex( triangle, first, second, direction, face, flags );
		}
		return FilterVertex( triangle, first, second, direction, face, flags,
			(flags & vertexBit) != 0 && !context.PreserveBoundaryNormal, normal, reverseNormal, contacts );
	}

	private static int Classify( ReadOnlySpan<uint> triangle, CollisionWordVector direction,
		CollisionWordVector face, uint alignment )
	{
		if ( !Greater( AlignmentThreshold, alignment & ~Sign ) )
			return 0;
		var tangent = Project( direction, face );
		tangent = Normalize( tangent, 2 );
		var first = Dot( tangent, CollisionWordVector.Read( triangle, 16 ) );
		var second = Dot( tangent, CollisionWordVector.Read( triangle, 20 ) );
		var third = Dot( tangent, CollisionWordVector.Read( triangle, 24 ) );
		const uint threshold = 0x3d4cccccu;
		if ( Greater( first, threshold ) && Greater( threshold ^ Sign, second ) )
			return 6;
		if ( Greater( second, threshold ) && Greater( threshold ^ Sign, third ) )
			return 3;
		if ( Greater( third, threshold ) && Greater( threshold ^ Sign, first ) )
			return 5;
		if ( Greater( first, third ^ Sign ) && !Greater( first, second ^ Sign ) )
			return 4;
		if ( Greater( second, first ^ Sign ) && !Greater( second, third ^ Sign ) )
			return 2;
		if ( Greater( third, second ^ Sign ) && !Greater( third, first ^ Sign ) )
			return 1;
		return 0;
	}

	private static uint FilterEdge( uint cosine, bool convex, CollisionWordVector face, uint alignment,
		Span<uint> normal, bool reverseNormal, Span<uint> contacts, TriangleContactFilterContext context )
	{
		var nearCoplanar = Math.Abs( (double)Value( Subtract( cosine, 0x3f800000u ) ) ) < 0.00001;
		if ( convex || (context.PreserveBoundaryNormal && nearCoplanar) )
			return Greater( cosine, Add( alignment, context.NormalTolerance ) ) ? 0u : 1u;
		if ( Greater( cosine, context.MaximumEdgeCosine ) || !Greater( cosine, 0x3a83126eu ) )
			return 0;
		var squared = Subtract( 0x3f800000u, Multiply( cosine, cosine ) );
		var threshold = Multiply( squared, InverseRoot( squared, 2 ) );
		if ( !Greater( alignment, threshold ) )
			return 0;
		Correct( face, normal, reverseNormal, contacts );
		return 1;
	}

	private static uint TestTwoSidedVertex( ReadOnlySpan<uint> triangle, int first, int second,
		CollisionWordVector direction, CollisionWordVector face, uint flags )
	{
		return TestTwoSidedEdge( triangle, first, direction, face, flags )
			&& TestTwoSidedEdge( triangle, second, direction, face, flags ) ? 1u : 0u;
	}

	private static bool TestTwoSidedEdge( ReadOnlySpan<uint> triangle, int index,
		CollisionWordVector direction, CollisionWordVector face, uint flags )
	{
		var edge = Negate( CollisionWordVector.Read( triangle, 16 + (2 - index) * 4 ) );
		if ( Greater( 0, Dot( direction, edge.Cross( face ) ) ) )
			return true;
		var projected = Normalize( Project( direction, edge ), 2 );
		var comparisonFace = (flags & (0x20u << index)) != 0 ? face : Negate( face );
		return !Greater( triangle[ 38 + index ], Dot( projected, comparisonFace ) );
	}

	private static CollisionWordVector Negate( CollisionWordVector value ) =>
		new( value.X ^ Sign, value.Y ^ Sign, value.Z ^ Sign, value.W ^ Sign );

	private static uint Dot( CollisionWordVector first, CollisionWordVector second ) =>
		Add( Add( Multiply( first.X, second.X ), Multiply( first.Y, second.Y ) ), Multiply( first.Z, second.Z ) );

	private static CollisionWordVector Project( CollisionWordVector value, CollisionWordVector axis )
	{
		var dot = Dot( value, axis );
		return new( NegativeMultiplySubtract( axis.X, dot, value.X ),
			NegativeMultiplySubtract( axis.Y, dot, value.Y ),
			NegativeMultiplySubtract( axis.Z, dot, value.Z ),
			NegativeMultiplySubtract( axis.W, dot, value.W ) );
	}

	private static CollisionWordVector Normalize( CollisionWordVector value, int iterations ) =>
		value.Scale( InverseRoot( value.LengthSquared(), iterations ) );

	private static uint InverseRoot( uint squared, int iterations )
	{
		var estimate = Interpolate( squared, RootEstimate( squared ) );
		for ( var i = 0; i < iterations; ++i )
			estimate = MultiplyAdd( Multiply( estimate, 0x3f000000u ),
				NegativeMultiplySubtract( squared, Multiply( estimate, estimate ), 0x3f800000u ), estimate );
		return estimate;
	}
}
