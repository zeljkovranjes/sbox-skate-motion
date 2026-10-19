namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;
using Vector4 = System.Numerics.Vector4;
using static LimbSolverMath;

/// <summary>Orders direction filtering, frame production, lateral tilt and inactive steering decay.</summary>
internal sealed class PhysicalDirectionController
{
	private readonly LimbSolverMath _math;
	private readonly LimbSignedAngle _angles;
	private readonly LimbArcSine _arcSine;
	private readonly BoardCorrectionCurve _lateralCurve;
	private readonly BoardCorrectionCurve _upCurve;
	private readonly Vector4 _filterCoefficients;

	internal PhysicalDirectionController( LimbSolverMath math, LimbSignedAngle angles, LimbArcSine arcSine,
		BoardCorrectionCurve lateralCurve, BoardCorrectionCurve upCurve, Vector4 filterCoefficients )
	{
		ArgumentNullException.ThrowIfNull( math );
		ArgumentNullException.ThrowIfNull( angles );
		ArgumentNullException.ThrowIfNull( arcSine );
		ArgumentNullException.ThrowIfNull( lateralCurve );
		ArgumentNullException.ThrowIfNull( upCurve );
		_math = math;
		_angles = angles;
		_arcSine = arcSine;
		_lateralCurve = lateralCurve;
		_upCurve = upCurve;
		_filterCoefficients = filterCoefficients;
	}

	internal void Update( PhysicalDirectionState state, PhysicalDirectionInput input )
	{
		ArgumentNullException.ThrowIfNull( state );
		state.PreviousDirection = state.Direction;
		state.FirstRate = 0f;
		state.SecondRate = 0f;
		state.DirectionDelta = Vector3.Zero;
		state.PrimaryFilter.Coefficients = _filterCoefficients;
		state.FilteredDirection = Normalize( PhysicalDirectionFilter.Update( state.PrimaryFilter, input.Direction ) );
		state.PrimaryFilter.Value = state.FilteredDirection;
		var blended = Scale( state.Direction, input.Blend ) + Scale( input.Direction, 1f - input.Blend );
		var squared = Dot( blended, blended );
		if ( SquareRoot( squared ) > .000001f )
			state.Direction = Scale( blended, InverseSquareRoot( squared ) );
		state.PublishedDirection = state.Direction;
		PhysicalDirectionFilter.Update( state.FirstFilter, state.Direction );
		PhysicalDirectionFilter.Update( state.SecondFilter, state.Direction );
		state.FirstFilter.Value = state.Direction;
		state.SecondFilter.Value = state.Direction;
		ProduceFrames( state, input.SecondaryDirection );
		UpdateTilt( state, input.Mirrored );
		DecayControl( state, input.UseControl ? input.Control : 0f );
	}

	private static void ProduceFrames( PhysicalDirectionState state, Vector3 secondary )
	{
		var side = Cross( state.Direction, secondary );
		secondary = Normalize( Cross( side, state.Direction ) );
		side = Normalize( side );
		state.SecondaryDirection = secondary;
		state.LocalFrame = new( new( side, state.Direction, secondary ), state.WorldFrame.Translation );
		state.WorldFrame = state.SourceFrame.Compose( state.LocalFrame );
		state.InverseFrame = state.WorldFrame.RigidInverse();
		side = Cross( state.FilteredDirection, secondary );
		secondary = Normalize( Cross( side, state.FilteredDirection ) );
		side = Normalize( side );
		state.FilteredFrame = new( new( side, state.FilteredDirection, secondary ), state.FilteredFrame.Translation );
	}

	private void UpdateTilt( PhysicalDirectionState state, bool mirrored )
	{
		var up = state.LocalFrame.Rotation.Y;
		var projection = Scale( up, Dot( up, Vector3.UnitY ) );
		var lateral = Vector3.UnitY - projection;
		if ( Dot( lateral, lateral ) <= .0005f )
			return;
		var halfPi = _math.Pi * .5f;
		var angle = projection == Vector3.UnitY ? -halfPi
			: _angles.Evaluate( state.LocalFrame.Rotation.Z, Normalize( lateral ), up ) - halfPi;
		var turns = MultiplyAdd( angle, 1f / (_math.Pi * 2f), 0f );
		var fraction = turns - MathF.Floor( turns );
		if ( fraction > .5f )
			fraction -= 1f;
		angle = MultiplyAdd( fraction, _math.Pi * 2f, 0f );
		var signed = (halfPi - MathF.Abs( angle )) * (2f / _math.Pi);
		if ( mirrored )
			signed = -signed;
		var upAngle = MultiplyAdd( halfPi - _arcSine.Evaluate( up.Y ), 2f / _math.Pi, 0f );
		var magnitude = _lateralCurve.Evaluate( MathF.Abs( signed ) );
		state.Tilt = new( _upCurve.Evaluate( MathF.Abs( upAngle ) ) * (signed >= 0f ? magnitude : -magnitude), 0f, 0f );
	}

	private static void DecayControl( PhysicalDirectionState state, float control )
	{
		state.ControlDelta = (control - state.Control) * 1.5f;
		state.Control = control;
		state.SecondaryControl = 0f;
		state.AlternativeCurve = false;
		var response = 0f;
		if ( control * state.ControlDelta > .1f )
			response = -1f - state.ControlDelta >= 0f ? -1f : state.ControlDelta;
		state.ControlResponse = 1f - response >= 0f ? response : 1f;
		state.ControlHistory[ state.HistoryIndex ] = state.ControlResponse;
		state.HistoryIndex = (state.HistoryIndex + 1) % 30;
		state.SteeringVelocity = 0f;
		state.SteeringTarget = 0f;
		state.SteeringMagnitude = 0f;
		state.SmoothedControl = MultiplyAdd( state.SmoothedControl, .8f, control * .2f );
	}
}
