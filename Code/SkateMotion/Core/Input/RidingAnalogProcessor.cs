namespace SkateMotion.Core.Input;

/// <summary>Resolves stick-driven riding requests without merging overlapping action groups.</summary>
internal static class RidingAnalogProcessor
{
	internal static RidingAnalogActions Evaluate( ControllerReading current, ControllerStickValues sticks,
		RidingAnalogContext context, RidingAnalogTuning tuning )
	{
		var angle = sticks.LeftAngle;
		var absoluteAngle = MathF.Abs( angle );
		var fullStick = sticks.LeftMagnitude > tuning.FullStickThreshold;
		var hasHardTurn = fullStick && absoluteAngle >= tuning.HardTurnMinAngle
			&& absoluteAngle <= tuning.HardTurnMaxAngle && !context.SuppressTurn;
		var hardTurn = (absoluteAngle - tuning.HardTurnMaxAngle) * tuning.HardTurnScale;
		if ( hardTurn - tuning.HardTurnMinimum < 0f )
			hardTurn = tuning.HardTurnMinimum;
		if ( angle < 0f )
			hardTurn = -hardTurn;
		var leftSlide = 0f;
		var rightSlide = 0f;
		if ( fullStick && !context.SuppressSlide )
		{
			if ( angle > -tuning.SlideOverlapAngle && angle < tuning.SlideOuterAngle )
				rightSlide = (tuning.SlideOuterAngle - angle) * tuning.SlideScale;
			if ( angle > -tuning.SlideOuterAngle && angle < tuning.SlideOverlapAngle )
				leftSlide = -((angle + tuning.SlideOuterAngle) * tuning.SlideScale);
		}
		var trigger = current.LeftTrigger;
		if ( trigger - current.RightTrigger < 0f )
			trigger = current.RightTrigger;
		return new()
		{
			BodySpin = context.SuppressSpin ? 0f : current.LeftX,
			HasTweak = sticks.HasRight, TweakX = current.RightX, TweakY = current.RightY,
			HasBoardAdjust = sticks.RightMagnitude != 0f,
			BoardAdjustAngle = sticks.RightAngle, BoardAdjustMagnitude = sticks.RightMagnitude,
			KickTurn = context.SuppressTurn ? 0f : current.LeftX,
			GrindBalance = -current.LeftX,
			HasAnticipation = sticks.HasRight, AnticipationAngle = sticks.RightAngle, AnticipationMagnitude = sticks.RightMagnitude,
			GrindTranslation = Math.Clamp( current.LeftX + current.RightX, -1f, 1f ),
			GrindStabilityNudge = current.LeftX, GrindUpDown = current.RightY,
			HasAutoPump = !context.AutoPushActive && sticks.HasLeft,
			AutoPumpAngle = (angle < 0f ? -tuning.Pi : tuning.Pi) - angle,
			AutoPumpMagnitude = sticks.LeftMagnitude,
			HasHardTurn = hasHardTurn, HardTurn = hardTurn,
			HardTurnCrouch = hasHardTurn ? MathF.Abs( hardTurn * tuning.HardTurnCrouchScale ) : 0f,
			HasCrouch = absoluteAngle > tuning.CrouchAngle || current.LeftTrigger > 0f || current.RightTrigger != 0f,
			Crouch = trigger > 0f ? trigger : 0f,
			Turn = context.SuppressTurn ? 0f : Turn( current, sticks, tuning ),
			RightSlideStart = fullStick && !context.SuppressSlide && angle > 0f && angle < tuning.HardTurnMinAngle,
			LeftSlideStart = fullStick && !context.SuppressSlide && angle > -tuning.HardTurnMinAngle && angle < 0f,
			LeftSlide = leftSlide, RightSlide = rightSlide
		};
	}

	private static float Turn( ControllerReading current, ControllerStickValues sticks, RidingAnalogTuning tuning )
	{
		if ( current.LeftY <= 0f )
			return current.LeftX;
		var angle = MathF.Abs( sticks.LeftAngle );
		if ( angle > tuning.TurnZeroAngle )
			return 0f;
		var magnitude = current.LeftX < 0f ? -sticks.LeftMagnitude : sticks.LeftMagnitude;
		return angle > tuning.TurnFullAngle
			? (tuning.TurnZeroAngle - angle) * (magnitude * tuning.TurnScale)
			: magnitude;
	}
}
