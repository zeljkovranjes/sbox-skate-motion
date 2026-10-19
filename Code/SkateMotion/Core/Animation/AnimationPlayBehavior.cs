namespace SkateMotion.Core.Animation;

/// <summary>Applies entry actions, one-use transition overrides and ordered motion variants.</summary>
internal static class AnimationPlayBehavior
{
	internal static void Enter( AnimationPlayBehaviorDefinition definition, int instance,
		int fallbackMotion, IAnimationPlayBehaviorContext context )
	{
		ArgumentNullException.ThrowIfNull( context );
		context.MarkEntered( instance );
		context.ExecuteEntryAction( definition.EntryAction, instance );
		context.RefreshCharacterSelection();
		var transition = context.ReadTransitionOverride();
		if ( transition.Kind == 0 )
			transition = definition.Transition;
		context.ConsumeTransitionOverride();

		var motion = definition.DefaultMotion;
		if ( (definition.ControlFlags & 0x00800000u) != 0 && context.IsAlternateStance() )
			motion = definition.AlternateStanceMotion;
		if ( (definition.ControlFlags & 0x00400000u) != 0 && context.IsMirrored() )
			motion = definition.MirroredMotion;
		if ( (definition.ControlFlags & 0x00200000u) != 0 && context.UseOffBoardVariant() )
			motion = definition.OffBoardMotion;
		context.SetActorMode( (byte)(definition.ControlFlags >> 24) );

		if ( !TryRequest( motion, definition.Rate, transition, context ) )
			context.TryReplace( new( fallbackMotion, AnimationBaseTransition.Play, 1f, StartTime: 0f ) );
	}

	private static bool TryRequest( int motion, float rate, AnimationPlayTransition transition,
		IAnimationPlayBehaviorContext context )
	{
		switch ( transition.Kind )
		{
			case 1:
				return context.TryReplace( new( motion, AnimationBaseTransition.Play, rate, StartTime: 0f ) );
			case 2:
			case 3:
				return context.TryReplace( new( motion,
					transition.Kind == 2 ? AnimationBaseTransition.Blend : AnimationBaseTransition.ChannelBlend,
					rate, transition.Duration, transition.Synchronization, transition.Under, 0f,
					transition.Kind == 3 && (transition.ChannelFlags & 0x80000000u) != 0 ) );
			case 4:
				return context.TryReplace( new( motion, AnimationBaseTransition.Sequence, rate,
					Under: transition.Under, StartTime: 0f ) );
			default:
				return false;
		}
	}
}
