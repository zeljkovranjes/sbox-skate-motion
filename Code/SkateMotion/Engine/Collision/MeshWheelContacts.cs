using System.Collections.Generic;
using Sandbox;
using SkateMotion.Core.Collision;
using SkateMotion.Core.Grinding;

namespace SkateMotion.Engine.Collision;

using NumericsVector = System.Numerics.Vector3;

/// <summary>Feeds selected map-shape triangles into wheel sweeps while retaining engine surface ownership.</summary>
internal sealed class MeshWheelContacts
{
	private readonly MapShapeCache _shapes;
	private readonly HashSet<PhysicsShape> _met = new();
	private readonly MeshQueryInstance[] _instance = new MeshQueryInstance[ 1 ];
	private readonly MeshWorldQuery _query = new( 1e-8f, -.00001f, 2.938735877055719e-39f, 1.52587890625e-5f, 1e-6f );
	private static readonly MeshQueryFilter Surfaces = new( 0, 0, 0, 0, true, false );
	internal long Queries { get; private set; }
	internal long Hits { get; private set; }
	internal int TriangleCount { get; private set; }

	/// <param name="shapes">The board's read shapes, shared with its contact generation.</param>
	internal MeshWheelContacts( MapShapeCache shapes ) => _shapes = shapes;

	internal bool Refine( ref SceneTraceResult trace, NumericsVector start, NumericsVector end, float radius )
	{
		var shape = trace.Shape;
		if ( !trace.Hit || trace.StartedSolid || !shape.IsValid()
			|| !(shape.IsMeshShape || shape.IsHullShape || shape.IsHeightfieldShape) )
			return trace.Hit;
		// The engine trace retains the material and dynamic body for friction/reaction.
		var entry = _shapes.Get( shape, static (_, _) => 0 );
		if ( _met.Add( shape ) )
			TriangleCount += entry.Geometry.Triangles.Length;
		_instance[ 0 ] = entry.Instance();
		Queries++;
		var contact = _query.Evaluate( new( RailEdgeSpace.ToLookup( start ), RailEdgeSpace.ToLookup( end ),
			radius, -1, 0, Surfaces ), _instance );
		if ( contact.HitCount == 0 )
			return false;
		Hits++;
		trace.HitPosition = SkateUnits.Inches( RailEdgeSpace.ToWorld( contact.Position ) );
		trace.Normal = SkateUnits.ToEngine( RailEdgeSpace.ToWorld( contact.Normal ) );
		trace.Fraction = contact.Fraction;
		return true;
	}

	/// <summary>Starts the count of shapes met over; the shapes keep their triangles in the shared cache.</summary>
	internal void Clear()
	{
		_met.Clear();
		TriangleCount = 0;
	}
}
