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

	/// <summary>Polyline only: which side of the line the top of its ledge lies on (a box finds its own: its two top
	/// edges have the top between them). An end slide with the deck over that top takes the ledge-top blunts.</summary>
	[Property] public GrindLedgeTop LedgeTop { get; set; } = GrindLedgeTop.None;

	private BoxCollider _box;
	private RailSegment[] _segments = Array.Empty<RailSegment>();
	private readonly RailEdgeSource _source = new();
	private readonly List<RailSolid> _widths = new();

	// How near (metres) a polyline must run to the middle of a box's top to take that box's width.
	private const float TopTolerance = 0.01f;

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

	/// <summary>
	/// Appends this rail's world segments in metres; returns the new count. A box narrower than <see cref="RailWidth"/>
	/// and a polyline segment along the middle of a box collider's top keep that box's width, so trucks keep the whole
	/// rail between their wheels; a bare polyline is a rail 1 in across, and the edges of a wider box have no width.
	/// </summary>
	internal int Collect( Span<RailSegment> output, int count )
	{
		if ( Points.Count >= 2 )
		{
			_widths.Clear();
			CollectSolids( _widths );
			for ( var i = 0; i + 1 < Points.Count && count < output.Length; i++ )
			{
				var start = WorldTransform.PointToWorld( Points[ i ] );
				var end = WorldTransform.PointToWorld( Points[ i + 1 ] );
				count = Append( output, count, start, end, HalfWidthUnder( (start + end) * 0.5f ), TopSide( start, end ) );
			}
			return count;
		}
		if ( !_box.IsValid() )
			_box = Components.Get<BoxCollider>();
		if ( !_box.IsValid() )
			return count;
		Span<(Vector3 Start, Vector3 End)> edges = stackalloc (Vector3, Vector3)[ 2 ];
		var edgeCount = BoxRails.TopEdges( _box.Center, _box.Scale, RailWidth, edges );
		var halfWidth = edgeCount == 1 ? RailSolids.Of( _box ).Half.Y : 0f;
		// The two top edges of a wide box have its top between them.
		var middle = WorldTransform.PointToWorld( _box.Center + Vector3.Up * (_box.Scale.z * 0.5f) );
		for ( var i = 0; i < edgeCount && count < output.Length; i++ )
		{
			var start = WorldTransform.PointToWorld( edges[ i ].Start );
			var end = WorldTransform.PointToWorld( edges[ i ].End );
			var top = edgeCount == 2 ? (middle - (start + end) * 0.5f).WithZ( 0f ).Normal : Vector3.Zero;
			count = Append( output, count, start, end, halfWidth, top );
		}
		return count;
	}

	// Half the width (metres) of the box collider whose top this polyline point runs along the middle of, or zero.
	private float HalfWidthUnder( Vector3 point )
	{
		var metres = SkateUnits.Metres( point );
		foreach ( var solid in _widths )
		{
			if ( solid.OnTopMiddle( metres, TopTolerance ) )
				return solid.Half.Y;
		}
		return 0f;
	}


	/// <summary>
	/// Appends this rail's solids in metres, which nothing shown may pass into: its BoxCollider, or for a polyline the box
	/// colliders on it and below it, or with none a square rail 1 in across hanging below each segment.
	/// </summary>
	internal void CollectSolids( List<RailSolid> output )
	{
		var boxes = Points.Count >= 2 ? Components.GetAll<BoxCollider>( FindMode.EnabledInSelfAndDescendants )
			: Components.GetAll<BoxCollider>( FindMode.EnabledInSelf );
		var found = false;
		foreach ( var box in boxes )
		{
			output.Add( RailSolids.Of( box ) );
			found = true;
		}
		if ( found || Points.Count < 2 )
			return;
		for ( var i = 0; i + 1 < Points.Count; i++ )
			output.Add( RailSolids.Below( WorldTransform.PointToWorld( Points[ i ] ), WorldTransform.PointToWorld( Points[ i + 1 ] ) ) );
	}

	// The direction across a polyline segment to its ledge's top, or zero.
	private Vector3 TopSide( Vector3 start, Vector3 end )
	{
		if ( LedgeTop == GrindLedgeTop.None )
			return Vector3.Zero;
		var left = Vector3.Cross( Vector3.Up, (end - start).WithZ( 0f ) ).Normal;
		return LedgeTop == GrindLedgeTop.Left ? left : -left;
	}

	private static int Append( Span<RailSegment> output, int count, Vector3 start, Vector3 end, float halfWidth,
		Vector3 top = default )
	{
		var segment = new RailSegment( SkateUnits.Metres( start ), SkateUnits.Metres( end ) )
		{
			HalfWidth = halfWidth,
			Top = SkateUnits.ToCore( top )
		};
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
