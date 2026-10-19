namespace SkateMotion.Core.Animation;

/// <summary>Runs non-launch character placement, board forces and direction continuation in owner order.</summary>
internal sealed class PhysicalNonJumpMotionController
{
	private readonly PhysicalGroundCharacterController _ground;
	private readonly PhysicalRidingCharacterController _riding;
	private readonly PhysicalBoardForceProducer _forces;
	private readonly PhysicalDirectionController _direction;

	internal PhysicalNonJumpMotionController( PhysicalGroundCharacterController ground,
		PhysicalRidingCharacterController riding, PhysicalBoardForceProducer forces, PhysicalDirectionController direction )
	{
		ArgumentNullException.ThrowIfNull( ground );
		ArgumentNullException.ThrowIfNull( riding );
		ArgumentNullException.ThrowIfNull( forces );
		ArgumentNullException.ThrowIfNull( direction );
		_ground = ground;
		_riding = riding;
		_forces = forces;
		_direction = direction;
	}

	internal void Update( PhysicalJumpCharacterState motor, PhysicalCharacterState character,
		PhysicalRiderFrameState placement, PhysicalBoardForceState forces, PhysicalDirectionState direction,
		PhysicalNonJumpMotionInput input, PhysicalBodyState primaryBody, PhysicalBoardForceQueue queue )
	{
		ArgumentNullException.ThrowIfNull( motor );
		if ( (input.Forces.MotionFlags & (1UL << 22)) != 0 )
			throw new ArgumentException( "The non-launch callback requires the launch request to be clear.", nameof( input ) );
		motor.Launched = false;
		if ( SelectRiding( input.CharacterMode, input.CharacterVerticalValue,
			input.CharacterVerticalThreshold, input.Forces.ActionFlags ) )
			_riding.Update( character, placement, input.Character with { BoardCorrectionMode = 0 } );
		else
			_ground.Update( character, placement, new( input.Character.OrientationSource,
				input.Character.RigInput, input.Character.PlacementDeltaTime ) );

		_forces.Update( forces, input.Forces, input.ForceTuning, character.BoardBodies[ 6 ], primaryBody, queue );
		var frame = input.Forces.PhysicalFrame.Rotation;
		var sign = (input.RigFlags & (1UL << 22)) != 0 ? -1f : 1f;
		_direction.Update( direction, new( LimbSolverMath.Scale( frame.Y, sign ), frame.Z,
			.98f, (input.Forces.MotionFlags & (1UL << 20)) != 0, false, 0f ) );
	}

	internal static bool SelectRiding( int mode, float verticalValue, float threshold, ulong actionFlags )
	{
		var eligible = mode == 2 || (MathF.Abs( verticalValue ) > threshold && mode != 1);
		return eligible && (actionFlags & (1UL << 54)) != 0;
	}
}
