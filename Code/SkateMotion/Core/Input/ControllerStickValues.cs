using SkateMotion.Core.Animation;

namespace SkateMotion.Core.Input;

/// <summary>Independent stick presence, direction and radial pressure for action routing.</summary>
internal readonly record struct ControllerStickValues(
	bool HasLeft, float LeftAngle, float LeftMagnitude, bool HasRight, float RightAngle, float RightMagnitude )
{
	internal static ControllerStickValues Evaluate( ControllerReading current, LimbArcTangent arcTangent )
	{
		var left = current.LeftX != 0f || current.LeftY != 0f;
		var right = current.RightX != 0f || current.RightY != 0f;
		return new( left, left ? arcTangent.Evaluate( current.LeftX, -current.LeftY ) : 0f,
			left ? Magnitude( current.LeftX, current.LeftY ) : 0f,
			right, right ? arcTangent.Evaluate( current.RightX, -current.RightY ) : 0f,
			right ? Magnitude( current.RightX, current.RightY ) : 0f );
	}

	private static float Magnitude( float x, float y )
	{
		// Action routing rounds Y squared before accumulating X; activity timers use a separate path.
		var squaredY = y * y;
		return LimbSolverMath.SquareRoot( LimbSolverMath.MultiplyAdd( x, x, squaredY ) );
	}
}
