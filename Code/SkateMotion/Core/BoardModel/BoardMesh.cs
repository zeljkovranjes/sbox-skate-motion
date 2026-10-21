namespace SkateMotion.Core.BoardModel;

using Vector3 = System.Numerics.Vector3;

/// <summary>
/// A model's triangles for finding its parts: vertex positions in the model's own frame (metres), three vertex indices
/// per triangle, and the bones and attachments it was authored with.
/// </summary>
internal sealed class BoardMesh
{
	internal BoardMesh( Vector3[] positions, int[] triangles, BoardHint[] hints = null )
	{
		if ( positions is null || triangles is null || triangles.Length % 3 != 0 )
			throw new ArgumentException( "A board mesh needs positions and whole triangles." );
		foreach ( var index in triangles )
		{
			if ( (uint)index >= (uint)positions.Length )
				throw new ArgumentException( "A triangle refers past the vertex list." );
		}
		Positions = positions;
		Triangles = triangles;
		Hints = hints ?? [];
	}

	internal Vector3[] Positions { get; }

	internal int[] Triangles { get; }

	internal BoardHint[] Hints { get; }

	internal int TriangleCount => Triangles.Length / 3;
}
