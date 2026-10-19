namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;
using static LimbSolverMath;

/// <summary>Checks source speed, upward motion and the permitted surface-angle interval.</summary>
internal sealed class PhysicalHandSourceEligibility
{
	private readonly LimbArcSine _arcSine;
	private readonly float _minimumSpeed;
	private readonly float _minimumAngle;

	internal PhysicalHandSourceEligibility( LimbArcSine arcSine, float minimumSpeed, float minimumAngle )
	{
		ArgumentNullException.ThrowIfNull( arcSine );
		_arcSine = arcSine;
		_minimumSpeed = minimumSpeed;
		_minimumAngle = minimumAngle;
	}

	internal bool Allows( Vector3 velocity, Vector3 normal )
	{
		var cosine = Math.Clamp( normal.Y, -1f, 1f );
		var angle = 90f - MultiplyAdd( _arcSine.Evaluate( cosine ), 57.295780181884766f, 0f );
		return SquareRoot( Dot( velocity, velocity ) ) > _minimumSpeed
			&& velocity.Y > 0f && angle > _minimumAngle
			&& !(MathF.Abs( normal.Y ) > .949999988079071f);
	}
}
