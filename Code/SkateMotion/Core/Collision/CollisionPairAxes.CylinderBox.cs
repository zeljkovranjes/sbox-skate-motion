using static SkateMotion.Core.Collision.CollisionArithmetic;

namespace SkateMotion.Core.Collision;

internal static partial class CollisionPairAxes
{
	private static void AppendCylinderBox( ReadOnlySpan<uint> cylinder, ReadOnlySpan<uint> box,
		Span<uint> axes, ref int count )
	{
		var center = Read( cylinder, 0 );
		var axis = Read( cylinder, 16 );
		AppendCylinderBoxCap( axis.MultiplyAdd( cylinder[ 28 ], center ), axis, cylinder[ 29 ], box, axes, ref count );
		AppendCylinderBoxCap( CylinderSubtractProduct( axis, cylinder[ 28 ], center ), axis,
			cylinder[ 29 ], box, axes, ref count );
		var x = Read( box, 16 );
		var y = Read( box, 20 );
		var z = Read( box, 24 );
		var sx = SignedBoxExtent( axis, x, box[ 28 ] );
		var sy = SignedBoxExtent( axis, y, box[ 29 ] );
		var sz = SignedBoxExtent( axis, z, box[ 30 ] );
		var nx = sx ^ 0x80000000u;
		var ny = sy ^ 0x80000000u;
		var nz = sz ^ 0x80000000u;
		var boxCenter = Read( box, 0 );
		var first = CylinderAdd( z.MultiplyAdd( sz, x.MultiplyAdd( nx, y.Scale( sy ) ) ), boxCenter );
		AppendCylinderBoundedSegment( center, axis, first, y, ny, box[ 29 ], axes, ref count );
		AppendCylinderBoundedSegment( center, axis, first, z, nz, box[ 30 ], axes, ref count );
		var second = CylinderAdd( z.MultiplyAdd( sz, x.MultiplyAdd( sx, CylinderNegate( y.Scale( sy ) ) ) ), boxCenter );
		AppendCylinderBoundedSegment( center, axis, second, z, nz, box[ 30 ], axes, ref count );
		AppendCylinderBoundedSegment( center, axis, second, x, nx, box[ 28 ], axes, ref count );
		var third = CylinderAdd( CylinderSubtractProduct( z, sz, x.MultiplyAdd( sx, y.Scale( sy ) ) ), boxCenter );
		AppendCylinderBoundedSegment( center, axis, third, x, nx, box[ 28 ], axes, ref count );
		AppendCylinderBoundedSegment( center, axis, third, y, ny, box[ 29 ], axes, ref count );
	}

	private static void AppendCylinderBoxCap( CollisionWordVector center, CollisionWordVector axis, uint radius,
		ReadOnlySpan<uint> box, Span<uint> axes, ref int count )
	{
		var boxCenter = Read( box, 0 );
		var difference = center.Subtract( boxCenter );
		Span<uint> signed = stackalloc uint[ 3 ];
		for ( var i = 0; i < 3; ++i )
			signed[ i ] = SignedBoxExtent( difference, Read( box, 16 + i * 4 ), box[ 28 + i ] );
		var corner = Read( box, 20 ).Scale( signed[ 1 ] );
		corner = Read( box, 16 ).MultiplyAdd( signed[ 0 ], corner );
		corner = Read( box, 24 ).MultiplyAdd( signed[ 2 ], corner );
		corner = CylinderAdd( corner, boxCenter );
		for ( var i = 0; i < 3; ++i )
		{
			var edge = Read( box, 16 + i * 4 );
			var negative = signed[ i ] ^ 0x80000000u;
			var first = edge.MultiplyAdd( Add( negative, box[ 28 + i ] ), corner );
			var second = edge.MultiplyAdd( Subtract( negative, box[ 28 + i ] ), corner );
			AppendCylinderCircleSegment( center, axis, radius, first, second, axes, ref count );
		}
	}

	private static uint SignedBoxExtent( CollisionWordVector direction, CollisionWordVector axis, uint extent ) =>
		Greater( 0, TriangleDot( direction, axis ) ) ? extent ^ 0x80000000u : extent;
}
