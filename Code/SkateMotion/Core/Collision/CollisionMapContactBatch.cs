using static SkateMotion.Core.Collision.CollisionArithmetic;

namespace SkateMotion.Core.Collision;

internal static partial class CollisionMapContactBatch
{
	internal static void Generate( ReadOnlySpan<CollisionMapBodyInput> bodies, ReadOnlySpan<uint> triangles,
		CollisionMapContactContext context, ICollisionContactCollector collector )
	{
		ArgumentNullException.ThrowIfNull( collector );
		Validate( bodies, triangles, context );
		Span<uint> shape = stackalloc uint[ 44 ];
		Span<uint> manifold = stackalloc uint[ 140 ];
		for ( var triangleOffset = 0; triangleOffset < triangles.Length; triangleOffset += 44 )
		{
			var triangle = triangles.Slice( triangleOffset, 44 );
			foreach ( var body in bodies )
			{
				if ( body.Disabled || body.ObjectType == 6 ||
					CollisionPairFilter.Rejects( context.BodyFilter.Span, body.Category, context.Category ) ||
					CollisionPairFilter.Rejects( context.TriangleFilter.Span, body.Category, (triangle[ 34 ] >> 23) & 31u ) ||
					body.BodyState.Span[ 35 ] == 1 )
					continue;
				shape.Clear();
				CollisionShapeConstruction.TryConstruct( body.ShapeKind, body.Shape.Span, shape, body.Transform.Span, body.ShapeIdentity );
				if ( (!body.Active && bodies.Length != 1) || !Overlaps( triangle, body.Bounds.Span ) )
					continue;
				var margin = Margin( body.BaseMargin, body.BodyState.Span, triangle, context.MaximumExtension );
				var count = CollisionContactManifold.Assemble( shape, triangle, margin, context.TriangleContext, manifold );
				for ( var point = 0; point < count; ++point )
				{
					var output = collector.AllocateContact();
					if ( output.IsEmpty )
						return;
					if ( output.Length != 64 )
						throw new ArgumentException( "The collector must allocate one complete contact record.", nameof( collector ) );
					WriteContact( body, context, triangle[ 34 ], manifold, point, output );
					collector.CompleteContact();
				}
			}
		}
	}

	private static void Validate( ReadOnlySpan<CollisionMapBodyInput> bodies, ReadOnlySpan<uint> triangles,
		CollisionMapContactContext context )
	{
		if ( triangles.Length % 44 != 0 || context.BodyState.Length < 44 )
			throw new ArgumentException( "A batch requires complete triangle descriptors and a map body state." );
		foreach ( var body in bodies )
			if ( body.Shape.Length < 24 || body.Transform.Length < 16 || body.Bounds.Length < 8 ||
				body.BodyState.Length < 44 || body.ShapeKind is < 1 or > 5 )
				throw new ArgumentException( "A body requires complete shape, transform, bounds and rigid state inputs.", nameof( bodies ) );
		for ( var offset = 0; offset < triangles.Length; offset += 44 )
			if ( triangles[ offset + 36 ] != 3 )
				throw new ArgumentException( "Map batch descriptors must describe triangles.", nameof( triangles ) );
	}

	private static bool Overlaps( ReadOnlySpan<uint> triangle, ReadOnlySpan<uint> bounds )
	{
		for ( var axis = 0; axis < 3; ++axis )
		{
			var a = triangle[ 8 + axis ];
			var b = triangle[ 12 + axis ];
			var firstGreater = Greater( a, b );
			var maximum = firstGreater ? a : b;
			var minimum = firstGreater ? b : a;
			var c = triangle[ axis ];
			maximum = Greater( c, maximum ) ? c : maximum;
			minimum = Greater( c, minimum ) ? minimum : c;
			if ( Greater( bounds[ axis ], maximum ) || Greater( minimum, bounds[ 4 + axis ] ) )
				return false;
		}
		return true;
	}

	private static uint Margin( uint baseline, ReadOnlySpan<uint> body, ReadOnlySpan<uint> triangle, uint maximum )
	{
		var x = Multiply( Multiply( body[ 8 ], 0x3c888888u ), triangle[ 4 ] ^ 0x80000000u );
		var y = Multiply( Multiply( body[ 9 ], 0x3c888888u ), triangle[ 5 ] ^ 0x80000000u );
		var z = Multiply( Multiply( body[ 10 ], 0x3c888888u ), triangle[ 6 ] ^ 0x80000000u );
		var approach = Add( Add( x, y ), z );
		var positive = approach & unchecked( (uint)((int)(approach ^ 0x80000000u) >> 31) );
		var clamp = unchecked( (uint)((int)Subtract( maximum, positive ) >> 31) );
		return Add( baseline, (maximum & clamp) | (positive & ~clamp) );
	}
}
