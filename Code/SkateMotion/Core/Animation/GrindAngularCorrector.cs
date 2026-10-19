namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;
using static LimbSolverMath;

/// <summary>Accumulates angular alignment after a valid board-edge approach selection.</summary>
internal sealed class GrindAngularCorrector
{
	private readonly LimbSolverMath _math;
	private readonly LimbSignedAngle _angles;
	private readonly GrindAngularSettings _settings;
	private readonly GrindApproachCandidate[] _candidates;

	internal GrindAngularCorrector( LimbSolverMath math, GrindAngularSettings settings, GrindApproachCandidate[] candidates )
	{
		ArgumentNullException.ThrowIfNull( math );
		ArgumentNullException.ThrowIfNull( candidates );
		if ( !Positive( settings.DegreesPerRadian ) || !Positive( settings.RadiansPerDegree )
			|| !Nonnegative( settings.MaximumRate ) || !Nonnegative( settings.RollFactor )
			|| !Nonnegative( settings.NormalizationThreshold ) || candidates.Length != 7 )
			throw new ArgumentException( "Angular settings need positive unit scales, nonnegative limits and seven candidates." );
		foreach ( var candidate in candidates )
		{
			if ( !float.IsFinite( candidate.TargetAngle ) || !float.IsFinite( candidate.ResponseFactor ) )
				throw new ArgumentException( "Angular candidate targets and response factors must be finite." );
		}
		_math = math;
		_settings = settings;
		_angles = new LimbSignedAngle( math, settings.MinimumSquaredLength, settings.Period, settings.InversePeriod );
		_candidates = candidates.AsSpan().ToArray();
	}

	internal void Update( LimbFrame board, LimbFrame edgeMetadata, GrindApproachState state )
	{
		ArgumentNullException.ThrowIfNull( state );
		if ( !state.HasCandidate )
			return;
		var candidate = _candidates[ state.SelectedCandidate ];
		var crossing = state.Crossings[ state.SelectedPoint ];
		var previous = state.AngularVelocity;
		var target = state.SelectedAngle > 0f ? candidate.TargetAngle : -candidate.TargetAngle;
		var error = (target - state.SelectedAngle) / crossing;
		state.YawError = crossing * error;
		var yaw = ClampRate( candidate.ResponseFactor * error, _settings.MaximumRate );
		yaw = ClampRate( yaw + MultiplyAdd( _settings.DegreesPerRadian, previous.Y, 0f ), _settings.MaximumRate );
		var y = MultiplyAdd( _settings.RadiansPerDegree, yaw, 0f );
		var pitch = Pitch( crossing, edgeMetadata, state );
		state.PitchAngle = MultiplyAdd( _settings.DegreesPerRadian, pitch, 0f );
		var x = ClampRate( previous.X + pitch / crossing,
			MultiplyAdd( _settings.RadiansPerDegree, _settings.MaximumRate, 0f ) );
		// Roll replaces its previous rate; only the final angular position accumulates it.
		var z = state.SelectedCandidate is 0 or 1 or 2 or 3 ? Roll( board, state.EdgeFrame ) / crossing : 0f;
		state.AngularVelocity = new Vector3( x, y, z );
		state.AngularPosition += state.AngularVelocity;
	}

	private float Pitch( float crossing, LimbFrame metadata, GrindApproachState state )
	{
		var sample = (int)(crossing + .5f) * GrindApproachState.PointCount;
		var direction = state.Points[ sample + 1 ] - state.Points[ sample + 4 ];
		var along = state.EdgeFrame.Z;
		direction = NormalizeGuarded( direction - Scale( along, Dot( along, direction ) ) );
		if ( state.SelectedCandidate is 1 or 2 )
		{
			if ( state.SelectedCandidate == 1 )
				direction = -direction;
			var first = _angles.Wrapped( direction, metadata.Y, along );
			var second = _angles.Wrapped( direction, metadata.Z, along );
			return MathF.Abs( first ) >= MathF.Abs( second ) ? second : first;
		}
		if ( state.SelectedCandidate is not (0 or 3) )
			return 0f;
		var angle = _angles.Wrapped( direction, metadata.X, along );
		if ( angle > _math.Pi * .5f )
			angle -= _math.Pi;
		if ( angle < -_math.Pi * .5f )
			angle += _math.Pi;
		return angle;
	}

	private float Roll( LimbFrame board, LimbFrame edgeFrame )
	{
		var dot = Dot( board.X, edgeFrame.Y );
		// The scalar is subtracted from each component; this is not projection onto edge Y.
		var projected = NormalizeGuarded( board.X - new Vector3( dot ) );
		return Dot( projected, projected ) > _settings.RollFactor
			? _angles.WrappedFraction( board.X, projected, board.Z ) * _settings.RollFactor : 0f;
	}

	private Vector3 NormalizeGuarded( Vector3 value )
	{
		var squared = Dot( value, value );
		var inverse = InverseSquareRoot( squared );
		var length = squared == 0f ? 0f : MultiplyAdd( squared, inverse, 0f );
		return length > _settings.NormalizationThreshold ? Scale( value, inverse ) : Vector3.Zero;
	}

	private static float ClampRate( float value, float maximum ) => Math.Clamp( value, -maximum, maximum );

	private static bool Nonnegative( float value ) => float.IsFinite( value ) && value >= 0f;

	private static bool Positive( float value ) => float.IsFinite( value ) && value > 0f;
}
