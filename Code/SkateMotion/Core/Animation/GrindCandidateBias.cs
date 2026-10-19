namespace SkateMotion.Core.Animation;

using static LimbSolverMath;

/// <summary>Adds retained or height-generated bias to eligible candidate scores.</summary>
internal sealed class GrindCandidateBias
{
	private readonly int _minimumSampleIndex;
	private readonly float _heightBias;

	internal GrindCandidateBias( int minimumSampleIndex, float heightBias )
	{
		_minimumSampleIndex = minimumSampleIndex;
		_heightBias = heightBias;
	}

	internal void Apply( GrindCandidateBiasInput[] candidates, short count, float height,
		float[] scores, float[] biases )
	{
		if ( count <= 0 )
			return;
		ArgumentNullException.ThrowIfNull( candidates );
		ArgumentNullException.ThrowIfNull( scores );
		ArgumentNullException.ThrowIfNull( biases );
		if ( count > candidates.Length || count > scores.Length || count > biases.Length )
			throw new ArgumentOutOfRangeException( nameof( count ) );
		for ( var i = 0; i < count; i++ )
		{
			var candidate = candidates[ i ];
			var time = candidate.Time;
			if ( candidate.SampleIndex < _minimumSampleIndex
				|| !(time.X >= 0f && time.Y >= 0f && time.Z >= 0f && time.W >= 0f) )
				continue;
			var trajectory = candidate.Trajectory;
			var linearHeight = MultiplyAdd( trajectory.Velocity.Y, time.X, trajectory.Position.Y );
			var halfAcceleration = MultiplyAdd( trajectory.Acceleration.Y, .5f, 0f );
			var squaredTime = MultiplyAdd( time.X, time.X, 0f );
			var predictedHeight = MultiplyAdd( halfAcceleration, squaredTime, linearHeight );
			if ( biases[ i ] == 0f && predictedHeight > height )
				biases[ i ] = _heightBias;
			scores[ i ] += biases[ i ];
		}
	}
}
