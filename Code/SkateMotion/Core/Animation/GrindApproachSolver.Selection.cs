namespace SkateMotion.Core.Animation;

internal sealed partial class GrindApproachSolver
{
	private bool Eligible( int index, GrindApproachState state )
	{
		var definition = _candidates[ index ];
		var crossing = state.Crossings[ definition.PointIndex ];
		if ( index == 4 )
		{
			var first = state.Crossings[ 2 ];
			var second = state.Crossings[ 3 ];
			if ( MathF.Min( first, second ) < 4f && MathF.Abs( first - second ) > 2f )
				return false;
		}
		else if ( state.EarliestCrossing < 5f && MathF.Abs( state.EarliestCrossing - crossing ) > 3f )
		{
			return false;
		}
		if ( crossing <= 0f || state.Distances[ definition.PointIndex ] >= definition.DistanceLimit )
			return false;
		var nearest = (int)(crossing + .5f);
		return MathF.Abs( definition.TargetAngle - MathF.Abs( state.Angles[ nearest ] ) ) < definition.AngleTolerance;
	}

	private void SelectCandidate( bool candidateZero, GrindApproachState state )
	{
		for ( var index = 0; index < _candidates.Length; index++ )
		{
			if ( (index == 0) != candidateZero || !Eligible( index, state ) )
				continue;
			var candidate = index;
			if ( candidate == 3 && state.PreviousCandidate is 1 or 2 && Eligible( state.PreviousCandidate, state ) )
				candidate = state.PreviousCandidate;
			var point = _candidates[ candidate ].PointIndex;
			if ( !state.HasCandidate || state.Distances[ point ] < state.Distances[ state.SelectedPoint ] )
			{
				state.HasCandidate = true;
				state.SelectedPoint = point;
				state.SelectedCandidate = candidate;
				// Eligibility uses the rounded crossing sample; retained angle uses the candidate index.
				state.SelectedAngle = state.Angles[ candidate ];
			}
			if ( state.SelectedCandidate <= 4 )
				break;
		}
	}
}
