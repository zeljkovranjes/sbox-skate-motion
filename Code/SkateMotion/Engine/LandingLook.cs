using Sandbox;

namespace SkateMotion.Engine;

/// <summary>
/// The look straight down for where a flight comes down, shared by the rider's grab let-go (how long until the landing)
/// and the board's body flips (how much flight is left to turn over in): the first thing below a point within
/// <see cref="Reach"/>, past the board, the rider and any rider's body.
/// </summary>
internal static class LandingLook
{
	/// <summary>Farthest (inches) below the point the ground is looked for.</summary>
	internal const float Reach = 2000f;

	/// <summary>Height (inches) of the ground below <paramref name="from"/>, or null with nothing in reach.</summary>
	internal static float? Ground( Scene scene, Vector3 from, GameObject board, GameObject rider = null )
	{
		var trace = scene.Trace.Ray( from, from + Vector3.Down * Reach ).IgnoreGameObjectHierarchy( board )
			.WithoutTags( SkateTags.Rider );
		if ( rider.IsValid() )
			trace = trace.IgnoreGameObjectHierarchy( rider );
		var below = trace.Run();
		return below.Hit ? below.HitPosition.z : null;
	}
}
