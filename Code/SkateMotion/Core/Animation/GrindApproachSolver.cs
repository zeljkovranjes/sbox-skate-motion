namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;
using static LimbSolverMath;

/// <summary>Predicts board-edge approaches and accumulates positional correction for an active request.</summary>
internal sealed partial class GrindApproachSolver
{
	private readonly LimbSolverMath _math;
	private readonly GrindApproachSettings _settings;
	private readonly Vector3[] _points;
	private readonly GrindApproachCandidate[] _candidates;

	internal GrindApproachSolver( LimbSolverMath math, GrindApproachSettings settings,
		Vector3[] points, GrindApproachCandidate[] candidates )
	{
		ArgumentNullException.ThrowIfNull( math );
		ArgumentNullException.ThrowIfNull( points );
		ArgumentNullException.ThrowIfNull( candidates );
		if ( settings.SampleCount < 1 || settings.SampleCount > GrindApproachState.SampleCapacity
			|| points.Length != GrindApproachState.PointCount || candidates.Length != 7 )
			throw new ArgumentException( "An approach requires one to twelve samples, five board points and seven candidates." );
		if ( !float.IsFinite( settings.NormalAcceleration ) || !Finite( settings.Gravity )
			|| !Nonnegative( settings.NormalizationThreshold ) || !Nonnegative( settings.AngularSpeedThreshold )
			|| !float.IsFinite( settings.AngleScale ) || !float.IsFinite( settings.AngleFoldThreshold )
			|| !float.IsFinite( settings.AngleFoldPeriod ) || !Nonnegative( settings.MaximumCorrectionVelocity )
			|| !Nonnegative( settings.MaximumCorrectionPosition ) )
			throw new ArgumentException( "Approach parameters must be finite, with nonnegative thresholds and limits." );
		foreach ( var point in points )
		{
			if ( !Finite( point ) )
				throw new ArgumentException( "Board points must be finite." );
		}
		foreach ( var candidate in candidates )
		{
			if ( candidate.PointIndex < 0 || candidate.PointIndex >= GrindApproachState.PointCount
				|| !float.IsFinite( candidate.TargetAngle ) || !Nonnegative( candidate.AngleTolerance )
				|| !Nonnegative( candidate.DistanceLimit ) || !float.IsFinite( candidate.ResponseFactor ) )
				throw new ArgumentException( "Candidates need valid board points and finite parameters with nonnegative limits." );
		}
		_math = math;
		_settings = settings;
		_points = points.AsSpan().ToArray();
		_candidates = candidates.AsSpan().ToArray();
	}

	/// <summary>Processes an already-enabled request; activation and suppression belong to its caller.</summary>
	internal void Update( GrindApproachInput input, GrindApproachState state )
	{
		ArgumentNullException.ThrowIfNull( state );
		state.HasCandidate = false;
		state.PreviousCandidate = state.SelectedCandidate;
		BuildFrames( input, state );
		Predict( input, state );
		FindCrossings( input.EdgeStart, state );
		SelectCandidate( input.CandidateZero, state );
		if ( state.HasCandidate )
			RefinePosition( state );
	}

	private Vector3 NormalizeGuarded( Vector3 value )
	{
		var squared = Dot( value, value );
		var inverse = InverseSquareRoot( squared );
		var length = squared == 0f ? 0f : MultiplyAdd( squared, inverse, 0f );
		return length > _settings.NormalizationThreshold ? Scale( value, inverse ) : Vector3.Zero;
	}

	private void BuildFrames( GrindApproachInput input, GrindApproachState state )
	{
		var along = NormalizeGuarded( input.EdgeEnd - input.EdgeStart );
		var side = Cross( input.Normal, along );
		// The second cross uses the unnormalized side before either axis is normalized.
		var up = NormalizeGuarded( Cross( along, side ) );
		state.EdgeFrame = new LimbFrame( NormalizeGuarded( side ), up, along );
		state.VelocityFrame = new LimbFrame( NormalizeGuarded( Cross( input.Velocity, along ) ), up, along );
	}

	private void RefinePosition( GrindApproachState state )
	{
		var point = state.SelectedPoint;
		var velocity = AddScaled( state.Corrections[ point ], Reciprocal( state.Crossings[ point ] ), state.CorrectionVelocity );
		state.CorrectionVelocity = LimitLength( velocity, _settings.MaximumCorrectionVelocity );
		state.CorrectionPosition = LimitLength( state.CorrectionPosition + state.CorrectionVelocity, _settings.MaximumCorrectionPosition );
	}

	private static Vector3 LimitLength( Vector3 value, float maximum )
	{
		var length = SquareRoot( Dot( value, value ) );
		return length > maximum ? Scale( value, maximum / length ) : value;
	}

	private static bool Finite( Vector3 value ) => float.IsFinite( value.X ) && float.IsFinite( value.Y ) && float.IsFinite( value.Z );

	private static bool Nonnegative( float value ) => float.IsFinite( value ) && value >= 0f;
}
