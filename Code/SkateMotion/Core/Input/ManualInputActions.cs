using SkateMotion.Core.Animation;

namespace SkateMotion.Core.Input;

/// <summary>Independent balance and braking requests from right-stick pressure.</summary>
internal readonly record struct ManualInputActions( bool HasBalance, float Balance, bool HasBrake, float Brake )
{
	internal static ManualInputActions Evaluate( ControllerReading current, bool suppressed,
		float brakeThreshold, float brakeScale )
	{
		if ( suppressed )
			return default;
		var squaredY = current.RightY * current.RightY;
		var magnitude = LimbSolverMath.SquareRoot(
			LimbSolverMath.MultiplyAdd( current.RightX, current.RightX, squaredY ) );
		var hasBalance = current.RightY != 0f;
		var balance = current.RightY > 0f ? magnitude : current.RightY < 0f ? -magnitude : 0f;
		var hasBrake = magnitude > brakeThreshold;
		var brake = hasBrake ? (magnitude - brakeThreshold) * brakeScale : 0f;
		// A horizontal stick can request braking without a balance request.
		if ( current.RightY <= 0f )
			brake = -brake;
		return new( hasBalance, balance, hasBrake, brake );
	}
}
