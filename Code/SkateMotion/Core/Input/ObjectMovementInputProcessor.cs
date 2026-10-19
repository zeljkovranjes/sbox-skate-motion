using SkateMotion.Core.Animation;

namespace SkateMotion.Core.Input;

/// <summary>Combines wrapped stick direction, response curves and direct rotation input.</summary>
internal sealed class ObjectMovementInputProcessor
{
	private readonly LimbArcTangent _arcTangent;
	private readonly InputResponseCurve _rotation;
	private readonly InputResponseCurve _sideways;
	private readonly InputResponseCurve _forward;
	private readonly float _inverseTurn;
	private readonly float _turn;
	private readonly float _curveScale;

	internal ObjectMovementInputProcessor( LimbArcTangent arcTangent,
		InputResponseCurve rotation, InputResponseCurve sideways, InputResponseCurve forward,
		float inverseTurn, float turn, float curveScale )
	{
		ArgumentNullException.ThrowIfNull( arcTangent );
		ArgumentNullException.ThrowIfNull( rotation );
		ArgumentNullException.ThrowIfNull( sideways );
		ArgumentNullException.ThrowIfNull( forward );
		_arcTangent = arcTangent;
		_rotation = rotation;
		_sideways = sideways;
		_forward = forward;
		_inverseTurn = inverseTurn;
		_turn = turn;
		_curveScale = curveScale;
	}

	internal ObjectMovementInputActions Evaluate( ControllerReading current, bool hasLeftStickInput, float leftMagnitude )
	{
		var rotation = 0f;
		var sideways = 0f;
		var forward = 0f;
		if ( hasLeftStickInput )
		{
			var angle = _arcTangent.Evaluate( current.LeftX, -current.LeftY );
			var turns = LimbSolverMath.MultiplyAdd( angle, _inverseTurn, 0f );
			var wrapped = turns - MathF.Floor( turns );
			wrapped -= wrapped > .5f ? 1f : 0f;
			angle = LimbSolverMath.MultiplyAdd( wrapped, _turn, 0f );
			var curveInput = MathF.Abs( angle ) * _curveScale;
			rotation = _rotation.Evaluate( curveInput ) * leftMagnitude;
			rotation *= angle >= 0f ? 1f : -1f;
			sideways = _sideways.Evaluate( curveInput ) * current.LeftX;
			forward = _forward.Evaluate( curveInput ) * current.LeftY;
		}
		rotation += current.RightX;
		return new( sideways, forward, Math.Clamp( rotation, -1f, 1f ) );
	}
}
