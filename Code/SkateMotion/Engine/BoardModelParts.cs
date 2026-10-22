using System.Collections.Generic;
using System.Linq;
using Sandbox;
using SkateMotion.Core.BoardModel;

namespace SkateMotion.Engine;

/// <summary>
/// A skateboard model taken apart into its deck, baseplates, trucks and wheels, found from the model's own geometry (see
/// <see cref="BoardPartDetector"/>). Each part becomes a model of its own placed about where it turns (the deck and
/// baseplates about the deck's middle, a truck about its axle, a wheel about its centre), so trucks steer and wheels spin
/// even when the source is one static mesh. A model whose own bones move its wheels is drawn whole instead and moved by
/// those bones. Lower levels of detail, given as models of their own, are taken apart along the parts found on the
/// first, and each part keeps every level, switching where the model's own levels do. Read once per model and levels.
/// </summary>
internal sealed class BoardModelParts
{
	private static readonly Dictionary<(Model, string), BoardModelParts> _read = new();
	private static readonly Dictionary<(Model, string), string> _refused = new();

	// What the cached readings were found by. A code reload keeps static state, so models read by an earlier detector
	// kept its layout (one without the hanger and wheel gap measured): raise Detection whenever the detector changes
	// what it measures, and the cache is read again.
	private const int Detection = 10;
	private static int _cachedDetection;

	// A board shorter or longer than this (metres) was authored in other units: it is shown at a usual deck's length.
	private const float ShortestBoard = 0.4f;
	private const float LongestBoard = 1.6f;
	private const float UsualLength = 0.8f;

	// A deck wider than this (metres; the widest decks are about 0.27 m) was authored larger than life: it is shown at a
	// usual longboard's width.
	private const float WidestDeck = 0.3f;
	private const float UsualWidth = 0.25f;

	// Parts drawn with more triangles than this keep rails and the ground off a cheaper stand-in (ClearanceProxy) with
	// cells this size (metres).
	private const int ProxyFrom = 3000;
	private const float ProxyCell = 0.004f;

	// Most levels of detail a model has, and the switch metric of a level never switched to (past a model's last level).
	private const int MostLevels = 8;
	private const float Never = 1e9f;
	private readonly Dictionary<Model, Model> _clearance = new();

	private BoardModelParts( Model source, BoardLayout layout, DeckShape shape, float scale )
	{
		Source = source;
		Layout = layout;
		Shape = shape;
		Scale = scale;
		Placement = new Transform( SkateUnits.ToEngine( System.Numerics.Vector3.Transform( -layout.Origin, layout.Rotation ) )
			* SkateUnits.InchesPerMetre, SkateUnits.ToEngine( layout.Rotation ), scale );
	}

	internal Model Source { get; }

	internal BoardLayout Layout { get; }

	/// <summary>The deck's top and underside (metres, the board's frame).</summary>
	internal DeckShape Shape { get; }

	/// <summary>Size the source is shown at against its authored size.</summary>
	internal float Scale { get; }

	/// <summary>The source model's place in the deck's frame (inches): where it is drawn when its own bones move it.</summary>
	internal Transform Placement { get; }

	internal Model Deck { get; private set; }

	internal Model[] Baseplates { get; } = new Model[ 2 ];

	internal Model[] Trucks { get; } = new Model[ 2 ];

	internal Model[] Wheels { get; } = new Model[ 4 ];

	/// <summary>True when the model's own bones move its wheels: it is drawn whole, not split.</summary>
	internal bool Skinned => Layout.Source == BoardPartSource.Bones;

	/// <summary>A wheel's centre in the deck's frame (inches).</summary>
	internal Vector3 WheelCentre( int wheel ) => SkateUnits.Inches( Layout.WheelCentres[ wheel ] );

	/// <summary>A truck's axle centre in the deck's frame (inches): where it turns about the deck.</summary>
	internal Vector3 AxleCentre( int truck ) => SkateUnits.Inches( Layout.AxleCentres[ truck ] );

	/// <summary>A bone's bind pose in the deck's frame (inches).</summary>
	internal Transform BoneRest( int bone ) => Placement.ToWorld( Source.Bones.AllBones[ bone ].LocalTransform );

	/// <summary>What a drawn part model is kept off rails and the ground by: its cheaper stand-in where it is densely
	/// modelled (see <see cref="ClearanceProxy"/>), else the model itself.</summary>
	internal Model Clearance( Model drawn ) => drawn is not null && _clearance.TryGetValue( drawn, out var proxy ) ? proxy : drawn;

