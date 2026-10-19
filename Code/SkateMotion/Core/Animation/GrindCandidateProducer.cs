using SkateMotion.Core.Collision;

namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;
using static LimbSolverMath;

/// <summary>Finds nearby edges around a predicted trajectory and scores accepted contacts.</summary>
internal sealed class GrindCandidateProducer
{
	private readonly GrindEdgeLookup _lookup;
	private readonly GrindCandidateAcceptance _acceptance;
	private readonly float _acceptedScore;
	private readonly GrindEdgeRecord[] _edges = new GrindEdgeRecord[ GrindEdgeQuery.Capacity ];
	internal int EdgeCount { get; private set; }
	internal GrindEdgeBounds Bounds { get; private set; }
	internal bool LastAccepted { get; private set; }
	internal int QueryCount { get; private set; }

	internal GrindCandidateProducer( GrindEdgeLookup lookup, GrindCandidateAcceptance acceptance, float acceptedScore )
	{
		ArgumentNullException.ThrowIfNull( lookup );
		ArgumentNullException.ThrowIfNull( acceptance );
		_lookup = lookup;
		_acceptance = acceptance;
		_acceptedScore = acceptedScore;
	}

	internal float Evaluate( GrindEdgeChunk[] chunks, GrindCandidateInput input,
		MeshWorldQuery query, MeshQueryInstance[] instances, ref GrindCandidateProductionState state )
	{
		LastAccepted = false;
		QueryCount = 0;
		var candidate = state.Candidate;
		var trajectory = candidate.Trajectory;
		var timeSquared = MultiplyAdd( candidate.Time, candidate.Time, 0f );
		var predicted = trajectory.Position + Scale( trajectory.Velocity, candidate.Time );
		predicted += Scale( Scale( trajectory.Acceleration, .5f ), timeSquared );
		Bounds = new( predicted - new Vector3( 2f, .5f, 2f ), predicted + new Vector3( 2f, 4f, 2f ) );
		state.FoundEdges = false;
		EdgeCount = _lookup.Collect( chunks, Bounds, input.Reference, _edges );
		if ( EdgeCount == 0 )
			return 0f;
		state.FoundEdges = true;
		var accepted = _acceptance.TryAccept( _edges, EdgeCount, input, query, instances, ref candidate );
		LastAccepted = accepted;
		QueryCount = _acceptance.QueryCount;
		state.Candidate = candidate;
		if ( !accepted )
			return 0f;
		state.Accepted = true;
		return _acceptedScore;
	}

	internal float EvaluateFallback( GrindCandidateFallback fallback, bool foundEdges,
		System.Numerics.Vector4 point, System.Numerics.Vector4 time ) =>
		fallback.Evaluate( foundEdges, point, time, _edges, EdgeCount );
}
