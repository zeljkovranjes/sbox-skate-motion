namespace SkateMotion.Engine.Collision;

using Sandbox;
using SkateMotion.Core.Collision;
using SkateMotion.Core.Grinding;
using NumericsVector = System.Numerics.Vector3;
using NumericsMatrix = System.Numerics.Matrix4x4;

internal sealed partial class MapCollisionSource
{
	/// <summary>Generates contacts from bound shapes using body inputs and binding caches in world-space metres.</summary>
	/// <summary>
	/// World points (metres) that must be in front of every face they may collide with, normally the board's body
	/// centres before this step. A face any of them was behind is skipped, so a board already under a thin ramp or
	/// bank passes out from beneath it instead of catching its back or its foot.
	/// </summary>
	internal NumericsVector[] FrontReferences { get; set; } = [];

	// How far, in metres, a centre may sit behind a face it is resting against before that face counts as behind it.
	private const float BehindTolerance = 0.01f;

	internal int GenerateContacts( ReadOnlyMemory<CollisionMapBodyInput> bodies,
		Func<PhysicsShape, CollisionMapContactContext?> binding, ICollisionContactCollector collector )
	{
		ArgumentNullException.ThrowIfNull( binding );
		ArgumentNullException.ThrowIfNull( collector );
		UpdateTransforms();
		Span<uint> vertices = stackalloc uint[ 12 ];
		Span<uint> transform = stackalloc uint[ 16 ];
		Span<uint> descriptor = stackalloc uint[ 44 ];
		descriptor.Clear();
		var count = 0;
		for ( var shape = 0; shape < _ordered.Count; ++shape )
		{
			var instance = _instances[ shape ];
			if ( instance.Geometry.Triangles.Length == 0 ) continue;
			var context = binding( _ordered[ shape ].Shape );
			if ( !context.HasValue ) continue;
			var queue = new CollisionMapTriangleQueue( bodies, context.Value, collector );
			WriteContactFrame( MapCollisionCoordinates.MatrixToWorld( instance.Transform ), transform );
			// Full enumeration uses the admission override; no wheel-query culling. Edge codes come from shared
			// edges, so seams between faces (and between meshes) do not read as open boundaries.
			var edges = shape < _edgeCodes.Length ? _edgeCodes[ shape ] : null;
			var frame = MapCollisionCoordinates.MatrixToWorld( instance.Transform );
			var index = 0;
			foreach ( var triangle in instance.Geometry.Triangles )
			{
				if ( Behind( FrontReferences, triangle, frame ) )
				{
					index++;
					continue;
				}
				WriteContactVertex( RailEdgeSpace.ToWorld( triangle.A ), vertices );
				WriteContactVertex( RailEdgeSpace.ToWorld( triangle.B ), vertices[ 4.. ] );
				WriteContactVertex( RailEdgeSpace.ToWorld( triangle.C ), vertices[ 8.. ] );
				CollisionWorldTriangleConstruction.Construct( vertices, transform, descriptor,
					0, triangle.Material, instance.Identity, edges is null ? default : edges.AsSpan( index * 3, 3 ) );
				index++;
				queue.Enqueue( descriptor );
				count++;
			}
			queue.GeneratePending();
		}
		return count;
	}

	// Corners closer than this, in metres, are one point when finding shared edges.
	private const float EdgeWeld = 1e-4f;

	// Three edge codes per triangle for each ordered shape, from edges shared between any of the shapes where they
	// stand now, so a seam between separate meshes (a floor meeting a ramp) is handled like a seam inside one.
	// The host meshes carry no authored adjacency.
	private byte[][] _edgeCodes = [];

	private void BuildEdgeCodes()
	{
		var total = 0;
		for ( var shape = 0; shape < _ordered.Count; ++shape )
			total += _instances[ shape ].Geometry.Triangles.Length;
		var a = new NumericsVector[ total ];
		var b = new NumericsVector[ total ];
		var c = new NumericsVector[ total ];
		var index = 0;
		for ( var shape = 0; shape < _ordered.Count; ++shape )
		{
			var frame = MapCollisionCoordinates.MatrixToWorld( _instances[ shape ].Transform );
			foreach ( var triangle in _instances[ shape ].Geometry.Triangles )
			{
				a[ index ] = NumericsVector.Transform( RailEdgeSpace.ToWorld( triangle.A ), frame );
				b[ index ] = NumericsVector.Transform( RailEdgeSpace.ToWorld( triangle.B ), frame );
				c[ index ] = NumericsVector.Transform( RailEdgeSpace.ToWorld( triangle.C ), frame );
				index++;
			}
		}
		var codes = TriangleEdgeCodes.Compute( a, b, c, EdgeWeld );
		_edgeCodes = new byte[ _ordered.Count ][];
		index = 0;
		for ( var shape = 0; shape < _ordered.Count; ++shape )
		{
			var count = _instances[ shape ].Geometry.Triangles.Length * 3;
			_edgeCodes[ shape ] = codes.AsSpan( index, count ).ToArray();
			index += count;
		}
	}

	private static bool Behind( NumericsVector[] points, MeshTriangle triangle, NumericsMatrix frame )
	{
		if ( points.Length == 0 )
			return false;
		var a = NumericsVector.Transform( RailEdgeSpace.ToWorld( triangle.A ), frame );
		var b = NumericsVector.Transform( RailEdgeSpace.ToWorld( triangle.B ), frame );
		var c = NumericsVector.Transform( RailEdgeSpace.ToWorld( triangle.C ), frame );
		var normal = NumericsVector.Cross( b - a, c - a );
		var length = normal.Length();
		if ( length <= 0f )
			return false;
		normal /= length;
		foreach ( var point in points )
		{
			if ( NumericsVector.Dot( point - a, normal ) < -BehindTolerance )
				return true;
		}
		return false;
	}

	private static void WriteContactVertex( NumericsVector value, Span<uint> words )
	{
		words[ 0 ] = BitConverter.SingleToUInt32Bits( value.X );
		words[ 1 ] = BitConverter.SingleToUInt32Bits( value.Y );
		words[ 2 ] = BitConverter.SingleToUInt32Bits( value.Z );
		words[ 3 ] = 0x3f800000u;
	}

	private static void WriteContactFrame( NumericsMatrix value, Span<uint> words )
	{
		// Query matrices omit homogeneous lanes; physical descriptors use affine positions.
		words[ 0 ] = BitConverter.SingleToUInt32Bits( value.M11 );
		words[ 1 ] = BitConverter.SingleToUInt32Bits( value.M12 );
		words[ 2 ] = BitConverter.SingleToUInt32Bits( value.M13 );
		words[ 3 ] = 0;
		words[ 4 ] = BitConverter.SingleToUInt32Bits( value.M21 );
		words[ 5 ] = BitConverter.SingleToUInt32Bits( value.M22 );
		words[ 6 ] = BitConverter.SingleToUInt32Bits( value.M23 );
		words[ 7 ] = 0;
		words[ 8 ] = BitConverter.SingleToUInt32Bits( value.M31 );
		words[ 9 ] = BitConverter.SingleToUInt32Bits( value.M32 );
		words[ 10 ] = BitConverter.SingleToUInt32Bits( value.M33 );
		words[ 11 ] = 0;
		words[ 12 ] = BitConverter.SingleToUInt32Bits( value.M41 );
		words[ 13 ] = BitConverter.SingleToUInt32Bits( value.M42 );
		words[ 14 ] = BitConverter.SingleToUInt32Bits( value.M43 );
		words[ 15 ] = 0x3f800000u;
	}
}