	/// <summary>
	/// The parts of a skateboard model, or null (with why) when they cannot be found. <paramref name="levels"/> are its lower
	/// levels of detail as models of their own, nearest first (see <see cref="Components.Skateboard.BoardModelLevels"/>).
	/// </summary>
	internal static BoardModelParts Of( Model model, IReadOnlyList<Model> levels, out string problem )
	{
		problem = null;
		if ( model is null || model.IsError )
		{
			problem = "is missing";
			return null;
		}
		if ( _cachedDetection != Detection )
		{
			_read.Clear();
			_refused.Clear();
			_cachedDetection = Detection;
		}
		var lower = (levels ?? []).Where( level => level is not null && !level.IsError ).Take( MostLevels - 1 ).ToArray();
		var key = (model, string.Join( "|", lower.Select( level => level.ResourcePath ) ));
		if ( _read.TryGetValue( key, out var parts ) )
			return parts;
		if ( _refused.TryGetValue( key, out problem ) )
			return null;
		parts = Read( model, lower, out problem );
		if ( parts is null )
			_refused[ key ] = problem;
		else
			_read[ key ] = parts;
		return parts;
	}

	private static BoardModelParts Read( Model model, Model[] lower, out string problem )
	{
		if ( Gather( model ) is not { } first )
		{
			problem = "has no mesh that can be read";
			return null;
		}
		var levels = new List<(Vertex[] Vertices, int[] Triangles, Material[] Materials)> { first };
		foreach ( var level in lower )
		{
			if ( Gather( level ) is { } read )
				levels.Add( read );
		}
		var (vertices, triangles, _) = first;
		var size = model.Bounds.Size;
		var length = MathF.Max( size.x, MathF.Max( size.y, size.z ) ) / SkateUnits.InchesPerMetre;
		// Across the board (the bounds' middle side): the deck, or the wheels where they stand out past it.
		var across = MathF.Max( MathF.Min( size.x, size.y ), MathF.Min( MathF.Max( size.x, size.y ), size.z ) ) / SkateUnits.InchesPerMetre;
		var scale = length is > ShortestBoard and < LongestBoard ? 1f : UsualLength / MathF.Max( length, 1e-4f );
		if ( scale == 1f && across > WidestDeck )
			scale = UsualWidth / across;
		// The parts are found on the first level of detail, from the vertices it draws.
		var used = new Dictionary<int, int>();
		var drawn = new int[ triangles.Length ];
		var positions = new List<System.Numerics.Vector3>();
		for ( var i = 0; i < triangles.Length; i++ )
		{
			if ( !used.TryGetValue( triangles[ i ], out var index ) )
			{
				index = positions.Count;
				used[ triangles[ i ] ] = index;
				positions.Add( SkateUnits.Metres( vertices[ triangles[ i ] ].Position ) * scale );
			}
			drawn[ i ] = index;
		}
		var mesh = new BoardMesh( positions.ToArray(), drawn, Hints( model, scale ) );
		var layout = BoardPartDetector.Detect( mesh, out problem );
		if ( layout is null )
			return null;
		var parts = new BoardModelParts( model, layout, DeckShape.Of( mesh, layout ), scale );
		if ( !parts.Skinned )
			parts.Split( levels );
		if ( scale != 1f )
			Log.Warning( $"Skateboard model {model.ResourceName} is {length:F2} m long and {across:F2} m wide: shown at {scale:F2} times its size." );
		Log.Info( $"Skateboard model {model.ResourceName}: parts found from {layout.Source}; wheelbase {layout.Wheelbase:F3} m, "
			+ $"track {layout.Track:F3} m, wheel radius {layout.WheelRadius:F4} m, ride height {layout.RideHeight:F3} m, "
			+ $"deck {layout.Length:F3} x {layout.Width:F3} m; {levels.Count} level{(levels.Count == 1 ? "" : "s")} of detail ("
			+ string.Join( ", ", levels.Select( level => $"{level.Triangles.Length / 3} triangles" ) ) + ")."
			+ (layout.LeanLimit < Core.Vehicle.DeckLean.MaximumRoll
				? $" Its deck leans {layout.LeanLimit * (180f / MathF.PI):F1} degrees at most: its trucks and wheels leave it no more room."
				: "" ) );
		return parts;
	}

