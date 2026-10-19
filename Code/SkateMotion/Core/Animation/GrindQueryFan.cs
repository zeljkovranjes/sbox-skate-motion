namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;
using Vector4 = System.Numerics.Vector4;
using static LimbSolverMath;

/// <summary>Builds bounded velocity directions around a retained first query.</summary>
internal sealed class GrindQueryFan
{
	private readonly LimbSolverMath _math;
	private readonly GrindEdgeSelectionCurve _spreadCurve;
	private readonly GrindQueryFanSettings _settings;

	internal GrindQueryFan( LimbSolverMath math, GrindEdgeSelectionCurve spreadCurve, GrindQueryFanSettings settings )
	{
		ArgumentNullException.ThrowIfNull( math );
		ArgumentNullException.ThrowIfNull( spreadCurve );
		_math = math;
		_spreadCurve = spreadCurve;
		_settings = settings;
	}

	internal int Build( Vector4 velocity, Vector4 secondaryVelocity, ushort requestedCount, bool useSecondarySpeed,
		float spreadDegrees, float spreadScale, Vector4[] directions, out float height )
	{
		ArgumentNullException.ThrowIfNull( directions );
		var count = Math.Min( (int)requestedCount, 7 );
		if ( directions.Length < Math.Max( count, 1 ) )
			throw new ArgumentException( "Storage must include the retained first direction and every requested slot.", nameof( directions ) );
		height = _settings.InitialHeight;
		directions[ 0 ] = velocity;
		var speed = Length( velocity );
		var spreadSpeed = useSecondarySpeed ? Length( secondaryVelocity ) : speed;
		var side = NormalizeOrZero( Cross( Vector3.UnitY, Spatial( velocity ) ) );
		var forward = NormalizeOrZero( Cross( side, Vector3.UnitY ) );
		var spread = MultiplyAdd( _settings.DegreesToRadians, spreadDegrees, 0f );
		var curvedSpread = _spreadCurve.EvaluateOrdered( spreadSpeed * _settings.SpeedScale ) * spreadScale;
		curvedSpread = MultiplyAdd( _settings.DegreesToRadians, curvedSpread, 0f );
		var magnitude = MathF.Min( _settings.MaximumSpreadSpeed, MathF.Max( _settings.MinimumSpreadSpeed, spreadSpeed ) );
		var lateral = Scale( side, MultiplyAdd( _math.SinCos( spread ).Sine, magnitude, 0f ) );
		var longitudinal = Scale( forward, MultiplyAdd( _math.SinCos( curvedSpread ).Sine, magnitude, 0f ) );
		if ( count <= 1 )
			return count;
		var step = _settings.FullCircle / (count - 1);
		var angle = 0f;
		for ( var i = 1; i < count; i++ )
		{
			var (sine, cosine) = _math.SinCos( angle );
			var direction = AddScaled( new Vector4( lateral, 0f ), sine, velocity );
			direction = AddScaled( new Vector4( longitudinal, 0f ), cosine, direction );
			var squared = Dot( Spatial( direction ), Spatial( direction ) );
			var inverse = InverseSquareRoot( squared );
			var length = squared == 0f ? 0f : MultiplyAdd( squared, inverse, 0f );
			var normalized = length > _settings.MinimumLength ? AddScaled( direction, inverse, Vector4.Zero ) : Vector4.Zero;
			directions[ i ] = AddScaled( normalized, MathF.Min( length, speed ), Vector4.Zero );
			angle += step;
		}
		return count;
	}

	private Vector3 NormalizeOrZero( Vector3 value )
	{
		var squared = Dot( value, value );
		var inverse = InverseSquareRoot( squared );
		var length = squared == 0f ? 0f : MultiplyAdd( squared, inverse, 0f );
		return length > _settings.MinimumLength ? Scale( value, inverse ) : Vector3.Zero;
	}

	private static float Length( Vector4 value ) => SquareRoot( Dot( Spatial( value ), Spatial( value ) ) );

	private static Vector3 Spatial( Vector4 value ) => new( value.X, value.Y, value.Z );

	private static Vector4 AddScaled( Vector4 value, float scale, Vector4 offset ) => new(
		MultiplyAdd( value.X, scale, offset.X ), MultiplyAdd( value.Y, scale, offset.Y ),
		MultiplyAdd( value.Z, scale, offset.Z ), MultiplyAdd( value.W, scale, offset.W ) );
}
