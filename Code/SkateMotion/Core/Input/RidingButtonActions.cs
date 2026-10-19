namespace SkateMotion.Core.Input;

/// <summary>Independent riding button requests after timing and movement-state restrictions.</summary>
internal readonly record struct RidingButtonActions(
	bool ExitGrind, bool LeftGroundGrab, bool RightGroundGrab, bool LeftAirGrab, bool RightAirGrab,
	bool Brake, bool NewPush, bool LeftPush, bool RightPush, bool GrabWorld,
	bool ToggleOffBoard, bool NewToggleOffBoard, bool DarkCatch, bool NewDarkCatch, bool Sprint, bool NoInput )
{
	internal bool Pushing => LeftPush || RightPush;

	internal static RidingButtonActions Evaluate( ControllerInputState state, RidingButtonContext context,
		RidingButtonTuning tuning, float leftAngle, float leftMagnitude )
	{
		var current = state.Current;
		var leftPush = !context.SuppressPush && current.Down( ControllerButtons.West );
		var rightPush = !context.SuppressPush && current.Down( ControllerButtons.South );
		var newLeftPush = leftPush && (state.Pressed( ControllerButtons.West ) || state.WestHeld < tuning.PushWindow);
		var newRightPush = rightPush && (state.Pressed( ControllerButtons.South ) || state.SouthHeld < tuning.PushWindow);
		if ( context.AutoPush && !context.SuppressPush && !leftPush && !rightPush )
		{
			var targetAngle = leftAngle >= 0f ? tuning.Pi : -tuning.Pi;
			if ( MathF.Abs( targetAngle - leftAngle ) < tuning.AutoPushAngle && leftMagnitude > tuning.AutoPushMagnitude )
			{
				leftPush = newLeftPush = !context.Goofy;
				rightPush = newRightPush = context.Goofy;
			}
		}
		if ( context.SuppressSimultaneousPush && leftPush && rightPush )
		{
			leftPush = rightPush = newLeftPush = newRightPush = false;
		}
		var toggle = !context.MountPrepared && !current.Down( ControllerButtons.LeftBumper )
			&& current.Down( ControllerButtons.North );
		var newToggle = toggle && !context.SuppressNewToggle
			&& (state.Pressed( ControllerButtons.North ) || state.NorthHeld < tuning.MountWindow);
		var darkCatch = !context.SuppressDarkCatch && current.Down( ControllerButtons.East | ControllerButtons.RightBumper );
		var leftGroundGrab = current.LeftTrigger == 1f;
		var rightGroundGrab = current.RightTrigger == 1f;
		var leftAirGrab = current.LeftTrigger > 0f;
		var rightAirGrab = current.RightTrigger > 0f;
		var hasAxes = current.LeftX != 0f || current.LeftY != 0f || current.RightX != 0f || current.RightY != 0f;
		var noInput = !hasAxes && !current.Down( ControllerButtons.East | ControllerButtons.RightBumper )
			&& !leftPush && !rightPush && !leftGroundGrab && !rightGroundGrab && !leftAirGrab && !rightAirGrab && !toggle;
		return new(
			ExitGrind: state.Pressed( ControllerButtons.West ) || state.Pressed( ControllerButtons.South ) || state.Pressed( ControllerButtons.East ),
			LeftGroundGrab: leftGroundGrab, RightGroundGrab: rightGroundGrab,
			LeftAirGrab: leftAirGrab, RightAirGrab: rightAirGrab,
			Brake: !context.SuppressBrake && current.Down( ControllerButtons.East ),
			NewPush: newLeftPush || newRightPush, LeftPush: leftPush, RightPush: rightPush,
			GrabWorld: current.Down( ControllerButtons.RightBumper ),
			ToggleOffBoard: toggle, NewToggleOffBoard: newToggle, DarkCatch: darkCatch,
			NewDarkCatch: darkCatch && (state.Pressed( ControllerButtons.East ) || state.Pressed( ControllerButtons.RightBumper )),
			Sprint: state.EastHeld <= 0f && state.SouthHeld > 0f, NoInput: noInput );
	}
}
