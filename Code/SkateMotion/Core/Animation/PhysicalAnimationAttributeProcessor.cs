namespace SkateMotion.Core.Animation;

/// <summary>Consumes ordered attributes without resetting state owned by other update stages.</summary>
internal static partial class PhysicalAnimationAttributeProcessor
{
	private static readonly AnimationAttributeKey JumpKey = AnimationAttributeKey.Encode( "JUMP" );

	internal static void Update( PhysicalAnimationAttributeState state, PhysicalAnimationAttributeInput input )
	{
		ArgumentNullException.ThrowIfNull( state );
		ArgumentNullException.ThrowIfNull( input.Attributes );
		UpdateAttributes( state, input, input.Attributes );
	}

	internal static void UpdateAttributes( PhysicalAnimationAttributeState state,
		PhysicalAnimationAttributeInput input, ReadOnlySpan<PhysicalAnimationAttribute> attributes )
	{
		ArgumentNullException.ThrowIfNull( state );
		ArgumentNullException.ThrowIfNull( input.BoneNames );
		var revert = false;
		var revertDirection = 1f;
		var minimumJump = 0f;
		var trickHeight = 0f;
		var hasTrickHeight = false;
		foreach ( var attribute in attributes )
		{
			if ( attribute.Kind == 3 )
			{
				ApplyContact( attribute, state, input );
				continue;
			}
			if ( attribute.Kind != 0 )
				continue;
			if ( attribute.Name == JumpKey )
			{
				state.MotionFlags |= 1UL << 22;
				continue;
			}
			var name = PhysicalAnimationAttributeCatalog.Find( attribute.Name );
			switch ( name )
			{
				case "REVERT":
					revert = true;
					break;
				case "REVERTDIR":
					revertDirection = attribute.Value;
					state.ActionFlags |= 1UL << 53;
					break;
				case "MINJUMP":
					minimumJump = attribute.Value;
					break;
				case "TRICKHEIGHT":
					trickHeight = attribute.Value;
					hasTrickHeight = true;
					break;
				case "PREPAREJUMP":
					state.MotionFlags |= 1UL << 21;
					if ( input.HasController )
					{
						state.PreparedControlX = ClampControl( input.Controls.X );
						state.PreparedControlY = ClampControl( input.Controls.Y );
					}
					break;
				case "JUMPHEIGHTOVERRIDE":
					state.HeightOverride = attribute.Value;
					state.HeightOverrideActive = true;
					break;
				default:
					ApplyScalar( name, attribute, state );
					break;
			}
		}
		if ( revert )
			state.RevertDirection = revertDirection;
		FinalizeJump( state, input, minimumJump, trickHeight, hasTrickHeight );
		PublishControls( state, input );
	}
}
