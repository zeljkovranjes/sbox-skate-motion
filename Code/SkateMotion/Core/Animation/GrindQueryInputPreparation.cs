using SkateMotion.Core.Collision;

namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;

/// <summary>Starts a new query cycle only when the previous request is no longer pending.</summary>
internal sealed class GrindQueryInputPreparation
{
	private readonly GrindQueryMotionPreparation _motion;
	private readonly GrindQueryRequestBuilder _requests;

	internal GrindQueryInputPreparation( GrindQueryMotionPreparation motion, GrindQueryRequestBuilder requests )
	{
		ArgumentNullException.ThrowIfNull( motion );
		ArgumentNullException.ThrowIfNull( requests );
		_motion = motion;
		_requests = requests;
	}

	internal bool TryBegin( GrindQueryRequestInput input, float orientation, int movementState,
		GrindCandidateSelectionState state, TrajectoryQueryService service )
	{
		ArgumentNullException.ThrowIfNull( state );
		if ( state.Queries.Ready ) return false;
		state.UpdateCount = 0;
		state.SelectedIndex = -1;
		state.Production.Candidate = state.Production.Candidate with
		{
			EdgeUp = Vector3.UnitY,
			Side = Vector3.UnitY,
			HighSide = Vector3.UnitY,
			IsActive = false
		};
		state.Production.Accepted = false;
		state.Active = false;
		var velocity = _motion.Prepare( input.Velocity, input.ExternalUp, orientation,
			input.SecondarySpeed, movementState, out var special );
		state.MotionSpecial = special;
		state.HasInput = true;
		state.Request = input with { Velocity = velocity };
		_requests.Build( state.Request, state.Queries, service );
		state.Transfer.TrackRequests( state.Queries, state.Candidates );
		return true;
	}
}
