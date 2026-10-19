namespace SkateMotion.Core.Animation;

/// <summary>An owner-specific parameter whose scalar can use normalized sample coordinates.</summary>
internal readonly record struct AnimationAttributeOverride(
	AnimationAttributeQueryResult Attribute, int Owner, bool Normalized )
{
	/// <summary>Compatibility name for the normalized-coordinate bit; it does not retain the channel record.</summary>
	internal bool Persistent { get => Normalized; init => Normalized = value; }
}