	// A model's vertices, triangles and each triangle's material, or null without a mesh. Its vertex and index lists hold
	// the meshes of its first level of detail alone (a model's lower levels cannot be read from code), so the meshes that
	// fit them are read, in turn; where the draw calls do not account for the lists as given, they are read as one list
	// drawn with the first material.
	private static (Vertex[] Vertices, int[] Triangles, Material[] Materials)? Gather( Model model )
	{
		var vertices = model.GetVertices();
		var indices = model.GetIndices();
		if ( vertices is null || indices is null || vertices.Length < 3 || indices.Length < 3 )
			return null;
		var calls = new List<(int Start, int Count, int Base, Material Material)>();
		var vertexBase = 0;
		var indexBase = 0;
		foreach ( var mesh in model.MeshInfo?.Meshes ?? [] )
		{
			var used = mesh.DrawCalls.Select( call => call.StartIndex + call.Indices ).DefaultIfEmpty( 0 ).Max();
			if ( (mesh.LodMask & 1) == 0 || vertexBase + mesh.Vertices > vertices.Length || indexBase + used > indices.Length )
				break;
			foreach ( var call in mesh.DrawCalls )
				calls.Add( (indexBase + call.StartIndex, call.Indices, vertexBase + call.BaseVertex, call.Material) );
			vertexBase += mesh.Vertices;
			indexBase += used;
		}
		var fallback = model.Materials.FirstOrDefault( material => material is not null ) ?? Material.Load( "materials/default.vmat" );
		if ( calls.Count == 0 || indexBase != indices.Length || vertexBase != vertices.Length || !Fits( calls, indices, vertices.Length ) )
		{
			calls.Clear();
			calls.Add( (0, indices.Length / 3 * 3, 0, fallback) );
		}
		var triangles = new int[ indices.Length / 3 * 3 ];
		var materials = new Material[ triangles.Length / 3 ];
		System.Array.Fill( materials, fallback );
		foreach ( var (start, count, offset, material) in calls )
		{
			for ( var i = start; i + 2 < start + count && i + 2 < triangles.Length; i += 3 )
			{
				for ( var corner = 0; corner < 3; corner++ )
					triangles[ i + corner ] = Math.Clamp( (int)indices[ i + corner ] + offset, 0, vertices.Length - 1 );
				materials[ i / 3 ] = material ?? fallback;
			}
		}
		return (vertices, triangles, materials);
	}

	private static bool Fits( List<(int Start, int Count, int Base, Material Material)> calls, uint[] indices, int vertexCount )
	{
		foreach ( var (start, count, offset, _) in calls )
		{
			for ( var i = start; i < start + count && i < indices.Length; i++ )
			{
				if ( indices[ i ] + offset >= vertexCount )
					return false;
			}
		}
		return true;
	}

	// Bones at their bind pose and attachments, in metres of the model as shown.
	private static BoardHint[] Hints( Model model, float scale )
	{
		var hints = new List<BoardHint>();
		foreach ( var bone in model.Bones.AllBones )
			hints.Add( new BoardHint( bone.Name, SkateUnits.Metres( bone.LocalTransform.Position ) * scale, bone.Index ) );
		foreach ( var attachment in model.Attachments.All )
		{
			if ( model.Attachments.GetTransform( attachment.Name ) is { } place )
				hints.Add( new BoardHint( attachment.Name, SkateUnits.Metres( place.Position ) * scale, -1 ) );
		}
		return hints.ToArray();
	}

	// Each part's triangles as a model of its own, about the place it turns, at each level of detail. A lower level's
	// triangles go to the part the first level's nearest triangle is in, so each level's wheels turn and trucks steer with
	// the first's, and each level switches in where the source model's own levels do.
	private void Split( List<(Vertex[] Vertices, int[] Triangles, Material[] Materials)> levels )
	{
		var parts = new BoardPart[ levels.Count ][];
		parts[ 0 ] = Layout.Parts;
		for ( var level = 1; level < levels.Count; level++ )
			parts[ level ] = NearestParts( levels[ 0 ], Layout.Parts, levels[ level ] );
		var switches = Source.MeshInfo?.LodSwitchDistances ?? [];
		var name = Source.ResourceName;
		Deck = PartModel( $"{name} deck", BoardPart.Deck, Vector3.Zero, levels, parts, switches );
		for ( var truck = 0; truck < 2; truck++ )
		{
			Baseplates[ truck ] = PartModel( $"{name} baseplate {truck}", BoardLayout.Baseplate( truck ), Vector3.Zero, levels, parts, switches );
			Trucks[ truck ] = PartModel( $"{name} truck {truck}", BoardLayout.Truck( truck ), AxleCentre( truck ), levels, parts, switches );
		}
		for ( var wheel = 0; wheel < 4; wheel++ )
			Wheels[ wheel ] = PartModel( $"{name} wheel {wheel}", BoardLayout.Wheel( wheel ), WheelCentre( wheel ), levels, parts, switches );
	}

