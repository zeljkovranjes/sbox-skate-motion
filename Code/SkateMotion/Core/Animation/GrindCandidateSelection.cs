using SkateMotion.Core.Collision;

namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;
using Vector4 = System.Numerics.Vector4;

/// <summary>Scores completed candidates, retains the chosen trajectory and submits an eligible repeat.</summary>
internal sealed class GrindCandidateSelection
{
	private readonly GrindCandidateScoring _scoring;
	private readonly GrindEdgeSampleQueue _samples;
	private readonly GrindCandidateBias _bias;
	private readonly GrindTrajectoryPreparation _preparation;
	private readonly GrindCaptureEligibility _capture;
	private readonly GrindQueryRequestBuilder _requests;
	private readonly GrindCandidateSelectionSettings _settings;
	private readonly GrindCandidateBiasInput[] _biasInputs = new GrindCandidateBiasInput[ 7 ];
	internal int QueryCount { get; private set; }

	internal GrindCandidateSelection( GrindCandidateScoring scoring, GrindEdgeSampleQueue samples,
		GrindCandidateBias bias, GrindTrajectoryPreparation preparation, GrindCaptureEligibility capture,
		GrindQueryRequestBuilder requests, GrindCandidateSelectionSettings settings )
	{
		ArgumentNullException.ThrowIfNull( scoring );
		ArgumentNullException.ThrowIfNull( samples );
		ArgumentNullException.ThrowIfNull( bias );
		ArgumentNullException.ThrowIfNull( preparation );
		ArgumentNullException.ThrowIfNull( capture );
		ArgumentNullException.ThrowIfNull( requests );
		_scoring = scoring;
		_samples = samples;
		_bias = bias;
		_preparation = preparation;
		_capture = capture;
		_requests = requests;
		_settings = settings;
	}

	internal bool Evaluate( GrindCandidateSelectionInput input, GrindCandidateSelectionState state,
		GrindEdgeChunk[] chunks, MeshWorldQuery query, MeshQueryInstance[] instances, TrajectoryQueryService service )
	{
		ArgumentNullException.ThrowIfNull( state );
		QueryCount = 0;
		state.Transfer.Refresh( state.Queries, state.Candidates );
		state.Repeated = false;
		var queries = state.Queries;
		if ( !queries.Ready || !queries.Targets[ 0 ].Completed )
			return state.Active;
		state.Repeated = state.UpdateCount == 2;
		state.UpdateCount = unchecked( (ushort)(state.UpdateCount + 1) );
		queries.Ready = false;
		state.SelectedIndex = 0;
		state.HasSelection = false;
		var count = unchecked( (short)queries.Count );
		var height = queries.Height;
		_scoring.Evaluate( state.Candidates, count, input.MinimumSample,
			input.Scoring with { UpdateCount = state.UpdateCount, Special = state.HasInput ? state.MotionSpecial : input.Scoring.Special }, chunks, input.Contact,
			query, instances, ref state.Production, ref height );
		queries.Height = height;
		QueryCount = _scoring.QueryCount;
		queries.SecondarySelectionFlag = _scoring.AllTimesInvalid ? (byte)1 : (byte)0;
		_samples.Clear();
		for ( var i = 0; i < count; i++ )
		{
			var candidate = state.Candidates[ i ];
			_biasInputs[ i ] = new( candidate.Trajectory, candidate.Time, candidate.SampleIndex );
		}
		_bias.Apply( _biasInputs, count, height, _scoring.Scores, _scoring.Biases );
		Select( state, count );
		if ( !(state.Selected.Time.X >= _settings.MinimumTime) )
		{
			state.FallbackDirection = state.Selected.Direction;
			state.Early = true;
			state.Repeated = false;
			state.Active = false;
			return false;
		}
		state.SelectedKind = state.Selected.Kind;
		state.Trajectory = _preparation.Prepare( state.Selected.Trajectory, state.SelectedFrame.Direction,
			state.HasInput ? state.Request.Point : input.Anchor, queries.Origin, input.Scoring.Projection,
			state.Selected.SampleIndex, _settings.MaximumCorrectionSpeed );
		var repeat = _capture.Evaluate( new( queries.Special, state.Production.Accepted, state.UpdateCount,
			count, state.SelectedIndex, Spatial( state.SelectedFrame.Direction ), Spatial( input.Scoring.Up ) ), ref state.Capture );
		state.Active = true;
		if ( repeat )
		{
			var request = state.HasInput ? state.Request : input.Request;
			state.Request = request with
			{
				Velocity = state.SelectedVelocity,
				Spread = _settings.RepeatSpread,
				SpreadScale = _settings.RepeatSpreadScale
			};
			SubmitRequests( state.Request, state, service );
			state.UpdateCount = 2;
		}
		return state.Active;
	}

	internal void SubmitRequests( GrindQueryRequestInput input, GrindCandidateSelectionState state, TrajectoryQueryService service )
	{
		ArgumentNullException.ThrowIfNull( state );
		state.Request = input;
		_requests.Build( input, state.Queries, service );
		state.Transfer.TrackRequests( state.Queries, state.Candidates );
	}

	private void Select( GrindCandidateSelectionState state, short count )
	{
		var best = _settings.InitialScore;
		for ( short i = 0; i < count; i++ )
		{
			var score = _scoring.Scores[ i ];
			// Unordered comparisons advance selection; equal finite scores keep the earlier slot.
			if ( score <= best ) continue;
			best = score;
			state.SelectedIndex = i;
		}
		var index = state.SelectedIndex;
		state.Selected = state.Candidates[ index ];
		state.HasSelection = true;
		state.SelectedFrame = _scoring.Frames[ index ];
		// Scoring updates the other frame columns but leaves this incoming direction intact.
		state.SelectedVelocity = state.Queries.Directions[ index ];
		state.Queries.SelectionFlag = _scoring.RejectionFlags[ index ] ? (byte)1 : (byte)0;
	}

	private static Vector3 Spatial( Vector4 value ) => new( value.X, value.Y, value.Z );
}
