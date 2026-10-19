namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;

internal static class PhysicalLocomotionPlacementPreparation
{
	/// <summary>Predicts the physical placement target and retains the query's distinct secondary point.</summary>
	internal static PhysicalLocomotionPlacementInput Prepare( Vector3 physicalPosition,
		Vector3 physicalVelocity, bool overrideVelocityActive, Vector3 overrideVelocity,
		Vector3 secondaryQueryPoint )
	{
		var velocity = overrideVelocityActive ? overrideVelocity : physicalVelocity;
		var tick = BitConverter.UInt32BitsToSingle( 0x3c888889 );
		var displacement = new Vector3( MathF.FusedMultiplyAdd( velocity.X, tick, 0f ),
			MathF.FusedMultiplyAdd( velocity.Y, tick, 0f ), MathF.FusedMultiplyAdd( velocity.Z, tick, 0f ) );
		return new( new Vector3( physicalPosition.X + displacement.X,
			physicalPosition.Y + displacement.Y, physicalPosition.Z + displacement.Z ), secondaryQueryPoint );
	}

	/// <summary>Clears transition correction while preserving the independently retained placement reference.</summary>
	internal static void ResetCorrection( PhysicalLocomotionOwnerState owner,
		ref PhysicalLocomotionPlacementState placement )
	{
		ArgumentNullException.ThrowIfNull( owner );
		owner.SecondaryCorrection = Vector3.Zero;
		owner.SecondaryCorrectionActive = false;
		placement = placement with { SecondaryTarget = Vector3.Zero };
	}
}
