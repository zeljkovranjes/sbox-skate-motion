namespace SkateMotion.Core.Input;

internal sealed partial class ControllerActionRouter
{
	private void EmitAuxiliary( ControllerActionContext context )
	{
		var current = State.Current;
		var previous = State.Previous;
		var manual = ManualInputActions.Evaluate( current, context.SuppressManual, Tuning.ManualBrakeThreshold, Tuning.ManualBrakeScale );
		Emit( _manual, manual.HasBalance, manual.Balance );
		Emit( _manualBrake, manual.HasBrake, manual.Brake );
		var plant = HandplantInputActions.Evaluate( current );
		EmitAxis( _handplantX, plant.TweakX );
		EmitAxis( _handplantY, plant.TweakY );
		Emit( _handplantDismount, plant.Dismount );
		Emit( _handplantRightFoot, plant.OneFootRight );
		Emit( _handplantLeftFoot, plant.OneFootLeft );
		var dismount = DismountInputActions.Evaluate( previous, current );
		Emit( _dismount, dismount.Held );
		Emit( _newDismount, dismount.Started );
		var bail = BailInputActions.Evaluate( previous, current, context.BailGestureEnabled,
			context.BailControlEnabled, context.BailPushOffEnabled, context.SuppressBail );
		Emit( _bailGestureX, true, bail.GestureX );
		Emit( _bailGestureY, true, bail.GestureY );
		EmitAxis( _bailControlX, bail.ControlX );
		EmitAxis( _bailControlY, bail.ControlY );
		Emit( _bailRecover, bail.Recover );
		Emit( _bailRequest, bail.Request );
		Emit( _bailPushOff, bail.PushOff );
		var gestures = GestureInputActions.Evaluate( previous, current );
		Emit( _gestureDownStart, (gestures.Started & ControllerButtons.Down) != 0 );
		Emit( _gestureLeftStart, (gestures.Started & ControllerButtons.Left) != 0 );
		Emit( _gestureRightStart, (gestures.Started & ControllerButtons.Right) != 0 );
		Emit( _gestureUpStart, (gestures.Started & ControllerButtons.Up) != 0 );
		Emit( _gestureDownHeld, (gestures.Held & ControllerButtons.Down) != 0 );
		Emit( _gestureLeftHeld, (gestures.Held & ControllerButtons.Left) != 0 );
		Emit( _gestureRightHeld, (gestures.Held & ControllerButtons.Right) != 0 );
		Emit( _gestureUpHeld, (gestures.Held & ControllerButtons.Up) != 0 );
	}

	private void EmitOffBoard( ControllerReading current )
	{
		var actions = OffBoardInputActions.Evaluate( State.Previous, current );
		Emit( _offBoardJump, actions.Jump );
		EmitAxis( _lookX, actions.LookAtX );
		EmitAxis( _lookY, actions.LookAtY );
		Emit( _dropBoard, actions.DropBoard );
		Emit( _throwBoard, actions.ThrowBoard );
		Emit( _retrieveBoard, actions.RetrieveBoard );
		Emit( _airBodyTweak, actions.AirBodyTweak );
		Emit( _airBodyTweakX, true, actions.AirBodyTweakX );
		Emit( _airBodyTweakY, true, actions.AirBodyTweakY );
	}
}
