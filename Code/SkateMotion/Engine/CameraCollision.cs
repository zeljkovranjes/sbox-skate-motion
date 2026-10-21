using Sandbox;

namespace SkateMotion.Engine;

/// <summary>
/// Sphere sweeps from three points of the subject (the focus, head height and the feet) out to a camera position.
/// The way counts as blocked only when all three are, so a pole or rail passing between does not pull the camera in,
/// yet a camera that would sit inside something always is (every sweep to it then hits). The rider, its ragdoll,
/// the board and loose physics props are ignored.
/// </summary>
internal sealed class CameraCollision
{
	// Radius (inches) of the retry for a sweep that starts touching something (a rider pressed against a wall), how
	// short a hit (inches) counts as touching at the start, and how far (inches) a real contact can sit off the sphere.
	private const float NarrowRadius = 2f;
	private const float Touching = 1f;
	private const float ContactSlack = 0.25f;

	// Inches kept back from where a sweep stopped.
	private const float Margin = 0.5f;

	private Scene _scene;
	private GameObject[] _ignored = [];

	/// <summary>Sets the scene and the objects (with everything under them) the sweeps pass through this frame.</summary>
	internal void Ignore( Scene scene, GameObject[] ignored )
	{
		_scene = scene;
		_ignored = ignored;
	}

	/// <summary>
	/// How far from the focus along the line to the end the camera can sit (inches), or infinity when the way is not
	/// blocked from all three points.
	/// </summary>
	internal float Clear( Vector3 focus, Vector3 head, Vector3 feet, Vector3 end, float radius )
	{
		var reach = Sweep( focus, end, radius );
		if ( float.IsPositiveInfinity( reach ) || float.IsPositiveInfinity( Sweep( head, end, radius ) )
			|| float.IsPositiveInfinity( Sweep( feet, end, radius ) ) )
			return float.PositiveInfinity;
		return MathF.Max( reach - Margin, 0f );
	}

	// Distance a sphere travels from start toward end before touching something, or infinity when it gets there. One
	// that starts touching counts as blocked, at the distance a narrow sweep gets.
	private float Sweep( Vector3 start, Vector3 end, float radius )
	{
		var result = Run( start, end, radius );
		var direction = (end - start).Normal;
		if ( !StartsTouching( result, radius, direction ) )
			return result.Hit ? result.Distance : float.PositiveInfinity;
		var narrow = MathF.Min( radius, NarrowRadius );
		var retry = Run( start, end, narrow );
		if ( StartsTouching( retry, narrow, direction ) )
			return 0f;
		return retry.Hit ? retry.Distance : (end - start).Length;
	}

	// Touching at the start shows as starting solid or a hit straight away. A sphere that only just overlaps a
	// one-sided surface may instead report a false hit further on: its contact point is off the sphere, or behind it
	// (a sphere running into something touches it ahead of its centre).
	private static bool StartsTouching( SceneTraceResult result, float radius, Vector3 direction )
	{
		if ( result.StartedSolid )
			return true;
		if ( !result.Hit )
			return false;
		var contact = result.HitPosition - result.EndPosition;
		return result.Distance < Touching || contact.Length > radius + ContactSlack
			|| Vector3.Dot( contact, direction ) < -0.1f * radius;
	}

	/// <summary>A thin ray that passes through the same things.</summary>
	internal SceneTraceResult Ray( Vector3 start, Vector3 end ) => Ignoring( _scene.Trace.Ray( start, end ) ).Run();

	private SceneTraceResult Run( Vector3 start, Vector3 end, float radius ) =>
		Ignoring( _scene.Trace.Sphere( radius, start, end ) ).Run();

	private SceneTrace Ignoring( SceneTrace trace )
	{
		trace = trace.IgnoreDynamic().WithoutTags( SkateTags.Rider );
		foreach ( var ignored in _ignored )
		{
			if ( ignored.IsValid() )
				trace = trace.IgnoreGameObjectHierarchy( ignored );
		}
		return trace;
	}
}
