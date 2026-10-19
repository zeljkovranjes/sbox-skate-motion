using SkateMotion.Core.Collision;

namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;
using Vector4 = System.Numerics.Vector4;
using static LimbSolverMath;

/// <summary>Scores a fallback contact against map geometry and applies eligible vertical correction.</summary>
internal sealed class GrindCandidateRejection
{
	private readonly GrindCandidateRejectionSettings _settings;
	private readonly GrindHeightCorrectionCurve _curve;
	private readonly GrindContactCorrection _correction;
	internal int QueryCount { get; private set; }
	internal Vector3 QueryStart { get; private set; }
	internal Vector3 QueryEnd { get; private set; }

	internal GrindCandidateRejection( GrindCandidateRejectionSettings settings,
		GrindHeightCorrectionCurve curve, GrindContactCorrection correction )
	{
		ArgumentNullException.ThrowIfNull( curve );
		ArgumentNullException.ThrowIfNull( correction );
		_settings = settings;
		_curve = curve;
		_correction = correction;
	}

	internal float Evaluate( GrindCandidateRejectionInput input, MeshWorldQuery query,
		MeshQueryInstance[] instances, ref GrindTrajectoryState trajectory, ref float highestY )
	{
		QueryCount = 0;
		if ( !(MathF.Abs( input.Direction.Y ) < _settings.MaximumAbsDirectionY) )
			return 0f;
		var time = input.Time;
		var valid = time.X >= 0f && time.Y >= 0f && time.Z >= 0f && time.W >= 0f;
		var point = valid ? input.Point : Vector4.Zero;
		QueryStart = new Vector3(
			MultiplyAdd( input.Direction.X, _settings.ProbeOffset, point.X ),
			MultiplyAdd( input.Direction.Y, _settings.ProbeOffset, point.Y ),
			MultiplyAdd( input.Direction.Z, _settings.ProbeOffset, point.Z ) );
		QueryEnd = QueryStart + new Vector3( 0f, -_settings.ProbeDrop, 0f );
		QueryCount = 1;
		var hit = query.Evaluate( new MeshQueryRequest( QueryStart, QueryEnd, 0f, input.Filter, 0u,
			new MeshQueryFilter( 0u, 0u, 0u, 0u, true, true ) ), instances );
		if ( hit.HitCount == 0 )
			return 0f;
		var direction = new Vector3( input.Direction.X, input.Direction.Y, input.Direction.Z );
		if ( !(_settings.MaximumNormalDot > Dot( hit.Normal, direction )) )
			return 0f;
		var distance = MathF.Abs( hit.Position.Y - QueryStart.Y );
		var score = MathF.FusedMultiplyAdd( _settings.DistanceScale, distance, -_settings.ScorePenalty );
		if ( input.Preliminary )
			return score;
		var predicted = Predict( trajectory, time.X );
		var displacement = new Vector3( trajectory.Position.X, trajectory.Position.Y, trajectory.Position.Z ) - predicted;
		var horizontal = new Vector3( displacement.X, 0f, displacement.Z );
		var horizontalLength = SquareRoot( Dot( horizontal, horizontal ) );
		var verticalRatio = displacement.Y;
		if ( horizontalLength > _settings.MinimumHorizontalLength )
			verticalRatio = MultiplyAdd( Reciprocal( horizontalLength ), verticalRatio, 0f );
		if ( _settings.MaximumVerticalRatio < verticalRatio )
			return score;
		if ( _settings.MinimumDistance > distance )
			return distance - _settings.ScorePenalty;
		var correction = _curve.Evaluate( distance );
		if ( predicted.Y > highestY )
			highestY = predicted.Y;
		trajectory = trajectory with
		{
			Velocity = _correction.Apply( trajectory.Velocity,
				new Vector4( 0f, correction, 0f, input.CorrectionAuxiliary ), input.SampleIndex, _settings.MaximumCorrectionSpeed )
		};
		return _settings.DistanceScale * distance;
	}

	private static Vector3 Predict( GrindTrajectoryState trajectory, float time )
	{
		var squared = MultiplyAdd( time, time, 0f );
		return new Vector3(
			MultiplyAdd( MultiplyAdd( trajectory.Acceleration.X, .5f, 0f ), squared,
				MultiplyAdd( trajectory.Velocity.X, time, trajectory.Position.X ) ),
			MultiplyAdd( MultiplyAdd( trajectory.Acceleration.Y, .5f, 0f ), squared,
				MultiplyAdd( trajectory.Velocity.Y, time, trajectory.Position.Y ) ),
			MultiplyAdd( MultiplyAdd( trajectory.Acceleration.Z, .5f, 0f ), squared,
				MultiplyAdd( trajectory.Velocity.Z, time, trajectory.Position.Z ) ) );
	}
}
