namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;
using Vector4 = System.Numerics.Vector4;
using static LimbSolverMath;

/// <summary>Reduces retained board orientation through an angular response curve.</summary>
internal sealed class BoardFrameCorrection
{
	private readonly LimbSolverMath _math;
	private readonly BoardCorrectionCurve _curve;
	private readonly BoardCorrectionCurve _alternateCurve;
	private readonly BoardFrameCorrectionSettings _settings;

	internal BoardFrameCorrection( LimbSolverMath math, BoardCorrectionCurve curve,
		BoardCorrectionCurve alternateCurve, BoardFrameCorrectionSettings settings )
	{
		ArgumentNullException.ThrowIfNull( math );
		ArgumentNullException.ThrowIfNull( curve );
		ArgumentNullException.ThrowIfNull( alternateCurve );
		_math = math;
		_curve = curve;
		_alternateCurve = alternateCurve;
		_settings = settings;
	}

	internal BoardFrameCorrectionResult Update( LimbRigidTransform frame, Vector4 orientation,
		byte correctionFlag, bool alternate )
	{
		var angle = MultiplyAdd( _math.ArcCosine( orientation.W ), 2f, 0f );
		var turns = MultiplyAdd( angle, _settings.InverseFullTurn, 0f );
		turns -= MathF.Floor( turns );
		turns -= turns > .5f ? 1f : 0f;
		angle = MathF.Abs( MultiplyAdd( turns, _settings.FullTurn, 0f ) );
		var weight = (alternate ? _alternateCurve : _curve).Evaluate( angle );
		if ( weight >= _settings.ReleaseWeight )
			return new BoardFrameCorrectionResult( frame, orientation, 0 );

		orientation = BlendIdentity( orientation, weight );
		var increment = Rotation( orientation );
		var rotation = new LimbFrame(
			frame.Rotation.Transform( increment.X, Vector3.Zero ),
			frame.Rotation.Transform( increment.Y, Vector3.Zero ),
			frame.Rotation.Transform( increment.Z, Vector3.Zero ) );
		return new BoardFrameCorrectionResult( new( rotation, frame.Translation ), orientation, correctionFlag );
	}

	private Vector4 BlendIdentity( Vector4 orientation, float weight )
	{
		var cosine = orientation.W;
		if ( cosine < 0f )
			orientation = -orientation;
		cosine = MathF.Abs( cosine );
		var target = new Vector4( 0f, 0f, 0f, 1f );
		if ( cosine > _settings.LinearCosineThreshold )
		{
			var blended = cosine > 0f
				? AddScaled( target - orientation, weight, orientation )
				: SubtractScaled( target + orientation, weight, orientation );
			var xx = MultiplyAdd( blended.X, blended.X, 0f );
			var yy = MultiplyAdd( blended.Y, blended.Y, 0f );
			var zz = MultiplyAdd( blended.Z, blended.Z, 0f );
			var ww = MultiplyAdd( blended.W, blended.W, 0f );
			return AddScaled( blended, InverseSquareRoot( (xx + zz) + (yy + ww) ), Vector4.Zero );
		}

		var theta = _math.ArcCosine( cosine );
		var first = _math.SinCos( MultiplyAdd( 1f - weight, theta, 0f ) ).Sine;
		var second = _math.SinCos( MultiplyAdd( weight, theta, 0f ) ).Sine;
		var inverse = Reciprocal( _math.SinCos( theta ).Sine );
		first = MultiplyAdd( inverse, first, 0f );
		second = MultiplyAdd( second, inverse, 0f );
		return AddScaled( target, second, AddScaled( orientation, first, Vector4.Zero ) );
	}

	private LimbFrame Rotation( Vector4 orientation )
	{
		var scaled = AddScaled( orientation, _settings.QuaternionMatrixScale, Vector4.Zero );
		var xx = SubtractProduct( scaled.X, scaled.X, .5f );
		var yy = SubtractProduct( scaled.Y, scaled.Y, .5f );
		var zz = SubtractProduct( scaled.Z, scaled.Z, .5f );
		var xy = MultiplyAdd( scaled.X, scaled.Y, 0f );
		var xz = MultiplyAdd( scaled.X, scaled.Z, 0f );
		var yz = MultiplyAdd( scaled.Y, scaled.Z, 0f );
		var wx = MultiplyAdd( scaled.W, scaled.X, 0f );
		var wy = MultiplyAdd( scaled.W, scaled.Y, 0f );
		var wz = MultiplyAdd( scaled.W, scaled.Z, 0f );
		return new LimbFrame( new( yy + zz, xy + wz, xz - wy ),
			new( xy - wz, xx + zz, yz + wx ), new( xz + wy, yz - wx, xx + yy ) );
	}

	private static Vector4 AddScaled( Vector4 value, float scale, Vector4 offset ) => new(
		MultiplyAdd( value.X, scale, offset.X ), MultiplyAdd( value.Y, scale, offset.Y ),
		MultiplyAdd( value.Z, scale, offset.Z ), MultiplyAdd( value.W, scale, offset.W ) );

	private static Vector4 SubtractScaled( Vector4 value, float scale, Vector4 offset ) => new(
		SubtractProduct( value.X, scale, offset.X ), SubtractProduct( value.Y, scale, offset.Y ),
		SubtractProduct( value.Z, scale, offset.Z ), SubtractProduct( value.W, scale, offset.W ) );
}
