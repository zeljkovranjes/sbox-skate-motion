namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;
using static LimbSolverMath;

/// <summary>Retains the physical character velocity published for animation graph observations.</summary>
internal sealed class AnimationRidingFakieVelocity
{
	internal Vector3 Velocity { get; private set; }
	internal Vector3 Acceleration { get; private set; }

	internal void Reset()
	{
		Velocity = Vector3.Zero;
		Acceleration = Vector3.Zero;
	}

	internal void Update( Vector3 physicalVelocity )
	{
		var previous = Velocity;
		Velocity = Scale( previous, .75f ) + Scale( physicalVelocity, .25f );
		Acceleration = Scale( Velocity - previous, Reciprocal( 1f / 60f ) );
	}

	// Consume the current postsolve velocity in the same physical basis as the character frame.
	internal AnimationRidingFakieObservation Capture( PhysicalPosePreparationState character,
		PhysicalPoseFeedbackState feedback,
		PhysicalBodyBinding[] boardBodies, PhysicalDirectionState direction, ulong actionFlags,
		int movementState, int controllerCode )
	{
		ArgumentNullException.ThrowIfNull( character );
		ArgumentNullException.ThrowIfNull( feedback );
		var observation = AnimationRidingFakieObservationProvider.Capture( character,
			boardBodies, direction, actionFlags, movementState, controllerCode, Velocity );
		Update( feedback.Velocity );
		return observation with { AlternateDirection = Velocity };
	}
}
