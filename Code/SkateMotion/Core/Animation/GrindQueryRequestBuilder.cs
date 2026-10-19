namespace SkateMotion.Core.Animation;

using SkateMotion.Core.Collision;
using Vector3 = System.Numerics.Vector3;
using Vector4 = System.Numerics.Vector4;
using Matrix4x4 = System.Numerics.Matrix4x4;
using static LimbSolverMath;

/// <summary>Constructs and submits a direction fan or an eligible retained trajectory.</summary>
internal sealed class GrindQueryRequestBuilder
{
	private readonly GrindQueryFan _fan;
	private readonly GrindEdgeSelectionCurve _tiltCurve;
	private readonly GrindEdgeSelectionCurve _velocityCurve;
	private readonly GrindQueryRequestSettings _settings;

	internal GrindQueryRequestBuilder( GrindQueryFan fan, GrindEdgeSelectionCurve tiltCurve,
		GrindEdgeSelectionCurve velocityCurve, GrindQueryRequestSettings settings )
	{
		ArgumentNullException.ThrowIfNull( fan );
		ArgumentNullException.ThrowIfNull( tiltCurve );
		ArgumentNullException.ThrowIfNull( velocityCurve );
		_fan = fan;
		_tiltCurve = tiltCurve;
		_velocityCurve = velocityCurve;
		_settings = settings;
	}

	internal void Build( GrindQueryRequestInput input, GrindQueryRequestState state, TrajectoryQueryService service )
	{
		ArgumentNullException.ThrowIfNull( state );
		ArgumentNullException.ThrowIfNull( service );
		state.SelectionFlag = 0;
		state.SecondarySelectionFlag = 0;
		state.Count = _fan.Build( input.Velocity, input.SecondaryVelocity, input.Count,
			input.SecondarySpeed, input.Spread, input.SpreadScale, state.Directions, out var height );
		state.Height = height;
		PrepareOrigin( input, state );
		state.Duration = Duration( state.Directions[ 0 ].Y );
		if ( input.AllowReuse && CanReuse( input, state ) )
		{
			state.RetainedOffset = AddScaled( state.Directions[ 0 ], input.LeadTime, state.Origin ) - input.RetainedTrajectory.Position;
			state.RetainedAxis = input.RetainedAxis;
			state.Special = true;
			state.Count = 1;
			Submit( input, state, service, 0, input.RetainedTrajectory.Position,
				input.RetainedTrajectory.Velocity, input.RetainedTrajectory.Acceleration, 1f, 1f );
		}
		else
		{
			for ( var i = 0; i < state.Count; i++ )
			{
				var position = AddScaled( state.Directions[ i ], input.LeadTime, state.Origin );
				Submit( input, state, service, i, position, state.Directions[ i ], input.Acceleration,
					_settings.FirstStep, _settings.LaterStep );
			}
		}
		state.Ready = true;
	}

	private void PrepareOrigin( GrindQueryRequestInput input, GrindQueryRequestState state )
	{
		var tilt = _tiltCurve.EvaluateOrdered( MathF.Abs( input.ExternalUp.Y ) );
		var velocity = _velocityCurve.EvaluateOrdered( input.ExternalVelocity.Y * _settings.VelocityScale );
		var blend = velocity - tilt >= 0f ? velocity : tilt;
		var projected = Dot( Spatial( input.Point - input.ExternalReference ), Spatial( input.Up ) ) - _settings.Radius;
		var limited = projected - _settings.MaximumOffset >= 0f ? _settings.MaximumOffset : projected;
		var offset = MathF.FusedMultiplyAdd( blend, _settings.MaximumOffset, limited * (1f - blend) );
		state.Origin = input.Point - AddScaled( input.Up, offset, Vector4.Zero );
		state.Reference = input.Reference;
		if ( input.Override )
		{
			state.Origin = input.OverridePosition;
			state.Reference = input.OverrideReference;
		}
		// Local offsets still use the unmodified reference points after an override.
		state.LocalReference = Transform( input.LocalFrame, input.Reference - state.Origin );
		state.BoardOffset = input.Point - state.Origin;
		state.LocalBoardOffset = Transform( input.LocalFrame, state.BoardOffset );
	}

	private float Duration( float verticalVelocity )
	{
		var discriminant = MathF.FusedMultiplyAdd( verticalVelocity, verticalVelocity,
			_settings.PredictionHeight * _settings.GravityFactor );
		var duration = MultiplyAdd( _settings.InverseGravity, -verticalVelocity - SquareRoot( discriminant ), 0f );
		return duration - _settings.MaximumDuration >= 0f ? _settings.MaximumDuration : duration;
	}

	private bool CanReuse( GrindQueryRequestInput input, GrindQueryRequestState state )
	{
		var offset = Spatial( AddScaled( state.Directions[ 0 ], input.LeadTime, state.Origin ) - input.RetainedTrajectory.Position );
		if ( !(Dot( offset, offset ) < _settings.ReuseDistanceSquared) ) return false;
		var velocity = Spatial( state.Directions[ 0 ] );
		var retained = Spatial( input.RetainedTrajectory.Velocity );
		if ( 0f > Dot( velocity, retained ) ) return false;
		var speed = SquareRoot( Dot( velocity, velocity ) );
		if ( speed <= _settings.ReuseMinimumSpeed ) return true;
		var ratio = SquareRoot( Dot( retained, retained ) ) / speed;
		return !(ratio < _settings.ReuseMinimumRatio) && ratio <= _settings.ReuseMaximumRatio;
	}

	private void Submit( GrindQueryRequestInput input, GrindQueryRequestState state, TrajectoryQueryService service,
		int index, Vector4 position, Vector4 velocity, Vector4 acceleration, float first, float later )
	{
		var request = new TrajectoryQueryRequest( Spatial( position ), Spatial( velocity ), Spatial( acceleration ),
			state.Duration, _settings.Radius, first, later, input.Owner, 0, new( 0, 0, 0, 0, true, true ) )
		{
			PositionAuxiliary = position.W,
			VelocityAuxiliary = velocity.W,
			AccelerationAuxiliary = acceleration.W
		};
		service.Submit( state.Targets[ index ], request, input.Queue, 0 );
	}

	private static Vector4 Transform( Matrix4x4 frame, Vector4 point )
	{
		var result = AddScaled( new( frame.M11, frame.M12, frame.M13, frame.M14 ), point.X,
			new( frame.M41, frame.M42, frame.M43, frame.M44 ) );
		result = AddScaled( new( frame.M21, frame.M22, frame.M23, frame.M24 ), point.Y, result );
		return AddScaled( new( frame.M31, frame.M32, frame.M33, frame.M34 ), point.Z, result );
	}

	private static Vector3 Spatial( Vector4 value ) => new( value.X, value.Y, value.Z );

	private static Vector4 AddScaled( Vector4 value, float scale, Vector4 offset ) => new(
		MultiplyAdd( value.X, scale, offset.X ), MultiplyAdd( value.Y, scale, offset.Y ),
		MultiplyAdd( value.Z, scale, offset.Z ), MultiplyAdd( value.W, scale, offset.W ) );
}
