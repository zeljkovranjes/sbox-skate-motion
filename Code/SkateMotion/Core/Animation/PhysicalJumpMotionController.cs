namespace SkateMotion.Core.Animation;

/// <summary>Connects launch and character placement to the retained physical direction phase.</summary>
internal sealed class PhysicalJumpMotionController
{
	private readonly PhysicalJumpCharacterController _character;
	private readonly PhysicalDirectionController _direction;

	internal PhysicalJumpMotionController( PhysicalJumpCharacterController character, PhysicalDirectionController direction )
	{
		ArgumentNullException.ThrowIfNull( character );
		ArgumentNullException.ThrowIfNull( direction );
		_character = character;
		_direction = direction;
	}

	internal bool TryUpdate( PhysicalJumpCharacterState state, PhysicalAnimationAttributeState attributes,
		PhysicalJumpVelocityState launchState, PhysicalJumpVelocityInput launchInput,
		PhysicalJumpVelocityTuning tuning, ReadOnlySpan<PhysicalBodyState> bodies,
		PhysicalCharacterState character, PhysicalRiderFrameState placement, PhysicalRidingCharacterInput ridingInput,
		PhysicalDirectionState direction, LimbFrame directionFrame )
	{
		ArgumentNullException.ThrowIfNull( direction );
		if ( !_character.TryUpdate( state, attributes, launchState, launchInput, tuning, bodies, character, placement, ridingInput ) )
			return false;

		var sign = (attributes.RigFlags & (1UL << 22)) != 0 ? -1f : 1f;
		_direction.Update( direction, new( LimbSolverMath.Scale( directionFrame.Y, sign ), directionFrame.Z,
			.98f, (attributes.MotionFlags & (1UL << 20)) != 0, false, 0f ) );
		return true;
	}
}
