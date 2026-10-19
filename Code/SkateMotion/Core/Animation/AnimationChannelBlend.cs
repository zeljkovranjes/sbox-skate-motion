namespace SkateMotion.Core.Animation;

/// <summary>Controls a layer's weight and whether its overlay clock advances during a fade.</summary>
internal sealed class AnimationChannelBlend
{
	private const uint HoldDuringBlendIn = 0x80000000;
	private const uint HoldDuringBlendOut = 0x40000000;
	private const uint StopRequested = 0x20000000;
	private const uint KeepPlaying = 0x10000000;
	private const uint HasBlendOut = 0x08000000;
	private const uint OverlayFinished = 0x04000000;

	internal AnimationChannelBlendState State { get; private set; }

	internal AnimationChannelBlend( AnimationChannelBlendState state )
	{
		State = state;
	}

	/// <summary>Initializes the envelope after a new channel's tree has been created.</summary>
	internal void StartEnvelope( float entryDuration, float exitDuration, uint requestedFlags )
	{
		var flags = (State.Flags & 0x01ffffff) | (requestedFlags & 0xd2000000);
		if ( exitDuration > 0f )
			flags |= HasBlendOut;
		State = new AnimationChannelBlendState(
			0f, 1f, 0f, entryDuration, 0f, exitDuration, 0f, 0f, flags );
	}

	/// <summary>Tests replacement eligibility before allocating or releasing animation trees.</summary>
	internal bool CanReplace( bool allowRecovery ) => !(State.BlendOutElapsed > 0f) || allowRecovery;

	/// <summary>Updates the envelope after successful tree replacement, preserving entry and completion history.</summary>
	internal bool TryReplaceEnvelope( float recoveryDuration, float exitDuration,
		uint requestedFlags, bool allowRecovery )
	{
		if ( !CanReplace( allowRecovery ) )
			return false;
		ApplyReplacementEnvelope( recoveryDuration, exitDuration, requestedFlags, allowRecovery );
		return true;
	}

	/// <summary>Applies an admitted replacement after factory callbacks without repeating admission.</summary>
	internal void ApplyReplacementEnvelope( float recoveryDuration, float exitDuration,
		uint requestedFlags, bool allowRecovery )
	{
		if ( State.BlendOutElapsed > 0f && allowRecovery )
			Recover( recoveryDuration );

		// Replacement retains entry hold and completion, unlike initialization of a new channel.
		var flags = (State.Flags & 0x85ffffff) | (requestedFlags & 0x52000000);
		if ( exitDuration > 0f )
			flags |= HasBlendOut;
		State = State with { BlendOutElapsed = 0f, BlendOutDuration = exitDuration, Flags = flags };
	}

	internal bool Finished => (State.Flags & HasBlendOut) != 0
		? !(State.BlendOutElapsed < State.BlendOutDuration)
		: (State.Flags & (KeepPlaying | OverlayFinished)) == OverlayFinished;

	/// <summary>Call after advancing the underlying layer; returns whether to advance the overlay.</summary>
	internal bool Update( float delta, float overlayDuration, float overlayElapsed )
		=> CompleteUpdate( delta, AdvanceEntry( delta ), overlayDuration, overlayElapsed );

	/// <summary>Publishes entry-clock advancement before querying overlay timing.</summary>
	internal bool Update( float delta, IAnimationPlaybackNode overlay )
	{
		var blendIn = AdvanceEntry( delta );
		var duration = overlay.Duration;
		var elapsed = overlay.Elapsed;
		return CompleteUpdate( delta, blendIn, duration, elapsed );
	}

	private float AdvanceEntry( float delta )
	{
		var state = State;
		var blendIn = 1f;
		if ( state.BlendInDuration > 0f && state.BlendInElapsed < state.BlendInDuration )
		{
			blendIn = Saturate( state.BlendInElapsed / state.BlendInDuration );
			State = state with { BlendInElapsed = state.BlendInElapsed + delta };
		}
		return blendIn;
	}

	private bool CompleteUpdate( float delta, float blendIn, float overlayDuration, float overlayElapsed )
	{
		var state = State;
		var remaining = overlayDuration - overlayElapsed;
		remaining = remaining >= 0f ? remaining : 0f;
		var holdsOnExit = (state.Flags & HoldDuringBlendOut) != 0;
		var nearEnd = !holdsOnExit && remaining < state.BlendOutDuration;
		var blendOut = 1f;
		var stopping = (state.Flags & StopRequested) != 0;
		var completes = (state.Flags & (HoldDuringBlendOut | OverlayFinished))
			== (HoldDuringBlendOut | OverlayFinished);
		if ( state.BlendOutDuration > 0f
			&& (stopping || ((state.Flags & KeepPlaying) == 0 && (completes || nearEnd))) )
		{
			blendOut = Saturate( 1f - state.BlendOutElapsed / state.BlendOutDuration );
			state = state with { BlendOutElapsed = state.BlendOutElapsed + delta };
		}
		var recovery = 1f;
		if ( state.RecoveryDuration > 0f && state.RecoveryElapsed < state.RecoveryDuration )
		{
			recovery = Saturate( state.RecoveryElapsed / state.RecoveryDuration );
			state = state with { RecoveryElapsed = state.RecoveryElapsed + delta };
		}
		// Weights use the clocks from the start of the update; playback gates use the advanced clocks.
		var weight = blendIn - blendOut >= 0f ? blendOut : blendIn;
		weight = weight - recovery >= 0f ? recovery : weight;
		State = state with { Weight = Saturate( weight * state.TargetWeight ) };
		var waitsForEntry = (state.Flags & HoldDuringBlendIn) != 0
			&& state.BlendInElapsed < state.BlendInDuration;
		return !waitsForEntry && (!holdsOnExit || state.BlendOutElapsed <= 0f);
	}

	/// <summary>Publish the overlay's finished result only after an update that advanced it.</summary>
	internal void CompleteOverlayUpdate( bool finished )
	{
		State = State with
		{
			Flags = (State.Flags & ~OverlayFinished) | (finished ? OverlayFinished : 0u)
		};
	}

	internal void Stop( float duration, bool holdPlayback )
	{
		if ( State.BlendOutElapsed != 0f )
			return;
		State = State with
		{
			BlendOutDuration = duration,
			Flags = (State.Flags & ~HoldDuringBlendOut) | StopRequested | HasBlendOut
				| (holdPlayback ? HoldDuringBlendOut : 0u)
		};
	}

	/// <summary>Requests the retained exit envelope without changing its duration or playback hold.</summary>
	internal void RequestStop()
	{
		if ( State.BlendOutElapsed == 0f )
			State = State with { Flags = State.Flags | StopRequested | HasBlendOut };
	}

	internal void Recover( float duration )
	{
		State = State with
		{
			RecoveryElapsed = Saturate( 1f - State.BlendOutElapsed / State.BlendOutDuration ) * duration,
			RecoveryDuration = duration
		};
	}

	private static float Saturate( float value )
	{
		var lower = -value >= 0f ? 0f : value;
		return 1f - lower >= 0f ? lower : 1f;
	}
}
