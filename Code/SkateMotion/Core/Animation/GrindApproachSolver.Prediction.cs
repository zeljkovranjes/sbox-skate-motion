namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;
using static LimbSolverMath;

internal sealed partial class GrindApproachSolver
{
	private void Predict( GrindApproachInput input, GrindApproachState state )
	{
		var acceleration = AddScaled( input.Normal, input.TimeStep * _settings.NormalAcceleration,
			Scale( _settings.Gravity, input.TimeStep ) );
		var speed = SquareRoot( Dot( input.AngularVelocity, input.AngularVelocity ) );
		var increment = speed < _settings.AngularSpeedThreshold ? LimbRigidTransform.Identity.Rotation
			: _math.AxisRotation( Scale( input.AngularVelocity, 1f / speed ), speed * input.TimeStep );
		var velocity = input.Velocity;
		state.Poses[ 0 ] = input.Board;
		for ( var i = 1; i < _settings.SampleCount; i++ )
		{
			velocity += acceleration;
			var previous = state.Poses[ i - 1 ];
			var axes = new LimbFrame(
				increment.Transform( previous.Rotation.X, Vector3.Zero ),
				increment.Transform( previous.Rotation.Y, Vector3.Zero ),
				increment.Transform( previous.Rotation.Z, Vector3.Zero ) );
			// Translation integrates in world space; rotating the axes must not orbit the position.
			state.Poses[ i ] = new LimbRigidTransform( axes, AddScaled( velocity, input.TimeStep, previous.Translation ) );
		}
		for ( var i = 0; i < _settings.SampleCount; i++ )
		{
			var first = i * GrindApproachState.PointCount;
			for ( var point = 0; point < GrindApproachState.PointCount; point++ )
				state.Points[ first + point ] = state.Poses[ i ].Transform( _points[ point ] );
			state.Angles[ i ] = SampleAngle( state.Points[ first + 1 ] - state.Points[ first + 4 ], state.EdgeFrame );
		}
	}

	private float SampleAngle( Vector3 direction, LimbFrame frame )
	{
		direction = NormalizeGuarded( direction - Scale( frame.Y, Dot( frame.Y, direction ) ) );
		if ( Dot( direction, frame.Z ) < 0f )
			direction = -direction;
		var angle = MathF.Abs( _math.ArcCosine( Math.Clamp( Dot( direction, frame.Z ), -1f, 1f ) ) );
		if ( angle > _settings.AngleFoldThreshold )
			angle = _settings.AngleFoldPeriod - angle;
		var sign = Dot( frame.Y, Cross( direction, frame.Z ) ) < 0f ? -1f : 1f;
		return -MultiplyAdd( _settings.AngleScale, angle * sign, 0f );
	}

	private void FindCrossings( Vector3 origin, GrindApproachState state )
	{
		state.EarliestCrossing = _settings.SampleCount;
		for ( var point = 0; point < GrindApproachState.PointCount; point++ )
		{
			state.Crossings[ point ] = -1f;
			state.Distances[ point ] = 0f;
			for ( var i = 1; i < _settings.SampleCount; i++ )
			{
				var previous = state.Points[ (i - 1) * GrindApproachState.PointCount + point ];
				var current = state.Points[ i * GrindApproachState.PointCount + point ];
				var after = Dot( state.VelocityFrame.Y, current - origin );
				var before = Dot( state.VelocityFrame.Y, previous - origin );
				if ( after * before >= 0f )
					continue;
				var fraction = MathF.Abs( before / (before - after) );
				var crossing = fraction + (i - 1);
				var intersection = AddScaled( previous, 1f - fraction, Scale( current, fraction ) );
				var projection = AddScaled( state.VelocityFrame.Z, Dot( intersection - origin, state.VelocityFrame.Z ), origin );
				var correction = projection - intersection;
				state.Crossings[ point ] = crossing;
				state.Corrections[ point ] = correction;
				state.Distances[ point ] = SquareRoot( Dot( correction, correction ) );
				state.EarliestCrossing = MathF.Min( state.EarliestCrossing, crossing );
				break;
			}
		}
	}
}
