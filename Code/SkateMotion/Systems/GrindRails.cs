using System.Collections.Generic;
using Sandbox;
using SkateMotion.Components;
using SkateMotion.Core.Animation;
using SkateMotion.Core.Grinding;
using SkateMotion.Engine;

namespace SkateMotion.Systems;

/// <summary>Every enabled grind rail in the scene, queried by nearby skateboards.</summary>
public sealed class GrindRails : GameObjectSystem<GrindRails>
{
	private readonly List<GrindRail> _rails = new();
	private GrindEdgeChunk[] _chunks = Array.Empty<GrindEdgeChunk>();
	private readonly GrindEdgeLookup _lookup = new();
	private readonly GrindEdgeRecord[] _records = new GrindEdgeRecord[ GrindEdgeQuery.Capacity ];
	private readonly List<RailSolid> _solids = new();

	/// <summary>Creates the scene's rail registry.</summary>
	public GrindRails( Scene scene ) : base( scene )
	{
	}

	internal void Add( GrindRail rail )
	{
		if ( !_rails.Contains( rail ) )
			_rails.Add( rail );
	}

	internal void Remove( GrindRail rail ) => _rails.Remove( rail );

	/// <summary>Replaces <paramref name="output"/> with the solids (metres) of rails within radius inches of center.</summary>
	internal void CollectSolids( Vector3 center, float radius, List<RailSolid> output )
	{
		output.Clear();
		var near = SkateUnits.Metres( center );
		var reach = radius / SkateUnits.InchesPerMetre;
		foreach ( var rail in _rails )
		{
			if ( !rail.IsValid() )
				continue;
			_solids.Clear();
			rail.CollectSolids( _solids );
			foreach ( var solid in _solids )
			{
				if ( solid.IsValid && solid.Distance( near ) <= reach )
					output.Add( solid );
			}
		}
	}

	/// <summary>Collects filtered rail segments in a box extending radius inches from center.</summary>
	internal int Collect( Vector3 center, float radius, Span<RailSegment> output )
	{
		for ( var i = _rails.Count - 1; i >= 0; i-- )
			if ( !_rails[ i ].IsValid() )
				_rails.RemoveAt( i );
		if ( _chunks.Length != _rails.Count )
			_chunks = new GrindEdgeChunk[ _rails.Count ];
		for ( var i = 0; i < _rails.Count; i++ )
			_chunks[ i ] = _rails[ i ].ReadEdges();
		var origin = RailEdgeSpace.ToLookup( SkateUnits.Metres( center ) );
		var reach = new System.Numerics.Vector3( radius / SkateUnits.InchesPerMetre );
		var bounds = new GrindEdgeBounds( origin - reach, origin + reach );
		var count = Math.Min( output.Length, _lookup.Collect( _chunks, bounds, origin, _records ) );
		for ( var i = 0; i < count; i++ )
			output[ i ] = RailEdgeSpace.ToWorld( _records[ i ].Segment );
		return count;
	}
}
