using Sandbox;

namespace SkateMotion.Engine;

/// <summary>
/// Looks for drops ahead of the subject for the camera's look-down: five points spread along its travel (over about
/// 0.6 s of it, 1 to 5 m), each reached by a line from the subject (a blocked one ends the search) and each with the
/// ground found straight below it, plus the ground under the subject itself. Results are in metres for Core.
/// </summary>
internal sealed class CameraDropProbe
{
	internal const int Points = 5;

	// Seconds of travel spread over the points, and the shortest and longest spread (m).
	private const float TravelTime = 0.6f;
	private const float ShortestSpread = 1f;
	private const float LongestSpread = 5f;

	// Deepest drop looked for (m), and the height above the subject the lines start from (inches).
	private const float DeepestDrop = 4f;
	private const float StartHeight = 12f;

	// Slowest travel (m/s) worth looking ahead of.
	private const float SlowestTravel = 0.3f;

	internal float[] Distances { get; } = new float[ Points ];
	internal float[] Grounds { get; } = new float[ Points ];

	/// <summary>Points found this frame (0 when not moving).</summary>
	internal int Count { get; private set; }

	/// <summary>Ground height (m) under the subject, NaN when there is none in reach.</summary>
	internal float Below { get; private set; }

	/// <param name="origin">The board or the feet (inches).</param>
	/// <param name="velocity">The subject's velocity (inches per second).</param>
	internal void Run( CameraCollision world, Vector3 origin, Vector3 velocity )
	{
		var start = origin + Vector3.Up * StartHeight;
		var depth = DeepestDrop * SkateUnits.InchesPerMetre;
		Below = Ground( world, start, depth, out var below ) ? below / SkateUnits.InchesPerMetre : float.NaN;
		var planar = velocity.WithZ( 0f );
		var speed = planar.Length / SkateUnits.InchesPerMetre;
		Count = 0;
		if ( speed < SlowestTravel )
			return;
		var spacing = Math.Clamp( speed * TravelTime, ShortestSpread, LongestSpread ) / Points;
		var direction = planar.Normal;
		for ( var index = 0; index < Points; index++ )
		{
			var distance = spacing * (index + 1);
			var point = start + direction * (distance * SkateUnits.InchesPerMetre);
			Distances[ index ] = distance;
			Count = index + 1;
			if ( world.Ray( start, point ).Hit )
			{
				Grounds[ index ] = float.NaN;
				return;
			}
			Grounds[ index ] = (Ground( world, point, depth, out var ground ) ? ground : point.z - depth) / SkateUnits.InchesPerMetre;
		}
	}

	private static bool Ground( CameraCollision world, Vector3 from, float depth, out float height )
	{
		var hit = world.Ray( from, from + Vector3.Down * depth );
		height = hit.Hit ? hit.HitPosition.z : 0f;
		return hit.Hit;
	}
}
