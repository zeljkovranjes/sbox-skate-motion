namespace SkateMotion;

/// <summary>The rider's current relationship to their skateboard.</summary>
public enum SkateRiderMode
{
	/// <summary>Standing on and controlling the board.</summary>
	Riding,
	/// <summary>Walking while the board rolls independently.</summary>
	Walking,
	/// <summary>Walking with the board held at their side.</summary>
	Carrying,
	/// <summary>Separated from the board and simulated as a native ragdoll.</summary>
	Bailed
}
