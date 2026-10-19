namespace SkateMotion.Core.Input;

using static Animation.LimbSolverMath;
using Vector3 = System.Numerics.Vector3;

internal readonly record struct WalkingInputActions(
	float Magnitude,
	float WorldX,
	float WorldZ,
	float StickMagnitude )
{
	internal static WalkingInputActions Evaluate( ControllerReading current, WalkingInputContext context )
	{
		var stick = new Vector3( current.LeftX, 0f, current.LeftY );
		var facing = NormalizeOrZero( new Vector3( context.Facing.X, 0f, context.Facing.Z ),
			context.NormalizationMinimum );
		var magnitude = Dot( facing, stick );
		if ( context.ProjectAgainstObstruction )
		{
			var opposite = -context.ObstructionNormal;
			var into = Dot( stick, opposite );
			into = -into >= 0f ? 0f : into;
			into = 1f - into >= 0f ? into : 1f;
			var projected = AddScaled( context.ObstructionNormal, into, stick );
			var direction = NormalizeOrZero( stick, context.NormalizationMinimum );
			magnitude = Dot( direction, opposite ) > context.ObstructionAlignmentLimit
				? 0f : Dot( facing, projected );
		}
		return new( magnitude, current.LeftX, current.LeftY, SquareRoot( Dot( stick, stick ) ) );
	}

	private static Vector3 NormalizeOrZero( Vector3 value, float minimum )
	{
		var squared = Dot( value, value );
		var inverse = InverseSquareRoot( squared );
		var length = squared == 0f ? 0f : MultiplyAdd( squared, inverse, 0f );
		return length > minimum ? Scale( value, inverse ) : Vector3.Zero;
	}
}
