namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;
using static LimbSolverMath;

/// <summary>Builds an oriented hand reach region from speed curves and predicted apex positions.</summary>
internal sealed class PhysicalHandReachBuilder
{
	private readonly GrindHeightCorrectionCurve _minimumX;
	private readonly GrindHeightCorrectionCurve _maximumX;
	private readonly GrindHeightCorrectionCurve _minimumY;
	private readonly GrindHeightCorrectionCurve _decreaseY;
	private readonly GrindHeightCorrectionCurve _increaseY;
	private readonly GrindHeightCorrectionCurve _decreaseX;
	private readonly GrindHeightCorrectionCurve _increaseX;
	private readonly float _heightOffset;

	internal PhysicalHandReachBuilder( GrindHeightCorrectionCurve minimumX, GrindHeightCorrectionCurve maximumX,
		GrindHeightCorrectionCurve minimumY, GrindHeightCorrectionCurve decreaseY,
		GrindHeightCorrectionCurve increaseY, GrindHeightCorrectionCurve decreaseX,
		GrindHeightCorrectionCurve increaseX, float heightOffset )
	{
		ArgumentNullException.ThrowIfNull( minimumX );
		ArgumentNullException.ThrowIfNull( maximumX );
		ArgumentNullException.ThrowIfNull( minimumY );
		ArgumentNullException.ThrowIfNull( decreaseY );
		ArgumentNullException.ThrowIfNull( increaseY );
		ArgumentNullException.ThrowIfNull( decreaseX );
		ArgumentNullException.ThrowIfNull( increaseX );
		_minimumX = minimumX;
		_maximumX = maximumX;
		_minimumY = minimumY;
		_decreaseY = decreaseY;
		_increaseY = increaseY;
		_decreaseX = decreaseX;
		_increaseX = increaseX;
		_heightOffset = heightOffset;
	}

	internal PhysicalHandReachRegion Build( Vector3 origin, Vector3 velocity ) => BuildShaped( origin, ShapeMotion( velocity ) );

	internal static Vector3 ShapeMotion( Vector3 velocity )
	{
		var speed = SquareRoot( MultiplyAdd( velocity.X, velocity.X, 0f ) + MultiplyAdd( velocity.Y, velocity.Y, 0f ) );
		return new Vector3( velocity.X, speed, 0f );
	}

	internal PhysicalHandReachRegion BuildShaped( Vector3 origin, Vector3 motion )
	{
		var speed = motion.Y;
		var minimumX = _minimumX.Evaluate( speed );
		var maximumX = _maximumX.Evaluate( speed );
		var minimumY = _minimumY.Evaluate( speed );
		var lowerX = MathF.Abs( motion.X ) - _decreaseX.Evaluate( speed );
		lowerX = minimumX > lowerX ? minimumX : lowerX;
		var upperX = MathF.Abs( motion.X ) + _increaseX.Evaluate( speed );
		upperX = maximumX < upperX ? maximumX : upperX;
		var lowerY = speed - _decreaseY.Evaluate( speed );
		lowerY = minimumY > lowerY ? minimumY : lowerY;
		var upperY = _increaseY.Evaluate( speed ) + speed;
		var valid = upperX > lowerX;
		if ( !(motion.X > 0f) )
		{
			lowerX = -lowerX;
			upperX = -upperX;
		}
		var offset = new Vector3( 0f, _heightOffset, 0f );
		return new PhysicalHandReachRegion( valid, motion,
			Apex( origin, new Vector3( lowerX, lowerY, 0f ) ) - offset,
			Apex( origin, new Vector3( upperX, lowerY, 0f ) ) - offset,
			Apex( origin, new Vector3( lowerX, upperY, 0f ) ) - offset,
			Apex( origin, new Vector3( upperX, upperY, 0f ) ) - offset );
	}

	private static Vector3 Apex( Vector3 origin, Vector3 velocity )
	{
		if ( 0f > velocity.Y )
			return origin;
		var time = MultiplyAdd( Reciprocal( 9.800000190734863f ), velocity.Y, 0f );
		var linear = AddScaled( velocity, time, origin );
		var squared = MultiplyAdd( time, time, 0f );
		return AddScaled( new Vector3( 0f, -4.900000095367432f, 0f ), squared, linear );
	}
}
