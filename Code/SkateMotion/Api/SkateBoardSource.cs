namespace SkateMotion;

/// <summary>Where a skateboard's shown parts come from, and how its model's parts were told apart.</summary>
public enum SkateBoardSource
{
	/// <summary>The built-in deck, trucks and wheels.</summary>
	Generated,

	/// <summary>A custom deck or wheels from a <see cref="Components.SkateboardRig"/>.</summary>
	Rig,

	/// <summary>The board model's deck, trucks and wheels are pieces of mesh of their own.</summary>
	Pieces,

	/// <summary>The board model's parts share mesh pieces and were split by where each triangle lies.</summary>
	Clusters,

	/// <summary>The board model's own bones move its wheels and trucks.</summary>
	Bones,

	/// <summary>The board model's parts could not be found: it is shown whole, without steering trucks or spinning wheels.</summary>
	Whole
}
