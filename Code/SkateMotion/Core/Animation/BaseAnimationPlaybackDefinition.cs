using System.Collections.Generic;

namespace SkateMotion.Core.Animation;

/// <summary>Explicit owner inputs needed to construct and update retained animation trees.</summary>
internal sealed class BaseAnimationPlaybackDefinition
{
	/// <summary>Reference selected by the owning animation collection, independent of individual clips.</summary>
	public int ReferenceIndex { get; set; } = -1;

	/// <summary>Numeric parameter carrying the physical locomotion phase.</summary>
	public string PhaseParameter { get; set; }

	/// <summary>Ordered scalar bindings; absent parameters are omitted rather than replaced with zero.</summary>
	public List<AnimationScalarBinding> Parameters { get; set; } = new();

	/// <summary>Initial construction selectors; each new tree samples the current dictionary once.</summary>
	public Dictionary<string, string> ConstructionSelectors { get; set; } = new();
}
