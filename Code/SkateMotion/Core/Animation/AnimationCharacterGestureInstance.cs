namespace SkateMotion.Core.Animation;

/// <summary>Retains gesture entry, cycling and exit phases across graph updates.</summary>
internal sealed class AnimationCharacterGestureInstance
{
	internal bool Active { get; private set; }
	internal int Phase { get; private set; } = -1;
	internal int Gesture { get; private set; } = -1;
	internal int Mode { get; private set; } = -1;

	internal void Update( AnimationCharacterGestureDefinition definition, IAnimationCharacterGestureContext context )
	{
		var anyChannel = context.HasChannel( definition.Channel( 2 ) );
		anyChannel |= context.HasChannel( definition.Channel( 1 ) );
		anyChannel |= context.HasChannel( definition.Channel( 0 ) );
		if ( !Active && anyChannel )
		{
			StopAll( definition, context );
			Mode = Gesture = -1;
		}
		else if ( Active && !anyChannel )
		{
			Reset();
		}

		var selection = context.ReadSelection();
		var requested = FirstRequested( definition, context );
		Span<bool> held = stackalloc bool[ 4 ];
		held[ 3 ] = context.ContainsRequest( definition.Held( 3 ) );
		held[ 0 ] = context.ContainsRequest( definition.Held( 0 ) );
		held[ 2 ] = context.ContainsRequest( definition.Held( 2 ) );
		held[ 1 ] = context.ContainsRequest( definition.Held( 1 ) );
		var mode = ResolveMode( definition, context );
		if ( requested == -1 && Gesture != -1 && !held[ Gesture ] )
			requested = held[ 3 ] ? 3 : held[ 0 ] ? 0 : held[ 2 ] ? 2 : held[ 1 ] ? 1 : -1;
		var blockedUp = context.IsUpBlocked();
		var braking = context.ContainsRequest( definition.Brake );
		var maintain = requested == -1 || mode == -1 || braking || (blockedUp && requested == 2)
			|| (Gesture != -1 && held[ Gesture ]) || (Active && requested == Gesture);
		if ( maintain )
		{
			if ( !Active )
				return;
			if ( Mode != mode )
			{
				StopAll( definition, context );
				Active = false;
				Mode = Gesture = -1;
				return;
			}
			Maintain( definition, context, selection, held );
		}
		else
		{
			Gesture = requested;
			Phase = 0;
			var motion = context.Motion( selection.Get( Gesture ), mode, Phase );
			context.StartChannel( definition.Channel( mode ), motion );
			if ( mode != 2 )
				context.StopChannel( definition.Channel( 2 ) );
			if ( mode != 0 )
				context.StopChannel( definition.Channel( 0 ) );
			if ( mode != 1 )
				context.StopChannel( definition.Channel( 1 ) );
			Mode = mode;
			Active = true;
		}
		if ( !Active )
			return;
		context.PublishGesture( selection.Get( Gesture ), Gesture == 3 );
		context.SetDistance( definition.Distance, context.ReadDistance() );
	}

	private void Maintain( AnimationCharacterGestureDefinition definition, IAnimationCharacterGestureContext context,
		AnimationGestureSelection selection, ReadOnlySpan<bool> held )
	{
		var channel = definition.Channel( Mode );
		var remaining = context.Remaining( channel );
		var pending = context.HasPending( channel );
		if ( pending || !(remaining < .1f) )
			return;
		if ( Phase == 2 )
		{
			Reset();
			return;
		}
		if ( Phase == 1 && held[ Gesture ] )
			return;
		if ( Phase != 0 && Phase != 1 )
			return;
		Phase++;
		var motion = context.Motion( selection.Get( Gesture ), Mode, Phase );
		context.QueueChannel( channel, motion, Phase == 1 );
	}

	private void Reset()
	{
		Active = false;
		Phase = Gesture = Mode = -1;
	}

	private static void StopAll( AnimationCharacterGestureDefinition definition, IAnimationCharacterGestureContext context )
	{
		context.StopChannel( definition.Channel( 2 ) );
		context.StopChannel( definition.Channel( 1 ) );
		context.StopChannel( definition.Channel( 0 ) );
	}

	private static int FirstRequested( AnimationCharacterGestureDefinition definition, IAnimationCharacterGestureContext context ) =>
		context.ContainsRequest( definition.Start( 3 ) ) ? 3 : context.ContainsRequest( definition.Start( 0 ) ) ? 0
		: context.ContainsRequest( definition.Start( 2 ) ) ? 2 : context.ContainsRequest( definition.Start( 1 ) ) ? 1 : -1;

	private static int ResolveMode( AnimationCharacterGestureDefinition definition, IAnimationCharacterGestureContext context )
	{
		for ( var index = 0; index < 3; index++ )
			if ( context.HasChannel( definition.SuppressedChannel( index ) ) )
				return -1;
		if ( context.ContainsRequest( definition.Mount ) || context.ContainsRequest( definition.Dismount ) )
			return -1;
		return context.ReadModeInput().Resolve();
	}
}
