namespace SkateMotion.Core.Physics;

/// <summary>Runs ownership work before dispatching motion for the resulting board state.</summary>
internal static class PhysicalBoardTick
{
	internal static int Update( bool enabled, PhysicalBoardInput input, PhysicalBoardTickActions actions )
	{
		if ( !enabled )
			return input.State;
		ArgumentNullException.ThrowIfNull( actions );
		if ( input.State == 2 )
			input = input with { GroundCheck = actions.IsBeyondPlacementRange() };

		var decision = PhysicalBoardController.Evaluate( input );
		if ( decision.ClearVelocity )
			actions.ClearVelocity();
		if ( decision.ChangeState )
			actions.ChangeState( input.State, decision.NextState );
		if ( decision.UpdateHeld )
			actions.UpdateHeldFeedback();
		if ( decision.UpdateContactSpring )
			// The decision's legacy name describes a direction-alignment threshold.
			actions.UpdateContactDirections( decision.ContactSpringStrength );

		switch ( decision.NextState )
		{
			case 1:
				actions.UpdateHeldFrame();
				break;
			case 2:
				actions.UpdateFreeBoard();
				break;
			case 3:
				actions.UpdatePlacement();
				break;
			case 4:
				actions.UpdateRetrieval();
				break;
		}
		return decision.NextState;
	}
}
