using Sandbox;

namespace SkateMotion.Engine;

internal static class MeshFaces
{
	private const float ProbeInches = 0.5f;

	/// <summary>
	/// Shape sweeps hit triangle-mesh back faces and report a normal flipped toward the mover,
	/// while rays only hit front faces. A ray into the reported normal therefore separates the two.
	/// </summary>
	internal static bool IsBackFace( Scene scene, SceneTraceResult hit, GameObject ignore )
	{
		if ( !hit.Hit || !hit.Shape.IsValid() || !hit.Shape.IsMeshShape )
			return false;
		var probe = scene.Trace.Ray( hit.HitPosition + hit.Normal * ProbeInches, hit.HitPosition - hit.Normal * ProbeInches )
			.IgnoreGameObjectHierarchy( ignore ).WithoutTags( SkateTags.Rider )
			.Run();
		return !probe.Hit || probe.Body != hit.Body || Vector3.Dot( probe.Normal, hit.Normal ) < 0.5f;
	}
}
