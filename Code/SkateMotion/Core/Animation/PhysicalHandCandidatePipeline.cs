namespace SkateMotion.Core.Animation;

using SkateMotion.Core.Collision;
using Vector3 = System.Numerics.Vector3;

/// <summary>Joins source preparation, mesh investigation and deferred candidate consumption.</summary>
internal sealed class PhysicalHandCandidatePipeline
{
	private readonly PhysicalHandRequestController _request;
	private readonly GrindSurfaceInvestigation _investigation;
	private readonly MeshWorldQuery _query;
	private readonly PhysicalHandCandidateController _candidate;

	internal PhysicalHandCandidatePipeline( PhysicalHandRequestController request,
		GrindSurfaceClassifier classifier, MeshWorldQuery query, PhysicalHandCandidateController candidate )
	{
		ArgumentNullException.ThrowIfNull( request );
		ArgumentNullException.ThrowIfNull( query );
		ArgumentNullException.ThrowIfNull( candidate );
		_request = request;
		_investigation = new( request.Plan, classifier );
		_query = query;
		_candidate = candidate;
	}

	internal bool Prepare( GrindEdgeChunk[] chunks, MeshQueryInstance[] instances,
		PhysicalHandCandidatePipelineState state, PhysicalHandRequestInput input, Vector3 reference, int filter )
	{
		ArgumentNullException.ThrowIfNull( state );
		if ( !_request.Prepare( chunks, state.Request, state.Candidate, input, reference ) )
			return false;
		if ( !_investigation.TryEvaluatePrepared( _query, instances, _request.Reach,
			filter, 0u, out var classification ) )
			return false;
		state.Surface.Complete( classification );
		return true;
	}

	internal void Evaluate( PhysicalHandCandidatePipelineState state )
	{
		ArgumentNullException.ThrowIfNull( state );
		_candidate.Update( state.Candidate, state.Request.CandidateInput, state.Surface );
	}
}
