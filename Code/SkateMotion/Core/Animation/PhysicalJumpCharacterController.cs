namespace SkateMotion.Core.Animation;

/// <summary>Runs the explicit jump branch through deck-driven character placement and body publication.</summary>
internal sealed class PhysicalJumpCharacterController
{
	private readonly PhysicalRidingCharacterController _riding;

	internal PhysicalJumpCharacterController( PhysicalRidingCharacterController riding )
	{
		ArgumentNullException.ThrowIfNull( riding );
		_riding = riding;
	}

	// False leaves non-jump routing to the caller. The later direction-controller phase is separate.
	internal bool TryUpdate( PhysicalJumpCharacterState state, PhysicalAnimationAttributeState attributes,
		PhysicalJumpVelocityState launchState, PhysicalJumpVelocityInput launchInput,
		PhysicalJumpVelocityTuning tuning, ReadOnlySpan<PhysicalBodyState> bodies,
		PhysicalCharacterState character, PhysicalRiderFrameState placement, PhysicalRidingCharacterInput ridingInput )
	{
		ArgumentNullException.ThrowIfNull( state );
		ArgumentNullException.ThrowIfNull( attributes );
		state.Launched = false;
		if ( (attributes.MotionFlags & (1UL << 22)) == 0 )
			return false;

		state.Velocity = PhysicalJumpVelocityController.Launch( attributes, launchState,
			launchInput, tuning with { CorrectionInfluence = .7f }, bodies );
		state.Launched = true;
		_riding.Update( character, placement, ridingInput with { BoardCorrectionMode = 0 } );
		foreach ( var body in bodies )
			body.Velocity = state.Velocity;
		return true;
	}
}
