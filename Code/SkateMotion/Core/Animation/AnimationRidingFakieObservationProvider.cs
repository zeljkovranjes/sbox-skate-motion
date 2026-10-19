using System;

namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;
using static LimbSolverMath;

/// <summary>Captures riding-direction observations from physical owner state for later graph evaluation.</summary>
internal static class AnimationRidingFakieObservationProvider
{
	// All inputs use the retained physical solver basis, before renderer coordinate conversion.
	internal static AnimationRidingFakieObservation Capture( PhysicalPosePreparationState character,
		PhysicalBodyBinding[] boardBodies, PhysicalDirectionState direction, ulong actionFlags,
		int movementState, int controllerCode, Vector3 alternateDirection )
	{
		ArgumentNullException.ThrowIfNull( character );
		ArgumentNullException.ThrowIfNull( boardBodies );
		ArgumentNullException.ThrowIfNull( direction );
		if ( boardBodies.Length <= 6 || boardBodies[ 6 ]?.Body is null )
			throw new ArgumentException( "Observation publication requires the physical board body.", nameof( boardBodies ) );

		var reference = character.ModelToWorld.Rotation.Z;
		if ( (actionFlags & 4UL) != 0 )
			reference = Scale( reference, -1f );
		var velocity = boardBodies[ 6 ].Body.Velocity;
		var normal = direction.FilteredDirection;
		var projected = velocity - Scale( normal, Dot( velocity, normal ) );
		var speed = SquareRoot( Dot( projected, projected ) );
		return new( movementState, controllerCode, reference, velocity, alternateDirection, speed );
	}
}
