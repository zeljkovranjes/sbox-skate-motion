using static SkateMotion.Core.Collision.CollisionArithmetic;

namespace SkateMotion.Core.Collision;

internal static partial class CollisionPairAxes
{
	private static void AppendCylinderSegment( ReadOnlySpan<uint> cylinder, ReadOnlySpan<uint> other,
		Span<uint> axes, ref int count )
	{
		var center = Read( cylinder, 0 );
		var axis = Read( cylinder, 16 );
		var otherCenter = Read( other, 0 );
		var otherAxis = Read( other, 16 );
		var positive = otherAxis.MultiplyAdd( other[ 28 ], otherCenter );
		var negative = otherAxis.MultiplyAdd( other[ 28 ] ^ 0x80000000u, otherCenter );
		AppendCylinderCircleSegment( axis.MultiplyAdd( cylinder[ 28 ], center ), axis,
			cylinder[ 29 ], positive, negative, axes, ref count );
		AppendCylinderCircleSegment( CylinderSubtractProduct( axis, cylinder[ 28 ], center ), CylinderNegate( axis ),
			cylinder[ 29 ], positive, negative, axes, ref count );
		AppendCylinderBoundedSegment( center, axis, otherCenter, otherAxis, 0, other[ 28 ], axes, ref count );
	}

	private static void AppendCylinderCaps( ReadOnlySpan<uint> cylinder, ReadOnlySpan<uint> other,
		Span<uint> axes, ref int count )
	{
		var center = Read( cylinder, 0 );
		var axis = Read( cylinder, 16 );
		var otherCenter = Read( other, 0 );
		var otherAxis = Read( other, 16 );
		var positive = axis.MultiplyAdd( cylinder[ 28 ], center );
		var negativeEndpoint = axis.MultiplyAdd( cylinder[ 28 ] ^ 0x80000000u, center );
		var otherPositive = otherAxis.MultiplyAdd( other[ 28 ], otherCenter );
		var otherNegative = CylinderSubtractProduct( otherAxis, other[ 28 ], otherCenter );
		AppendCylinderCircleSegment( otherPositive, otherAxis, other[ 29 ], positive, negativeEndpoint, axes, ref count );
		AppendCylinderCircleSegment( otherNegative, otherAxis, other[ 29 ], positive, negativeEndpoint, axes, ref count );
		var cross = axis.Cross( otherAxis );
		if ( !Greater( cross.LengthSquared(), 0x007fffffu ) )
			return;
		var seed = cross.Scale( InverseRoot( cross.LengthSquared() ) );
		var negative = CylinderSubtractProduct( axis, cylinder[ 28 ], center );
		AppendCylinderCirclePair( positive, axis, cylinder[ 29 ], otherPositive, otherAxis, other[ 29 ], seed, axes, ref count );
		AppendCylinderCirclePair( negative, CylinderNegate( axis ), cylinder[ 29 ], otherPositive, otherAxis,
			other[ 29 ], CylinderNegate( seed ), axes, ref count );
		AppendCylinderCirclePair( positive, axis, cylinder[ 29 ], otherNegative, CylinderNegate( otherAxis ),
			other[ 29 ], CylinderNegate( seed ), axes, ref count );
		AppendCylinderCirclePair( negative, CylinderNegate( axis ), cylinder[ 29 ], otherNegative,
			CylinderNegate( otherAxis ), other[ 29 ], seed, axes, ref count );
	}

	private static void AppendCylinderCirclePair( CollisionWordVector center, CollisionWordVector axis, uint radius,
		CollisionWordVector otherCenter, CollisionWordVector otherAxis, uint otherRadius, CollisionWordVector seed,
		Span<uint> axes, ref int count )
	{
		Span<uint> inputs = stackalloc uint[ 28 ];
		center.Write( inputs, 0 );
		axis.Write( inputs, 4 );
		inputs.Slice( 8, 4 ).Fill( radius );
		otherCenter.Write( inputs, 12 );
		otherAxis.Write( inputs, 16 );
		inputs.Slice( 20, 4 ).Fill( otherRadius );
		seed.Write( inputs, 24 );
		if ( CollisionCylinderAxes.TryCircleCircleCandidate( inputs, axes.Slice( count * 4, 4 ) ) )
			++count;
	}

	private static void AppendCylinderTriangle( ReadOnlySpan<uint> cylinder, ReadOnlySpan<uint> triangle,
		Span<uint> axes, ref int count )
	{
		var center = Read( cylinder, 0 );
		var axis = Read( cylinder, 16 );
		ReadOnlySpan<int> vertices = stackalloc int[] { 0, 12, 8, 0 };
		for ( var edge = 0; edge < 3; ++edge )
		{
			var first = Read( triangle, vertices[ edge ] );
			var second = Read( triangle, vertices[ edge + 1 ] );
			var direction = second.Subtract( first );
			var projected = direction.Cross( direction.Cross( first.Subtract( center ) ) );
			var oriented = Greater( 0, TriangleDot( axis, projected ) ) ? axis : CylinderNegate( axis );
			AppendCylinderCircleSegment( oriented.MultiplyAdd( cylinder[ 28 ], center ), oriented,
				cylinder[ 29 ], first, second, axes, ref count );
		}
		for ( var edge = 0; edge < 3; ++edge )
		{
			var half = Multiply( triangle[ 28 + edge ], 0x3f000000u );
			AppendCylinderBoundedSegment( center, axis, Read( triangle, vertices[ edge ] ),
				Read( triangle, 16 + edge * 4 ), half, half, axes, ref count );
		}
	}
}
