namespace SkateMotion.Core.Collision;

using Vector3 = System.Numerics.Vector3;
using static SkateMotion.Core.Animation.LimbSolverMath;

/// <summary>Tests ordered curved-motion segments and reconstructs the first contact's sample time.</summary>
internal sealed class TrajectoryWorldQuery
{
	private readonly MeshWorldQuery _world;
	private readonly TrajectorySurfaceQuery _surface;
	private readonly TrajectoryQuerySettings _settings;

	internal TrajectoryWorldQuery( MeshWorldQuery world, TrajectoryQuerySettings settings )
	{
		_world = world;
		_surface = new( settings );
		_settings = settings;
	}

	internal TrajectoryContact Evaluate( TrajectoryQueryRequest request, MeshQueryInstance[] instances )
	{
		if ( !(request.Radius > 0f) || (request.Acceleration.Y < 0f
			&& !(request.FirstStep > 0f && request.LaterStep > 0f)) )
			throw new ArgumentOutOfRangeException( nameof( request ), "Trajectory radius and downward-acceleration step controls must be positive." );
		var first = request.Duration;
		var later = request.Duration;
		if ( request.Acceleration.Y < 0f )
		{
			var inverse = Reciprocal( request.Acceleration.Y );
			first = SquareRoot( MultiplyAdd( inverse, MultiplyAdd( request.FirstStep * request.Radius, -8f, 0f ), 0f ) );
			later = SquareRoot( MultiplyAdd( inverse, MultiplyAdd( request.Radius * request.LaterStep, -8f, 0f ), 0f ) );
		}
		var minimumSquared = MultiplyAdd( request.FirstStep, request.Radius, 0f );
		minimumSquared = MultiplyAdd( minimumSquared, request.FirstStep, 0f );
		minimumSquared = MultiplyAdd( minimumSquared, request.Radius, 0f );
		var inverseDuration = Reciprocal( request.Duration );
		var time = 0f;
		var start = PositionAt( request, 0f );
		var end = PositionAt( request, first );
		while ( request.Duration > time )
		{
			var delta = end - start;
			if ( Dot( delta, delta ) > minimumSquared )
			{
				var surfaces = request.Surfaces with { Front = true, Back = true };
				var hit = _world.Evaluate( new( start, end, request.Radius, request.Filter, request.Exclusion, surfaces ), instances );
				start = end;
				if ( hit.HitCount != 0 )
				{
					var contactTime = FindTime( request, hit.Position, time, out var sample );
					var normal = _surface.Evaluate( request, hit.Position, sample, instances );
					return new( hit.Position, hit.Normal, normal, contactTime, hit.Transform, sample, hit.Material, hit.Identity );
				}
			}
			var progress = MultiplyAdd( inverseDuration, time, 0f );
			var step = MultiplyAdd( progress, later, MultiplyAdd( 1f - progress, first, 0f ) );
			time += step;
			end = PositionAt( request, time + step );
		}
		return TrajectoryContact.Empty;
	}

	private float FindTime( TrajectoryQueryRequest request, Vector3 position, float start, out int sample )
	{
		if ( MathF.Abs( MultiplyAdd( Reciprocal( 2f ), request.Acceleration.Y, 0f ) ) <= _settings.MinimumMagnitude )
		{
			var offset = position - request.Position;
			var distance = SquareRoot( Dot( offset, offset ) );
			var speed = SquareRoot( Dot( request.Velocity, request.Velocity ) );
			if ( distance <= _settings.MinimumMagnitude || speed <= _settings.MinimumMagnitude )
			{
				sample = 0;
				return 0f;
			}
			var ratio = MultiplyAdd( Reciprocal( speed ), distance, 0f );
			sample = (int)MathF.Floor( ratio );
			return MultiplyAdd( ratio, _settings.SampleInterval, 0f );
		}

		var previous = float.MaxValue;
		var inverseInterval = Reciprocal( _settings.SampleInterval );
		for ( var time = start; request.Duration >= time; time += _settings.SampleInterval )
		{
			var offset = position - PositionAt( request, time );
			var squared = Dot( offset, offset );
			if ( squared > previous )
			{
				var result = time - _settings.SampleInterval;
				sample = (int)MathF.Ceiling( MultiplyAdd( inverseInterval, result, 0f ) );
				return result;
			}
			previous = squared;
		}
		sample = (int)MathF.Ceiling( MultiplyAdd( inverseInterval, request.Duration, 0f ) );
		return request.Duration;
	}

	private static Vector3 PositionAt( TrajectoryQueryRequest request, float time ) =>
		AddScaled( Scale( request.Acceleration, .5f ), MultiplyAdd( time, time, 0f ),
			AddScaled( request.Velocity, time, request.Position ) );
}
