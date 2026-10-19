namespace SkateMotion.Core.Animation;

internal static partial class PhysicalLocomotionOwner
{
	/// <summary>Runs ordinary preparation, solved motion and retained placement in physical phase order.</summary>
	internal static bool TryPrepareSolveAndPlace( PhysicalLocomotionOwnerState state,
		in PhysicalLocomotionOwnerInput input, in PhysicalLocomotionMotionInput physical,
		ref PhysicalLocomotionPlacementState placement, in PhysicalLocomotionPlacementInput placementInput )
	{
		if ( !TryPrepareAndSolve( state, input, physical ) )
			return false;
		placement = PhysicalLocomotionPlacement.Update( placement, state, input, physical, placementInput );
		return true;
	}
}
