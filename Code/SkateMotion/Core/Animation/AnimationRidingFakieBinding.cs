namespace SkateMotion.Core.Animation;

/// <summary>Connects physical snapshot publication to separately scheduled graph direction updates.</summary>
internal sealed class AnimationRidingFakieBinding
{
	private readonly AnimationRidingFakieVelocity _velocity = new();
	private readonly AnimationRidingFakieInstance _instance = new();
	private bool _hasObservation;
	internal AnimationPhysicalMovementState Movement { get; }
	internal AnimationRidingFakieObservation Observation { get; private set; }

	internal AnimationRidingFakieBinding( AnimationMovementContactKey emptyContactKey )
		=> Movement = new( emptyContactKey );

	internal AnimationPhysicalMovementOutput PublishPhysical( PhysicalPosePreparationState character,
		PhysicalPoseFeedbackState feedback, PhysicalBodyBinding[] boardBodies,
		PhysicalDirectionState direction, ulong actionFlags, AnimationPhysicalMovementInput input )
	{
		ArgumentNullException.ThrowIfNull( feedback );
		var observation = AnimationRidingFakieObservationProvider.Capture( character, boardBodies,
			direction, actionFlags, 0, input.Controllers.Current, default );
		_velocity.Update( feedback.Velocity );
		var movement = Movement.Update( input );
		Observation = observation with { MovementState = movement.Current, AlternateDirection = _velocity.Velocity };
		_hasObservation = true;
		return movement;
	}

	internal void UpdateGraph( AnimationRidingFakieDefinition definition, float deltaTime,
		AnimationGameplayFlags gameplay, Action<bool> setOwnerFakie )
	{
		if ( !_hasObservation )
			throw new InvalidOperationException( "Physical observations must be published before graph evaluation." );
		_instance.Update( definition, Observation, deltaTime, gameplay, setOwnerFakie );
	}
}
