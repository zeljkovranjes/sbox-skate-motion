namespace SkateMotion;

/// <summary>How forgiving the measured handling is.</summary>
public enum SkateDifficulty
{
	/// <summary>The measured handling as it is.</summary>
	Normal,
	/// <summary>Quicker pushes, full pops from any windup, and a wider grind lock-on.</summary>
	Easy,
	/// <summary>Slower pushes, low pops from a quick windup, a narrow grind lock-on, and little help lining up landings.</summary>
	Hard
}
