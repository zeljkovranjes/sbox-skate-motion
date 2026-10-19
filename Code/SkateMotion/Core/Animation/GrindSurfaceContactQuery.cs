namespace SkateMotion.Core.Animation;

using SkateMotion.Core.Collision;
using SkateMotion.Core.Grinding;
using Vector3 = System.Numerics.Vector3;

/// <summary>Investigates a candidate point and derives its contact frame and approach limits.</summary>
internal sealed class GrindSurfaceContactQuery
{
	private readonly GrindSurfaceInvestigation _investigation;
	private readonly GrindSurfaceDirections _directions;

	internal GrindSurfaceContactQuery( GrindSurfaceInvestigation investigation, GrindSurfaceDirections directions )
	{
		ArgumentNullException.ThrowIfNull( investigation );
		ArgumentNullException.ThrowIfNull( directions );
		_investigation = investigation;
		_directions = directions;
	}

	internal GrindSurfaceContact Evaluate( MeshWorldQuery query, MeshQueryInstance[] instances, RailSegment edge,
		Vector3 approachReference, Vector3 point, float reach, int filter, GrindSurfaceContact previous )
	{
		// The candidate point centers the cross-section. The approach reference only
		// chooses the contact side; this caller does not request a downward probe.
		var active = _investigation.TryEvaluate( query, instances, edge, point, reach,
			false, Vector3.Zero, filter, 0u, out var classification );
		return _directions.Evaluate( active, classification, approachReference, point, previous );
	}
}
