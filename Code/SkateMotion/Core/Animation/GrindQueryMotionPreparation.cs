namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;
using Vector4 = System.Numerics.Vector4;
using static LimbSolverMath;

/// <summary>Conditions incoming velocity using the current up vector, orientation and movement gates.</summary>
internal sealed class GrindQueryMotionPreparation
{
	private readonly GrindQueryMotionSettings _settings;
	private const int ProjectionBypassState = 200;

	internal GrindQueryMotionPreparation( GrindQueryMotionSettings settings ) => _settings = settings;

	internal Vector4 Prepare( Vector4 velocity, Vector4 up, float orientation,
		bool secondarySpeed, int movementState, out bool special )
	{
		special = false;
		if ( up.Y >= _settings.MinimumUpY ) return velocity;
		var projection = ScaleVector( up, Dot( Spatial( velocity ), Spatial( up ) ) );
		var vertical = new Vector4( projection.X, velocity.Y, projection.Z, projection.W );
		var verticalDirection = Normalize( vertical, out var verticalSpeed );
		if ( _settings.MinimumDirectionY < verticalDirection.Y )
		{
			special = true;
			var amount = orientation - _settings.OrientationOffset;
			amount = _settings.MinimumOrientation - amount >= 0f ? _settings.MinimumOrientation : amount;
			amount = _settings.MaximumOrientation - amount >= 0f ? amount : _settings.MaximumOrientation;
			var tilt = ScaleVector( ScaleVector( up, amount ), _settings.TiltGain );
			var direction = Normalize( Vector4.UnitY - tilt, out _ );
			var corrected = AddScaled( direction, verticalSpeed, velocity - vertical );
			var incoming = Normalize( velocity, out var speed );
			var blended = AddScaled( corrected, _settings.Blend, ScaleVector( incoming, 1f - _settings.Blend ) );
			return ScaleVector( Normalize( blended, out _ ), speed );
		}
		if ( secondarySpeed || movementState == ProjectionBypassState ) return velocity;
		return AddScaled( projection, _settings.NormalVelocityScale, velocity - projection );
	}

	private Vector4 Normalize( Vector4 value, out float length )
	{
		var squared = Dot( Spatial( value ), Spatial( value ) );
		var inverse = InverseSquareRoot( squared );
		length = squared == 0f ? 0f : MultiplyAdd( squared, inverse, 0f );
		return length > _settings.MinimumLength ? ScaleVector( value, inverse ) : Vector4.Zero;
	}

	private static Vector3 Spatial( Vector4 value ) => new( value.X, value.Y, value.Z );

	private static Vector4 ScaleVector( Vector4 value, float scale ) => AddScaled( value, scale, Vector4.Zero );

	private static Vector4 AddScaled( Vector4 value, float scale, Vector4 offset ) => new(
		MultiplyAdd( value.X, scale, offset.X ), MultiplyAdd( value.Y, scale, offset.Y ),
		MultiplyAdd( value.Z, scale, offset.Z ), MultiplyAdd( value.W, scale, offset.W ) );
}
