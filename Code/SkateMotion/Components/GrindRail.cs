using System.Collections.Generic;
using Sandbox;
using SkateMotion.Core.Animation;
using SkateMotion.Core.Grinding;
using SkateMotion.Engine;
using SkateMotion.Systems;

namespace SkateMotion.Components;

/// <summary>Marks a rail, ledge or coping that skateboards can grind and slide on.</summary>
[Title( "Grind Rail" ), Category( "Skate Motion" ), Icon( "linear_scale" )]
public sealed class GrindRail : Component
{
	/// <summary>Local polyline along the grindable edge. Empty uses the top of a BoxCollider on this object.</summary>
	[Property] public List<Vector3> Points { get; set; } = new();

	/// <summary>Box only: boxes narrower than this grind along their center, wider ones along both top edges.</summary>
	[Property, Range( 1f, 24f )] public float RailWidth { get; set; } = 6f;

	private BoxCollider _box;
	private RailSegment[] _segments = Array.Empty<RailSegment>();
	private readonly RailEdgeSource _source = new();

	protected override void OnAwake()
	{
		_box = Components.Get<BoxCollider>();
		GameObject.Tags.Add( SkateTags.Grind );
	}

	protected override void OnEnabled() => Scene.GetSystem<GrindRails>()?.Add( this );

	protected override void OnDisabled() => Scene.GetSystem<GrindRails>()?.Remove( this );

	internal GrindEdgeChunk ReadEdges()
	{
		var capacity = Math.Max( 2, Points.Count - 1 );
		if ( _segments.Length < capacity )
			_segments = new RailSegment[ capacity ];
		var count = Collect( _segments, 0 );
		_source.Update( _segments.AsSpan( 0, count ) );
		return _source.Chunk;
	}

	/// <summary>Appends this rail's world segments in metres; returns the new count.</summary>
	internal int Collect( Span<RailSegment> output, int count )
	{
		if ( Points.Count >= 2 )
		{
			for ( var i = 0; i + 1 < Points.Count && count < output.Length; i++ )
				count = Append( output, count, WorldTransform.PointToWorld( Points[ i ] ),
					WorldTransform.PointToWorld( Points[ i + 1 ] ) );
			return count;
		}
		if ( !_box.IsValid() )
			_box = Components.Get<BoxCollider>();
		if ( !_box.IsValid() )
			return count;
		Span<(Vector3 Start, Vector3 End)> edges = stackalloc (Vector3, Vector3)[ 2 ];
		var edgeCount = BoxRails.TopEdges( _box.Center, _box.Scale, RailWidth, edges );
		for ( var i = 0; i < edgeCount && count < output.Length; i++ )
			count = Append( output, count, WorldTransform.PointToWorld( edges[ i ].Start ),
				WorldTransform.PointToWorld( edges[ i ].End ) );
		return count;
	}

	private static int Append( Span<RailSegment> output, int count, Vector3 start, Vector3 end )
	{
		var segment = new RailSegment( SkateUnits.Metres( start ), SkateUnits.Metres( end ) );
		if ( segment.IsValid )
			output[ count++ ] = segment;
		return count;
	}

	protected override void DrawGizmos()
	{
		Span<RailSegment> segments = stackalloc RailSegment[ 8 ];
		var count = Collect( segments, 0 );
		Gizmo.Draw.Color = Color.Orange;
		Gizmo.Draw.LineThickness = 3f;
		for ( var i = 0; i < count; i++ )
			Gizmo.Draw.Line( WorldTransform.PointToLocal( SkateUnits.Inches( segments[ i ].Start ) ),
				WorldTransform.PointToLocal( SkateUnits.Inches( segments[ i ].End ) ) );
	}
}