	// A part at every level of detail. A part a lower level has nothing of (simplified away) keeps the level before's,
	// and past the last level the last stays.
	private Model PartModel( string name, BoardPart part, Vector3 pivot, List<(Vertex[] Vertices, int[] Triangles, Material[] Materials)> levels, BoardPart[][] parts,
		float[] switches )
	{
		var builder = Model.Builder.WithName( name );
		var first = Groups( part, pivot, levels[ 0 ], parts[ 0 ] );
		if ( first.Count == 0 )
			return null;
		var shown = Meshes( first );
		for ( var level = 0; level < MostLevels; level++ )
		{
			if ( level > 0 && level < levels.Count )
			{
				var groups = Groups( part, pivot, levels[ level ], parts[ level ] );
				if ( groups.Count > 0 )
					shown = Meshes( groups );
			}
			if ( level < levels.Count )
				builder.AddMeshes( shown, level );
			if ( level > 0 )
				builder.WithLodDistance( level, level < levels.Count && level < switches.Length && switches[ level ] > 0f ? switches[ level ] : Never );
		}
		var built = builder.Create();
		if ( first.Values.Sum( group => group.Indices.Count ) / 3 > ProxyFrom && Proxy( $"{name} clearance", first ) is { } proxy )
			_clearance[ built ] = proxy;
		return built;
	}

	// A part's triangles at one level, grouped by material, about its pivot.
	private Dictionary<Material, (List<Vertex> Vertices, List<int> Indices, Dictionary<int, int> Seen)> Groups( BoardPart part,
		Vector3 pivot, (Vertex[] Vertices, int[] Triangles, Material[] Materials) level, BoardPart[] parts )
	{
		var (vertices, triangles, materials) = level;
		var groups = new Dictionary<Material, (List<Vertex> Vertices, List<int> Indices, Dictionary<int, int> Seen)>();
		for ( var t = 0; t < parts.Length; t++ )
		{
			if ( parts[ t ] != part )
				continue;
			if ( !groups.TryGetValue( materials[ t ], out var group ) )
				groups[ materials[ t ] ] = group = (new List<Vertex>(), new List<int>(), new Dictionary<int, int>());
			for ( var corner = 0; corner < 3; corner++ )
			{
				var index = triangles[ t * 3 + corner ];
				if ( !group.Seen.TryGetValue( index, out var local ) )
				{
					local = group.Vertices.Count;
					group.Seen[ index ] = local;
					group.Vertices.Add( Placed( vertices[ index ], pivot ) );
				}
				group.Indices.Add( local );
			}
		}
		return groups;
	}

	private static Mesh[] Meshes( Dictionary<Material, (List<Vertex> Vertices, List<int> Indices, Dictionary<int, int> Seen)> groups )
	{
		var meshes = new List<Mesh>();
		foreach ( var (material, group) in groups )
		{
			var mesh = new Mesh( material );
			mesh.CreateVertexBuffer( group.Vertices.Count, group.Vertices );
			mesh.CreateIndexBuffer( group.Indices.Count, group.Indices );
			mesh.Bounds = BBox.FromPoints( group.Vertices.Select( vertex => vertex.Position ) );
			meshes.Add( mesh );
		}
		return meshes.ToArray();
	}

