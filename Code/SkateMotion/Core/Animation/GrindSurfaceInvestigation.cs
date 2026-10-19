namespace SkateMotion.Core.Animation;

using SkateMotion.Core.Collision;
using SkateMotion.Core.Grinding;
using Vector3 = System.Numerics.Vector3;

/// <summary>Runs ordered edge probes through mesh collision and classifies their results.</summary>
internal sealed class GrindSurfaceInvestigation
{
	private readonly GrindSurfaceProbePlan _plan;
	private readonly GrindSurfaceClassifier _classifier;
	private readonly GrindSurfaceHit[] _hits = new GrindSurfaceHit[ 7 ];
	internal int ProbeCount => _plan.Count;

	internal GrindSurfaceInvestigation( GrindSurfaceProbePlan plan, GrindSurfaceClassifier classifier )
	{
		ArgumentNullException.ThrowIfNull( plan );
		ArgumentNullException.ThrowIfNull( classifier );
		_plan = plan;
		_classifier = classifier;
	}

	internal bool TryEvaluate( MeshWorldQuery query, MeshQueryInstance[] instances, RailSegment edge,
		Vector3 reference, float reach, bool includeDownward, Vector3 downwardOrigin, int filter,
		uint previousFlags, out GrindSurfaceClassification classification )
	{
		ArgumentNullException.ThrowIfNull( query );
		ArgumentNullException.ThrowIfNull( instances );
		_plan.Build( edge, reference, reach, includeDownward, downwardOrigin );
		return TryEvaluatePrepared( query, instances, reach, filter, previousFlags, out classification );
	}

	internal bool TryEvaluatePrepared( MeshWorldQuery query, MeshQueryInstance[] instances,
		float reach, int filter, uint previousFlags, out GrindSurfaceClassification classification )
	{
		ArgumentNullException.ThrowIfNull( query );
		ArgumentNullException.ThrowIfNull( instances );
		if ( _plan.Count == 0 )
		{
			classification = GrindSurfaceClassifier.Inactive( _plan.Center, reach );
			return false;
		}

		var surfaces = new MeshQueryFilter( 0, 0, 0, 0, true, true );
		for ( var i = 0; i < _hits.Length; i++ )
		{
			_hits[ i ] = default;
			if ( i >= _plan.Count ) continue;
			var probe = _plan.GetProbe( i );
			var result = query.Evaluate( new( probe.Start, probe.End, probe.Radius, filter, 0, surfaces ), instances );
			_hits[ i ] = new( result.HitCount != 0, result.Position, result.Normal, result.Fraction, result.Material );
		}
		var section = new GrindSurfaceSection( _plan.Center, _plan.Up, _plan.Direction,
			_plan.GetProbe( 2 ).End, _plan.GetProbe( 3 ).End );
		classification = _classifier.Classify( section, _hits, previousFlags );
		return true;
	}
}
