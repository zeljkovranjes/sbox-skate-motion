using SkateMotion.Core.Collision;

namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;
using Vector4 = System.Numerics.Vector4;
using static LimbSolverMath;

/// <summary>Combines contact production, fallback rejection and directional scores in candidate order.</summary>
internal sealed class GrindCandidateScoring
{
	private readonly GrindCandidateProducer _producer;
	private readonly GrindCandidateFallback _fallback;
	private readonly GrindCandidateRejection _rejection;
	private readonly GrindScoringSettings _settings;
	private readonly GrindEdgeSelectionCurve _speedCurve;
	private readonly GrindEdgeSelectionCurve _timeCurve;
	private readonly GrindEdgeSelectionCurve _distanceCurve;
	internal float[] Scores { get; } = new float[ 7 ];
	internal float[] Biases { get; } = new float[ 7 ];
	internal bool[] RejectionFlags { get; } = new bool[ 7 ];
	internal GrindScoringFrame[] Frames { get; } = new GrindScoringFrame[ 7 ];
	internal bool AllTimesInvalid { get; private set; }
	internal int QueryCount { get; private set; }

	internal GrindCandidateScoring( GrindCandidateProducer producer, GrindCandidateFallback fallback,
		GrindCandidateRejection rejection, GrindScoringSettings settings,
		GrindEdgeSelectionCurve speedCurve, GrindEdgeSelectionCurve timeCurve, GrindEdgeSelectionCurve distanceCurve )
	{
		ArgumentNullException.ThrowIfNull( producer );
		ArgumentNullException.ThrowIfNull( fallback );
		ArgumentNullException.ThrowIfNull( rejection );
		ArgumentNullException.ThrowIfNull( speedCurve );
		ArgumentNullException.ThrowIfNull( timeCurve );
		ArgumentNullException.ThrowIfNull( distanceCurve );
		_producer = producer;
		_fallback = fallback;
		_rejection = rejection;
		_settings = settings;
		_speedCurve = speedCurve;
		_timeCurve = timeCurve;
		_distanceCurve = distanceCurve;
	}

	internal void Evaluate( GrindScoringCandidate[] candidates, short count, int minimumSample,
		GrindScoringContext context, GrindEdgeChunk[] chunks, GrindCandidateInput input,
		MeshWorldQuery query, MeshQueryInstance[] instances, ref GrindCandidateProductionState production, ref float highestY )
	{
		ArgumentNullException.ThrowIfNull( candidates );
		if ( count > candidates.Length || count > Scores.Length )
			throw new ArgumentOutOfRangeException( nameof( count ) );
		AllTimesInvalid = true;
		QueryCount = 0;
		for ( var i = 0; i < count; i++ )
		{
			Scores[ i ] = 0f;
			Biases[ i ] = 0f;
			RejectionFlags[ i ] = false;
			ref var candidate = ref candidates[ i ];
			var time = candidate.Time;
			if ( !(time.X >= 0f && time.Y >= 0f && time.Z >= 0f && time.W >= 0f) )
				continue;
			AllTimesInvalid = false;
			if ( candidate.SampleIndex < minimumSample )
				continue;
			EvaluateCandidate( i, ref candidate, context, chunks, input, query, instances, ref production, ref highestY );
		}
	}

	private void EvaluateCandidate( int index, ref GrindScoringCandidate candidate, GrindScoringContext context,
		GrindEdgeChunk[] chunks, GrindCandidateInput input, MeshWorldQuery query, MeshQueryInstance[] instances,
		ref GrindCandidateProductionState production, ref float highestY )
	{
		var first = index == 0 && context.UpdateCount == 1;
		var initialBonus = first && !context.Special ? _settings.FirstCandidateBonus : 0f;
		var trajectory = candidate.Trajectory;
		var predictedPosition = PredictPosition( trajectory, candidate.Time.X );
		var duration = (float)candidate.SampleIndex * _settings.SampleInterval;
		var predictedVelocity = AddScaled( trajectory.Acceleration, duration, trajectory.Velocity );
		candidate.Direction = candidate.Axis;
		var direction = Spatial( candidate.Direction );
		var velocity = Spatial( predictedVelocity );
		var cross = Cross( direction, NormalizeOrZero( velocity ) );
		var sideways = SquareRoot( Dot( cross, cross ) );
		var along = Dot( direction, velocity );
		var speedWeight = _speedCurve.Evaluate( MathF.Abs( along ) );
		var score = 0f;
		var suppressed = (context.ExternalFlags & (1ul << 61)) != 0
			&& (context.SecondaryFlags & (1ul << 58)) != 0 && (context.SecondaryFlags & (1ul << 59)) == 0;
		if ( first && !suppressed )
			score = Produce( ref candidate, chunks, input with { Approach = velocity }, query, instances, ref production );
		if ( (context.ExternalFlags & (1ul << 25)) != 0 || score == 0f )
			score = _producer.EvaluateFallback( _fallback, production.FoundEdges, candidate.Point, candidate.Time );
		if ( score <= 0f )
		{
			trajectory = candidate.Trajectory;
			var bias = _rejection.Evaluate( new( candidate.Point, candidate.Direction, candidate.Time,
				candidate.SampleIndex, score < 0f, input.Filter, candidate.CorrectionAuxiliary ), query, instances,
				ref trajectory, ref highestY );
			candidate.Trajectory = trajectory;
			QueryCount += _rejection.QueryCount;
			Biases[ index ] = bias;
			RejectionFlags[ index ] = bias > 0f;
			if ( bias < 0f )
				candidate.Direction = new( 0f, 1f, 0f, context.UpAuxiliary );
		}
		var kindBonus = score <= 1f ? KindBonus( ref candidate, context.Projection ) : 0f;
		Frames[ index ] = new( candidate.Direction, predictedVelocity, predictedPosition );
		var specialBonus = context.Special ? SpecialBonus( candidate, context, ref score, ref production ) : 0f;
		var timeBonus = _timeCurve.Evaluate( candidate.Time.X );
		var directional = MathF.FusedMultiplyAdd( speedWeight, _settings.SidewaysGain * sideways, 1f );
		directional = MathF.FusedMultiplyAdd( speedWeight, _settings.AlongGain * along, directional );
		directional += score;
		directional += timeBonus;
		Scores[ index ] = directional + (kindBonus + (initialBonus + specialBonus));
	}

