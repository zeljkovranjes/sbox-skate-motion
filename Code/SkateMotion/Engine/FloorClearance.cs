using System.Collections.Generic;
using Sandbox;

namespace SkateMotion.Engine;

/// <summary>
/// Keeps a shown board out of the ground under it: how far its drawn parts must rise to rest on the ground. A deck lying
/// dark side up rests on its kicked-up nose and tail, not on its flat middle as its collision does, so shown where the
/// clip or the physics holds its middle a model's kicks reach inches into the floor. A board upright on its wheels is left
/// to them (the ground under its middle says nothing of a slope or a bank's foot under its wheels).
/// </summary>
internal static class FloorClearance
{
	/// <summary>Most the shown board is lifted (inches): about the rise of a deck's kicks and its trucks' height.</summary>
	internal const float MaximumLift = 4f;

	/// <summary>Deck tilts past which (the deck's up below this height) the board is on its back or side.</summary>
	internal const float Tipped = 0.3f;

	// How far above the deck's middle the ground is looked for from, and how far below it. From just above the deck: a
	// rider stepping off over it (whose body the look would meet from higher up) is never taken for the ground.
	private const float Above = 2f;
	private const float Below = 48f;

	private static readonly Dictionary<Model, (Vector3[] Corners, BBox Box)> _vertices = new();

	/// <summary>
	/// The lift (world inches, straight up) that brings every part, each a model drawn at a pose, out of the ground
	/// under <paramref name="deck"/>; zero for a board upright on its wheels or with no ground in reach.
	/// </summary>
	internal static Vector3 Lift( GameObject board, Transform deck, ReadOnlySpan<(Model Model, Transform Pose)> parts )
	{
		if ( !board.IsValid() || deck.Rotation.Up.z > Tipped )
			return Vector3.Zero;
		var from = deck.Position + Vector3.Up * Above;
		var ground = board.Scene.Trace.Ray( from, deck.Position - Vector3.Up * Below ).IgnoreGameObjectHierarchy( board )
			.WithoutTags( SkateTags.Rider, SkateTags.Grind ).Run();
		if ( !ground.Hit || ground.StartedSolid || ground.Normal.z < 0.7f )
			return Vector3.Zero;
		var floor = ground.HitPosition.z;
		var lowest = float.MaxValue;
		foreach ( var (model, pose) in parts )
		{
			if ( model is null )
				continue;
			// A part whose vertices' box cannot reach into the ground is passed over before any vertex is read.
			var (corners, box) = Corners( model );
			var boxLowest = float.MaxValue;
			for ( var i = 0; i < 8; i++ )
				boxLowest = MathF.Min( boxLowest, pose.PointToWorld( new Vector3( (i & 1) == 0 ? box.Mins.x : box.Maxs.x,
					(i & 2) == 0 ? box.Mins.y : box.Maxs.y, (i & 4) == 0 ? box.Mins.z : box.Maxs.z ) ).z );
			if ( boxLowest >= floor )
				continue;
			foreach ( var corner in corners )
				lowest = MathF.Min( lowest, pose.PointToWorld( corner ).z );
		}
		if ( lowest == float.MaxValue )
			return Vector3.Zero;
		var depth = floor - lowest;
		return depth > 0f ? Vector3.Up * MathF.Min( depth, MaximumLift ) : Vector3.Zero;
	}

	// The vertices a model draws (model inches): its render mesh's, or its bounds' corners where the mesh cannot be read;
	// and the box they fill.
	private static (Vector3[] Corners, BBox Box) Corners( Model model )
	{
		if ( _vertices.TryGetValue( model, out var read ) )
			return read;
		Vector3[] corners;
		var vertices = model.GetVertices();
		if ( vertices is { Length: > 0 } )
		{
			corners = new Vector3[ vertices.Length ];
			for ( var i = 0; i < vertices.Length; i++ )
				corners[ i ] = vertices[ i ].Position;
		}
		else
		{
			var box = model.Bounds;
			corners = new Vector3[ 8 ];
			for ( var i = 0; i < 8; i++ )
				corners[ i ] = new Vector3( (i & 1) == 0 ? box.Mins.x : box.Maxs.x, (i & 2) == 0 ? box.Mins.y : box.Maxs.y,
					(i & 4) == 0 ? box.Mins.z : box.Maxs.z );
		}
		var mins = corners[ 0 ];
		var maxs = corners[ 0 ];
		foreach ( var corner in corners )
		{
			mins = Vector3.Min( mins, corner );
			maxs = Vector3.Max( maxs, corner );
		}
		read = (corners, new BBox( mins, maxs ));
		_vertices[ model ] = read;
		return read;
	}
}
