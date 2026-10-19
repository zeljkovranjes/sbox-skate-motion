namespace SkateMotion.Core.Animation;

using SkateMotion.Core.Collision;
using Vector4 = System.Numerics.Vector4;

/// <summary>Transfers changed requests and contacts without overwriting later scoring corrections.</summary>
internal sealed class GrindCandidateQueryTransfer
{
	private readonly bool[] _tracked = new bool[ 7 ];
	private readonly uint[] _requests = new uint[ 7 ];
	private readonly uint[] _results = new uint[ 7 ];

	internal void TrackRequests( GrindQueryRequestState queries, GrindScoringCandidate[] candidates )
	{
		for ( var i = 0; i < queries.Count; i++ ) _tracked[ i ] = true;
		Refresh( queries, candidates );
	}

	internal void Refresh( GrindQueryRequestState queries, GrindScoringCandidate[] candidates )
	{
		for ( var i = 0; i < _tracked.Length; i++ )
		{
			if ( !_tracked[ i ] ) continue;
			var target = queries.Targets[ i ];
			var requestChanged = _requests[ i ] != target.RequestRevision;
			if ( !requestChanged && _results[ i ] == target.ContactRevision ) continue;
			ref var candidate = ref candidates[ i ];
			if ( requestChanged ) ResetRequest( target, ref candidate );
			CopyContact( target.Contact, ref candidate );
			_requests[ i ] = target.RequestRevision;
			_results[ i ] = target.ContactRevision;
		}
	}

	private static void ResetRequest( TrajectoryQueryTarget target, ref GrindScoringCandidate candidate )
	{
		var request = target.Request;
		candidate.Trajectory = new( new( request.Position, request.PositionAuxiliary ),
			new( request.Velocity, request.VelocityAuxiliary ), new( request.Acceleration, request.AccelerationAuxiliary ),
			new Vector4( request.Duration ) );
		candidate.Radius = request.Radius;
		candidate = candidate with { FirstStep = request.FirstStep, LaterStep = request.LaterStep, RequestFlag = target.Flag };
	}

	private static void CopyContact( TrajectoryContact contact, ref GrindScoringCandidate candidate )
	{
		candidate.Point = new( contact.Position, 0f );
		candidate.Direction = new( contact.Normal, 0f );
		candidate.Axis = new( contact.SurfaceNormal, 0f );
		candidate.Time = new( contact.Time );
		candidate.SampleIndex = contact.Sample;
		candidate.Kind = (contact.Material >> 7) & 31;
		candidate = candidate with { ContactFrame = contact.Transform, Material = contact.Material, Identity = contact.Identity };
	}
}
