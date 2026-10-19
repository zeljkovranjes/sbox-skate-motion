namespace SkateMotion.Core.Animation;

internal static partial class PhysicalAnimationAttributeProcessor
{
	private static void FinalizeJump( PhysicalAnimationAttributeState state, PhysicalAnimationAttributeInput input,
		float minimumJump, float trickHeight, bool hasTrickHeight )
	{
		if ( (state.MotionFlags & (1UL << 22)) != 0 )
		{
			if ( input.SelectJumpExtremes && trickHeight <= input.LowJumpThreshold )
				state.JumpStrength = minimumJump;
			else if ( input.SelectJumpExtremes && trickHeight >= input.HighJumpThreshold )
				state.JumpStrength = 1f;
			else
				state.JumpStrength = trickHeight;
		}
		if ( hasTrickHeight && input.AllowHeightOverride )
		{
			if ( state.HeightOverrideActive )
				state.JumpStrength = state.HeightOverride - state.JumpStrength >= 0f ? state.HeightOverride : state.JumpStrength;
		}
		else
		{
			state.HeightOverride = 0f;
			state.HeightOverrideActive = false;
		}
		if ( (state.RigFlags & (1UL << 45)) != 0 )
			state.JumpStrength = trickHeight;
	}

	private static void PublishControls( PhysicalAnimationAttributeState state, PhysicalAnimationAttributeInput input )
	{
		if ( input.UsePreparedControls )
		{
			state.JumpControlX = state.PreparedControlX;
			state.JumpControlY = state.PreparedControlY;
		}
		else if ( input.HasController )
		{
			state.JumpControlX = ClampControl( input.Controls.X );
			// Preparation and publication deliberately use different secondary actions.
			state.JumpControlY = ClampControl( input.Controls.Z );
		}
		if ( input.ExternalImpulseActive )
			state.MotionFlags |= 1UL << 18;
		var flags = ReplaceBit( state.RigFlags, 36, (input.AnimationFlags & (1UL << 27)) != 0 );
		flags = ReplaceBit( flags, 2, (input.AnimationFlags & (1UL << 23)) != 0 );
		flags = ReplaceBit( flags, 32, (input.AnimationFlags & (1UL << 25)) != 0 );
		state.RigFlags = ReplaceBit( flags, 25, (input.AnimationFlags & (1UL << 28)) != 0 );
		if ( (state.ActionFlags & 0x600UL) != 0 )
			state.ActionFlags &= ~(1UL << 22);
	}

	private static float ClampControl( float value )
	{
		var lower = -1f - value >= 0f ? -1f : value;
		return 1f - lower >= 0f ? lower : 1f;
	}

	private static ulong ReplaceBit( ulong flags, int bit, bool set ) =>
		(flags & ~(1UL << bit)) | (set ? 1UL << bit : 0UL);
}
