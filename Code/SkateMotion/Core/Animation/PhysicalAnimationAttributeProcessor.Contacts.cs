namespace SkateMotion.Core.Animation;

using static LimbSolverMath;

internal static partial class PhysicalAnimationAttributeProcessor
{
	private static readonly AnimationAttributeKey PushContactKey = AnimationAttributeKey.Encode( "PUSH_CONTACT" );
	private static readonly AnimationAttributeKey BrakeContactKey = AnimationAttributeKey.Encode( "BRAKE_CONTACT" );
	private static readonly AnimationAttributeKey RightGrabKey = AnimationAttributeKey.Encode( "RIGHT_HAND_GRAB" );
	private static readonly AnimationAttributeKey LeftGrabKey = AnimationAttributeKey.Encode( "LEFT_HAND_GRAB" );
	private static readonly AnimationAttributeKey RightHandKey = AnimationAttributeKey.Encode( "RIGHTHAND" );

	private static void ApplyContact( PhysicalAnimationAttribute attribute, PhysicalAnimationAttributeState state,
		PhysicalAnimationAttributeInput input )
	{
		if ( (attribute.EventFlags & 12) == 0 )
			return;
		state.ContactBone = -1;
		for ( var i = 0; i < input.BoneNames.Length; i++ )
		{
			if ( input.BoneNames[ i ] != attribute.Bone )
				continue;
			state.ContactBone = i;
			break;
		}
		if ( attribute.Name == PushContactKey )
		{
			var flags = state.MotionFlags | (1UL << 27);
			flags = ReplaceBit( flags, 26, state.ContactBone == input.RightToeBone );
			flags = ReplaceBit( flags, 25, attribute.Value == 1f );
			state.MotionFlags = ReplaceBit( flags, 24, attribute.Value == -1f );
			var squared = Dot( input.BoardDisplacement, input.BoardDisplacement );
			var speed = MultiplyAdd( 1f / input.DeltaTime, SquareRoot( squared ), 0f );
			state.PushSpeed = attribute.Value == 1f ? speed : 0f;
		}
		else if ( attribute.Name == BrakeContactKey )
		{
			var flags = state.MotionFlags | (1UL << 28);
			flags = ReplaceBit( flags, 30, attribute.Value == 1f );
			state.MotionFlags = ReplaceBit( flags, 23, attribute.Value == -1f );
		}
		else if ( attribute.Name == RightGrabKey || attribute.Name == LeftGrabKey )
		{
			state.ActionFlags |= attribute.Bone == RightHandKey ? 1UL << 40 : 1UL << 39;
		}
	}
}
