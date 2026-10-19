namespace SkateMotion.Core.Animation;

/// <summary>Retains the head scalar and preceding fakie flag across behavior entry and exit.</summary>
internal sealed class AnimationFakieHeadInstance
{
	private static readonly AnimationAttributeKey Torso = AnimationAttributeKey.Encode( "torso" );

	internal float Value { get; private set; }
	internal bool Active { get; private set; }

	internal void Enter() { }
	internal void Exit() { }

	internal void Update( AnimationFakieHeadDefinition definition, IAnimationFakieHeadContext context )
	{
		ArgumentNullException.ThrowIfNull( context );
		var target = context.IsPrimaryInputActive() ? 1f
			: context.IsSecondaryInputActive() ? 0f : .5f;
		var active = context.IsRidingFakie();
		if ( !Active )
		{
			if ( !active )
				return;
			context.StartChannel( definition.Channel, definition.Motion );
			Value = target;
		}
		else if ( !active )
		{
			context.StopChannel( definition.Channel );
			Active = false;
			return;
		}

		Active = active;
		var difference = target - Value;
		var lower = -.01f - difference >= 0f ? -.01f : difference;
		var increment = .01f - lower >= 0f ? lower : .01f;
		Value += increment;
		context.SetParameter( Torso, Value, false, -1 );
	}
}
