namespace SkateMotion.Core.BoardModel;

/// <summary>How a model's parts were told apart.</summary>
internal enum BoardPartSource
{
	/// <summary>Each part is its own connected piece of mesh (or several).</summary>
	Pieces,

	/// <summary>Parts share mesh pieces, so their triangles were split by where they lie.</summary>
	Clusters,

	/// <summary>The model's own bones move the wheels (and trucks, where it has them).</summary>
	Bones
}
