using System.Collections.Generic;
using Sandbox;
using SkateMotion.Core.Grinding;

namespace SkateMotion.Engine;

/// <summary>
/// Keeps a shown board out of the rails: how far its drawn parts must rise, along a rail's up, to sit on the rail
/// instead of in it. Parts are measured by the triangles their models draw, so any deck, truck and wheel models fit.
/// </summary>
internal static class RailClearance
{
	/// <summary>
	/// Most the shown board is lifted (inches): as far as a shown board ever sits below the physical one, which physics
	/// keeps on the rail. A shape deeper in a rail than this is not on it but beside or under it, and is left alone.
	/// </summary>
	internal const float MaximumLift = 4f;

	private static readonly Dictionary<Model, Vector3[]> _triangles = new();

	/// <summary>
	/// The lift (world inches) that clears every part, each a model drawn at a pose, of every rail. A board
	/// <paramref name="resting"/> on a rail (grinding it, or already lifted onto it) is on top of it: every part of it over
	/// the rail rises by its full depth, even one sunk wholly into the rail or through it.
	/// </summary>
	/// <param name="most">The most it lifts (inches): <see cref="MaximumLift"/>, or more where the shown board is known to
	/// sit further below a board physics keeps on the rail.</param>
	internal static Vector3 Lift( IReadOnlyList<RailSolid> rails, ReadOnlySpan<(Model Model, Transform Pose)> parts, bool resting,
		float most = MaximumLift )
	{
		var best = 0f;
		var up = Vector3.Up;
		foreach ( var rail in rails )
		{
			var lift = 0f;
			foreach ( var (model, pose) in parts )
			{
				if ( model is null )
					continue;
				// Most parts are nowhere near the rail: their bounds say so before any triangle is read.
				var bounds = model.Bounds;
				var reach = bounds.Size.Length * 0.5f * MathF.Max( pose.Scale.x, MathF.Max( pose.Scale.y, pose.Scale.z ) );
				if ( rail.Distance( SkateUnits.Metres( pose.PointToWorld( bounds.Center ) ) ) * SkateUnits.InchesPerMetre > reach )
					continue;
				// A part reaching down through the rail's top (a truck's hanger over it, a deck across it) rises by its full
				// depth however deep it is; one wholly inside the rail came in from beside it and rises only gradually.
				var corners = Triangles( model );
				var through = resting;
				for ( var i = 0; i + 2 < corners.Length && !through; i += 3 )
					through = rail.ReachesThroughTop( Corner( pose, corners, i ), Corner( pose, corners, i + 1 ), Corner( pose, corners, i + 2 ) );
				for ( var i = 0; i + 2 < corners.Length; i += 3 )
					lift = MathF.Max( lift, rail.Lift( Corner( pose, corners, i ), Corner( pose, corners, i + 1 ),
						Corner( pose, corners, i + 2 ), through ) );
			}
			if ( lift <= best )
				continue;
			best = lift;
			up = SkateUnits.ToEngine( rail.Up );
		}
		return up * MathF.Min( best * SkateUnits.InchesPerMetre, most );
	}

	private static System.Numerics.Vector3 Corner( Transform pose, Vector3[] corners, int index ) =>
		SkateUnits.Metres( pose.PointToWorld( corners[ index ] ) );

	// The corners of the triangles a model draws (three per triangle, model inches): its render mesh, or its bounds as a
	// box where the mesh cannot be read.
	private static Vector3[] Triangles( Model model )
	{
		if ( _triangles.TryGetValue( model, out var corners ) )
			return corners;
		corners = Mesh( model ) ?? Box( model.Bounds );
		_triangles[ model ] = corners;
		return corners;
	}

	private static Vector3[] Mesh( Model model )
	{
		var vertices = model.GetVertices();
		var indices = model.GetIndices();
		if ( vertices is null || indices is null || vertices.Length == 0 || indices.Length < 3 )
			return null;
		var corners = new Vector3[ indices.Length / 3 * 3 ];
		for ( var i = 0; i < corners.Length; i++ )
		{
			if ( indices[ i ] >= vertices.Length )
				return null;
			corners[ i ] = vertices[ indices[ i ] ].Position;
		}
		return corners;
	}

	private static Vector3[] Box( BBox bounds )
	{
		Vector3 Corner( int index ) => new( (index & 1) == 0 ? bounds.Mins.x : bounds.Maxs.x,
			(index & 2) == 0 ? bounds.Mins.y : bounds.Maxs.y, (index & 4) == 0 ? bounds.Mins.z : bounds.Maxs.z );
		int[] faces = { 0, 1, 3, 0, 3, 2, 4, 6, 7, 4, 7, 5, 0, 4, 5, 0, 5, 1, 2, 3, 7, 2, 7, 6, 0, 2, 6, 0, 6, 4, 1, 5, 7, 1, 7, 3 };
		var corners = new Vector3[ faces.Length ];
		for ( var i = 0; i < faces.Length; i++ )
			corners[ i ] = Corner( faces[ i ] );
		return corners;
	}
}
