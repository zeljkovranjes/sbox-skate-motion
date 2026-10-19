namespace SkateMotion.Core.Input;

internal sealed partial class ControllerActionRouter
{
	private void EmitRiding( RidingButtonActions buttons, RidingAnalogActions analog )
	{
		Emit( _exitGrind, buttons.ExitGrind );
		Emit( _leftGroundGrab, buttons.LeftGroundGrab );
		Emit( _rightGroundGrab, buttons.RightGroundGrab );
		Emit( _leftAirGrab, buttons.LeftAirGrab );
		Emit( _rightAirGrab, buttons.RightAirGrab );
		Emit( _brake, buttons.Brake );
		Emit( _newPush, buttons.NewPush );
		Emit( _rightPush, buttons.RightPush );
		Emit( _pushing, buttons.RightPush );
		Emit( _leftPush, buttons.LeftPush );
		Emit( _pushing, buttons.LeftPush );
		EmitAxis( _bodySpin, analog.BodySpin );
		EmitAxis( _physicalBodySpin, analog.BodySpin );
		Emit( _tweakX, analog.HasTweak, analog.TweakX );
		Emit( _tweakY, analog.HasTweak, analog.TweakY );
		Emit( _boardAngle, analog.HasBoardAdjust, analog.BoardAdjustAngle );
		Emit( _boardMagnitude, analog.HasBoardAdjust, analog.BoardAdjustMagnitude );
		EmitAxis( _kickTurn, analog.KickTurn );
		EmitAxis( _grindBalance, analog.GrindBalance );
		Emit( _anticipationMagnitude, analog.HasAnticipation, analog.AnticipationMagnitude );
		Emit( _anticipationAngle, analog.HasAnticipation, analog.AnticipationAngle );
		EmitAxis( _grindTranslation, analog.GrindTranslation );
		EmitAxis( _grindStability, analog.GrindStabilityNudge );
		EmitAxis( _grindUpDown, analog.GrindUpDown );
		Emit( _pumpAngle, analog.HasAutoPump, analog.AutoPumpAngle );
		Emit( _pumpMagnitude, analog.HasAutoPump, analog.AutoPumpMagnitude );
		Emit( _noInput, buttons.NoInput );
		Emit( _hardTurnCrouch, analog.HasHardTurn, analog.HardTurnCrouch );
		Emit( _hardTurn, analog.HasHardTurn, analog.HardTurn );
		Emit( _crouch, analog.HasCrouch, analog.Crouch );
		Emit( _darkCatch, buttons.DarkCatch );
		Emit( _newDarkCatch, buttons.NewDarkCatch );
		EmitAxis( _turn, analog.Turn );
		Emit( _rightSlideStart, analog.RightSlideStart );
		Emit( _leftSlideStart, analog.LeftSlideStart );
		EmitAxis( _leftSlide, analog.LeftSlide );
		EmitAxis( _rightSlide, analog.RightSlide );
		Emit( _grabWorld, buttons.GrabWorld );
		Emit( _newToggle, buttons.NewToggleOffBoard );
		Emit( _toggle, buttons.ToggleOffBoard );
		Emit( _sprint, buttons.Sprint );
	}
}
