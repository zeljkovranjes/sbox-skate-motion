using System.Collections.Generic;

namespace SkateMotion.Core.Animation;

/// <summary>Names supplied by the animation data, independent of a particular clip bank.</summary>
internal sealed class BoardHandlingAnimationDefinition
{
	/// <summary>Motion enum names mapped to keyed clips or direct-leaf directional blends.</summary>
	public Dictionary<string, string> Motions { get; set; } = new();

	/// <summary>Authored scalar attribute supplying directional child coordinates.</summary>
	public string YawAttribute { get; set; } = "yaw";
}
