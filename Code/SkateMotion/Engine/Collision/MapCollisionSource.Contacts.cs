namespace SkateMotion.Engine.Collision;

using Sandbox;
using System.Collections.Generic;
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

	// Triangles near this step's box, reused so a step allocates nothing.
	private readonly List<int> _nearby = new();

	/// <param name="nearMinimum">Lowest corner (world metres) of the box the board's bodies can reach this step.</param>
	/// <param name="nearMaximum">Highest corner of that box. Triangles whose bounds miss it give no contacts and are not
	/// offered: every triangle of every shape was, every step, which on a level's world mesh is a great many.</param>
	/// <param name="skip">Shapes the board does not collide with this step, or null.</param>
	internal int GenerateContacts( ReadOnlyMemory<CollisionMapBodyInput> bodies, NumericsVector nearMinimum,
		NumericsVector nearMaximum, Func<PhysicsShape, CollisionMapContactContext?> binding,
		ICollisionContactCollector collector, Func<PhysicsShape, bool> skip = null )
	{
		ArgumentNullException.ThrowIfNull( binding );
		ArgumentNullException.ThrowIfNull( collector );
		UpdateTransforms();
		Span<uint> vertices = stackalloc uint[ 12 ];
		Span<uint> transform = stackalloc uint[ 16 ];
		Span<uint> descriptor = stackalloc uint[ 44 ];
		descriptor.Clear();
		var (centre, extent) = LookupBox( nearMinimum, nearMaximum );
		var count = 0;
		for ( var shape = 0; shape < _ordered.Count; ++shape )
		{
			var instance = _instances[ shape ];
			if ( instance.Geometry.Triangles.Length == 0 ) continue;
			var (localMinimum, localMaximum) = LocalBox( centre, extent, instance );
			if ( !MeshQueryMath.Overlaps( instance.Minimum, instance.Maximum, localMinimum, localMaximum ) ) continue;
			if ( skip is not null && skip( _ordered[ shape ].Shape ) ) continue;
			var context = binding( _ordered[ shape ].Shape );
			if ( !context.HasValue ) continue;
			var queue = new CollisionMapTriangleQueue( bodies, context.Value, collector );
			WriteContactFrame( MapCollisionCoordinates.MatrixToWorld( instance.Transform ), transform );
			// The admission override takes every triangle offered. Edge codes come from shared edges, so seams between
			// faces (and between meshes) do not read as open boundaries.
			var edges = shape < _edgeCodes.Length ? _edgeCodes[ shape ] : null;
			var frame = MapCollisionCoordinates.MatrixToWorld( instance.Transform );
			var triangles = instance.Geometry.Triangles;
			if ( instance.Geometry.Grid is { } grid )
			{
				grid.Query( localMinimum, localMaximum, _nearby );
				foreach ( var index in _nearby )
					count += Offer( triangles[ index ], index, frame, edges, instance.Identity, vertices, transform, descriptor, queue );
			}
			else
			{
				for ( var index = 0; index < triangles.Length; index++ )
					count += Offer( triangles[ index ], index, frame, edges, instance.Identity, vertices, transform, descriptor, queue );
			}
			queue.GeneratePending();
		}
		return count;
	}

	// Queues one triangle for contacts unless a board body was behind it; returns how many were queued (0 or 1).
	private int Offer( MeshTriangle triangle, int index, NumericsMatrix frame, byte[] edges, uint identity,
		Span<uint> vertices, Span<uint> transform, Span<uint> descriptor, CollisionMapTriangleQueue queue )
	{
		if ( Behind( FrontReferences, triangle, frame ) )
			return 0;
		WriteContactVertex( RailEdgeSpace.ToWorld( triangle.A ), vertices );
		WriteContactVertex( RailEdgeSpace.ToWorld( triangle.B ), vertices[ 4.. ] );
		WriteContactVertex( RailEdgeSpace.ToWorld( triangle.C ), vertices[ 8.. ] );
		CollisionWorldTriangleConstruction.Construct( vertices, transform, descriptor,
			0, triangle.Material, identity, edges is null ? default : edges.AsSpan( index * 3, 3 ) );
		queue.Enqueue( descriptor );
		return 1;
	}

	// A world box (metres) as a centre and half extent in the lookup space shapes are stored in.
	private static (NumericsVector Centre, NumericsVector Extent) LookupBox( NumericsVector minimum, NumericsVector maximum )
	{
		var a = RailEdgeSpace.ToLookup( minimum );
		var b = RailEdgeSpace.ToLookup( maximum );
		var low = NumericsVector.Min( a, b );
		var high = NumericsVector.Max( a, b );
		return ((low + high) * .5f, (high - low) * .5f);
	}

	// That box in a shape's own space, as the mesh query takes a sweep's.
	private static (NumericsVector Minimum, NumericsVector Maximum) LocalBox( NumericsVector centre, NumericsVector extent,
		MeshQueryInstance instance )
	{
		var localCentre = MeshQueryMath.TransformPoint( centre, instance.InverseTransform );
		var localExtent = MeshQueryMath.TransformExtent( extent, instance.InverseTransform );
		return (localCentre - localExtent, localCentre + localExtent);
	}

	// Corners closer than this, in metres, are one point when finding shared edges.
	private const float EdgeWeld = 1e-4f;

	// Three edge codes per triangle for each ordered shape, from edges shared between any of the shapes where they
	// stand now, so a seam between separate meshes (a floor meeting a ramp) is handled like a seam inside one.
	// The host meshes carry no authored adjacency.
	private byte[][] _edgeCodes = [];

	// Each shape's triangles inside the window, in order, while their edge codes are worked out.
	private readonly List<List<int>> _welded = new();

	/// <summary>
	/// Works out the edge codes of the triangles inside <paramref name="window"/> (world inches): all of a small shape's,
	/// and the part of a large shape reaching into the window, which is all the board can touch before the window moves
	/// on. A large shape's other triangles keep open edges until a later window takes them in.
	/// </summary>
	private void BuildEdgeCodes( BBox window )
	{
		var (centre, extent) = LookupBox( SkateUnits.Metres( window.Mins ), SkateUnits.Metres( window.Maxs ) );
		while ( _welded.Count < _ordered.Count )
			_welded.Add( new List<int>() );
		var total = 0;
		for ( var shape = 0; shape < _ordered.Count; ++shape )
		{
			var instance = _instances[ shape ];
			var inside = _welded[ shape ];
			inside.Clear();
			if ( instance.Geometry.Grid is { } grid )
			{
				var (localMinimum, localMaximum) = LocalBox( centre, extent, instance );
				grid.Query( localMinimum, localMaximum, inside );
			}
			else
			{
				for ( var i = 0; i < instance.Geometry.Triangles.Length; i++ )
					inside.Add( i );
			}
			total += inside.Count;
		}
		var a = new NumericsVector[ total ];
		var b = new NumericsVector[ total ];
		var c = new NumericsVector[ total ];
		var index = 0;
		for ( var shape = 0; shape < _ordered.Count; ++shape )
		{
			var frame = MapCollisionCoordinates.MatrixToWorld( _instances[ shape ].Transform );
			var triangles = _instances[ shape ].Geometry.Triangles;
			foreach ( var inside in _welded[ shape ] )
			{
				var triangle = triangles[ inside ];
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
			var entry = _ordered[ shape ];
			var length = _instances[ shape ].Geometry.Triangles.Length * 3;
			if ( entry.EdgeCodes.Length != length )
			{
				entry.EdgeCodes = new byte[ length ];
				Array.Fill( entry.EdgeCodes, TriangleEdgeCodes.Convex );
				entry.Written.Clear();
			}
			// Only what the last window wrote goes back to the default: a level's mesh, millions of edges, is not filled
			// again on every gathering.
			foreach ( var was in entry.Written )
			{
				entry.EdgeCodes[ was * 3 ] = TriangleEdgeCodes.Convex;
				entry.EdgeCodes[ was * 3 + 1 ] = TriangleEdgeCodes.Convex;
				entry.EdgeCodes[ was * 3 + 2 ] = TriangleEdgeCodes.Convex;
			}
			entry.Written.Clear();
			foreach ( var inside in _welded[ shape ] )
			{
				entry.EdgeCodes[ inside * 3 ] = codes[ index * 3 ];
				entry.EdgeCodes[ inside * 3 + 1 ] = codes[ index * 3 + 1 ];
				entry.EdgeCodes[ inside * 3 + 2 ] = codes[ index * 3 + 2 ];
				entry.Written.Add( inside );
				index++;
			}
			_edgeCodes[ shape ] = entry.EdgeCodes;
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