	private float Produce( ref GrindScoringCandidate candidate, GrindEdgeChunk[] chunks, GrindCandidateInput input,
		MeshWorldQuery query, MeshQueryInstance[] instances, ref GrindCandidateProductionState production )
	{
		var trajectory = candidate.Trajectory;
		production.Candidate = production.Candidate with
		{
			Trajectory = new( Spatial( trajectory.Position ), Spatial( trajectory.Velocity ), Spatial( trajectory.Acceleration ), candidate.Radius ),
			Time = candidate.Time.X,
			SampleIndex = candidate.SampleIndex,
			Normal = Spatial( candidate.Direction )
		};
		var score = _producer.Evaluate( chunks, input, query, instances, ref production );
		QueryCount += _producer.QueryCount;
		if ( _producer.LastAccepted )
		{
			var result = production.Candidate;
			candidate.Direction = new( result.Normal, 0f );
			candidate.SampleIndex = result.SampleIndex;
			candidate.Time = new( result.Time );
			candidate.Trajectory = trajectory with { Velocity = new( result.Trajectory.Velocity, trajectory.Velocity.W ) };
		}
		return score;
	}

	private float KindBonus( ref GrindScoringCandidate candidate, Vector4 projection )
	{
		if ( candidate.Kind is 6 or 7 )
			candidate.Direction = projection;
		return candidate.Kind == 6 ? _settings.FirstKindPenalty
			: candidate.Kind is 7 or 8 ? _settings.SecondKindPenalty : 0f;
	}

	private float SpecialBonus( GrindScoringCandidate candidate, GrindScoringContext context,
		ref float score, ref GrindCandidateProductionState production )
	{
		var sign = context.Orientation - _settings.OrientationThreshold >= 0f ? 1f : -1f;
		if ( score > 0f && sign < 0f )
		{
			production.Accepted = false;
			score = _distanceCurve.EvaluateOrdered( 0f );
		}
		var horizontal = NormalizeOrZero( new( context.Velocity.X, 0f, context.Velocity.Z ) );
		var velocityY = candidate.Trajectory.Velocity.Y;
		var oppositeAcceleration = -candidate.Trajectory.Acceleration.Y;
		var apexTime = velocityY >= 0f && oppositeAcceleration > 0f
			? MultiplyAdd( Reciprocal( oppositeAcceleration ), velocityY, 0f ) : 0f;
		var scale = (_settings.SpecialGain * (1f - MathF.Abs( context.Up.Y ))) * sign;
		var deviation = MathF.Abs( candidate.Time.X - apexTime );
		var penalty = deviation - _settings.TimeWindow >= 0f ? 0f : _settings.NearApexPenalty;
		return penalty + MultiplyAdd( scale, Dot( Spatial( candidate.Direction ), horizontal ), 0f );
	}

	private Vector3 NormalizeOrZero( Vector3 value )
	{
		var squared = Dot( value, value );
		var inverse = InverseSquareRoot( squared );
		var length = squared == 0f ? 0f : MultiplyAdd( squared, inverse, 0f );
		return length > _settings.MinimumLength ? Scale( value, inverse ) : Vector3.Zero;
	}

	private static Vector4 PredictPosition( GrindTrajectoryState state, float time ) =>
		AddScaled( AddScaled( state.Acceleration, .5f, Vector4.Zero ), MultiplyAdd( time, time, 0f ),
			AddScaled( state.Velocity, time, state.Position ) );

	private static Vector4 AddScaled( Vector4 value, float scale, Vector4 offset ) => new(
		MultiplyAdd( value.X, scale, offset.X ), MultiplyAdd( value.Y, scale, offset.Y ),
		MultiplyAdd( value.Z, scale, offset.Z ), MultiplyAdd( value.W, scale, offset.W ) );

	private static Vector3 Spatial( Vector4 value ) => new( value.X, value.Y, value.Z );
}