	// The part each of a later level's triangles is in: that of the first level's triangle whose middle is nearest its
	// own, found through a grid of the first level's triangle middles (cells a 64th of the board's length).
	private static BoardPart[] NearestParts( (Vertex[] Vertices, int[] Triangles, Material[] Materials) first, BoardPart[] firstParts, (Vertex[] Vertices, int[] Triangles, Material[] Materials) later )
	{
		static Vector3 Middle( Vertex[] vertices, int[] triangles, int t ) =>
			(vertices[ triangles[ t * 3 ] ].Position + vertices[ triangles[ t * 3 + 1 ] ].Position + vertices[ triangles[ t * 3 + 2 ] ].Position) / 3f;
		var count = Math.Min( first.Triangles.Length / 3, firstParts.Length );
		var middles = new Vector3[ count ];
		for ( var t = 0; t < count; t++ )
			middles[ t ] = Middle( first.Vertices, first.Triangles, t );
		var bounds = BBox.FromPoints( middles );
		var cell = MathF.Max( MathF.Max( bounds.Size.x, MathF.Max( bounds.Size.y, bounds.Size.z ) ) / 64f, 1e-3f );
		(int, int, int) Cell( Vector3 point ) => ((int)MathF.Floor( point.x / cell ), (int)MathF.Floor( point.y / cell ), (int)MathF.Floor( point.z / cell ));
		var grid = new Dictionary<(int, int, int), List<int>>();
		for ( var t = 0; t < count; t++ )
		{
			var key = Cell( middles[ t ] );
			if ( !grid.TryGetValue( key, out var list ) )
				grid[ key ] = list = new List<int>();
			list.Add( t );
		}
		var parts = new BoardPart[ later.Triangles.Length / 3 ];
		for ( var t = 0; t < parts.Length; t++ )
		{
			var middle = Middle( later.Vertices, later.Triangles, t );
			var (x, y, z) = Cell( middle );
			var best = -1;
			var nearest = float.MaxValue;
			// Shells of cells out from the triangle's own until nothing past the last shell can be nearer.
			for ( var shell = 0; shell <= 64 && (best < 0 || MathF.Sqrt( nearest ) > (shell - 1) * cell); shell++ )
			{
				for ( var dx = -shell; dx <= shell; dx++ )
				for ( var dy = -shell; dy <= shell; dy++ )
				for ( var dz = -shell; dz <= shell; dz++ )
				{
					if ( Math.Max( Math.Abs( dx ), Math.Max( Math.Abs( dy ), Math.Abs( dz ) ) ) != shell
						|| !grid.TryGetValue( (x + dx, y + dy, z + dz), out var list ) )
						continue;
					foreach ( var candidate in list )
					{
						var distance = (middles[ candidate ] - middle).LengthSquared;
						if ( distance < nearest )
						{
							nearest = distance;
							best = candidate;
						}
					}
				}
			}
			parts[ t ] = best < 0 ? BoardPart.Deck : firstParts[ best ];
		}
		return parts;
	}

	// A densely modelled part's stand-in for clearance, built once with the part (model inches, about its pivot).
	private static Model Proxy( string name, Dictionary<Material, (List<Vertex> Vertices, List<int> Indices, Dictionary<int, int> Seen)> groups )
	{
		var points = new List<System.Numerics.Vector3>();
		var normals = new List<System.Numerics.Vector3>();
		var triangles = new List<int>();
		foreach ( var group in groups.Values )
		{
			var first = points.Count;
			foreach ( var vertex in group.Vertices )
			{
				points.Add( SkateUnits.ToCore( vertex.Position ) );
				normals.Add( SkateUnits.ToCore( vertex.Normal ) );
			}
			foreach ( var index in group.Indices )
				triangles.Add( first + index );
		}
		var (proxyPoints, proxyTriangles) = ClearanceProxy.Build( points, normals, triangles, ProxyCell * SkateUnits.InchesPerMetre );
		if ( proxyTriangles.Length < 3 )
			return null;
		var vertices = proxyPoints.Select( point => new Vertex( SkateUnits.ToEngine( point ), Vector3.Up, Vector3.Forward, Vector4.Zero ) ).ToList();
		var mesh = new Mesh( groups.Keys.First() );
		mesh.CreateVertexBuffer( vertices.Count, vertices );
		mesh.CreateIndexBuffer( proxyTriangles.Length, proxyTriangles );
		mesh.Bounds = BBox.FromPoints( vertices.Select( vertex => vertex.Position ) );
		return Model.Builder.AddMesh( mesh ).WithName( name ).Create();
	}

	// A vertex of the source moved into the board's frame and about a part's pivot.
	private Vertex Placed( Vertex vertex, Vector3 pivot )
	{
		var rotation = Placement.Rotation;
		vertex.Position = Placement.PointToWorld( vertex.Position ) - pivot;
		vertex.Normal = rotation * vertex.Normal;
		var tangent = rotation * new Vector3( vertex.Tangent.x, vertex.Tangent.y, vertex.Tangent.z );
		vertex.Tangent = new Vector4( tangent.x, tangent.y, tangent.z, vertex.Tangent.w );
		return vertex;
	}
}
