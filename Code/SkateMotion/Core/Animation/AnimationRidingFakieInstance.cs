using System;

namespace SkateMotion.Core.Animation;

/// <summary>Updates the shared riding-direction flag while retaining its two graph timers.</summary>
internal sealed class AnimationRidingFakieInstance
{
	internal float ConfirmationTime { get; private set; }
	internal float TimeSinceTeleport { get; private set; }

	internal void Enter() { }
	internal void Exit() { }

	internal void Update( AnimationRidingFakieDefinition definition,
		AnimationRidingFakieObservation observation, float graphDelta,
		AnimationGameplayFlags gameplay, Action<bool> setOwnerFakie )
	{
		ArgumentNullException.ThrowIfNull( gameplay );
		ArgumentNullException.ThrowIfNull( setOwnerFakie );
		if ( observation.MovementState == 5 )
		{
			TimeSinceTeleport = 0f;
			setOwnerFakie( false );
			ConfirmationTime = 0f;
			return;
		}

		var previousTime = TimeSinceTeleport;
		TimeSinceTeleport += graphDelta;
		var riding = observation.MovementState is 1 or 2;
		var eligible = riding || (observation.MovementState == 6 && observation.ControllerCode == 0x1f7);
		var evaluateDirection = eligible && !gameplay.IsDoingTrick;
		if ( previousTime <= definition.TeleportTimeThreshold )
		{
			setOwnerFakie( false );
			ConfirmationTime = 0f;
			return;
		}
		if ( !evaluateDirection )
		{
			ConfirmationTime = 0f;
			// Riding states retain the preceding direction throughout a trick.
			if ( !riding )
				setOwnerFakie( false );
			return;
		}

		var direction = observation.MovementState == 1
			? observation.BoardDirection : observation.AlternateDirection;
		var backwards = LimbSolverMath.Dot( direction, observation.ReferenceDirection ) < -.5f;
		// Negated comparisons preserve the unordered branch behavior of the owner.
		if ( !(definition.HighSpeedThreshold >= observation.Speed) && backwards )
		{
			ConfirmationTime = 0f;
			setOwnerFakie( true );
			return;
		}
		if ( !(definition.LowSpeedThreshold >= observation.Speed) && backwards )
		{
			ConfirmationTime += graphDelta;
			setOwnerFakie( definition.ConfirmationTimeThreshold < ConfirmationTime );
			return;
		}
		ConfirmationTime = 0f;
		setOwnerFakie( false );
	}
}
