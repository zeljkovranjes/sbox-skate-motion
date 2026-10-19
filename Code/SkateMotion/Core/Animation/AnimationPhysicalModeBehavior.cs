namespace SkateMotion.Core.Animation;

/// <summary>Assigns the retained physical character mode when its graph behavior is entered.</summary>
internal static class AnimationPhysicalModeBehavior
{
	internal static CompiledStateBehaviorBinding Bind( int mode, Func<PhysicalCharacterState> character )
	{
		ArgumentNullException.ThrowIfNull( character );
		return new( () => -1, _ => Enter( mode, character ), _ => { }, () => Enter( mode, character ) );
	}

	internal static void Update()
	{
	}

	private static void Enter( int mode, Func<PhysicalCharacterState> character )
	{
		var state = character();
		if ( state is not null )
			state.PhysicsMode = mode;
	}
}
