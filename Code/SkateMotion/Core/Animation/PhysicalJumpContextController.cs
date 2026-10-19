namespace SkateMotion.Core.Animation;

/// <summary>Refreshes retained launch inputs and advances their discrete preparation cadence.</summary>
internal static class PhysicalJumpContextController
{
	internal static PhysicalJumpVelocityInput Prepare( PhysicalAnimationAttributeState attributes,
		PhysicalJumpVelocityState state, PhysicalJumpVelocityInput retained,
		PhysicalJumpSurfaceInput surface, int mode )
	{
		ArgumentNullException.ThrowIfNull( attributes );
		ArgumentNullException.ThrowIfNull( state );
		state.Triggered = false;
		var hold = unchecked(state.HoldFrames - 1);
		state.HoldFrames = hold < 0 ? 0 : hold;
		if ( hold > 0 )
			attributes.ActionFlags |= 1UL << 25;
		if ( surface.Available )
		{
			retained = retained with
			{
				CorrectionEnabled = surface.CorrectionEnabled,
				FallbackDirection = surface.FallbackDirection,
				Correction = surface.Correction,
				PrimaryDirection = surface.PrimaryDirection,
				SecondaryDirection = surface.SecondaryDirection,
				Anchor = surface.Anchor
			};
		}
		if ( mode != -1 )
			retained = retained with { Mode = mode };
		var elapsed = state.Cooldown + .0035f;
		state.Cooldown = 1f - elapsed >= 0f ? elapsed : 1f;
		return retained;
	}
}
