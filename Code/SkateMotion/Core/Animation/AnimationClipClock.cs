namespace SkateMotion.Core.Animation;

/// <summary>Authored clip timing; looping callers must supply a positive duration.</summary>
internal sealed class AnimationClipClock : IAnimationRatePlaybackNode
{
	private readonly float _frameCount;
	private readonly float _frameRate;
	private readonly float _rateScale;

	internal AnimationClipClock( float frameCount, float frameRate, float rateScale,
		bool looping, bool phaseControlled )
	{
		_frameCount = frameCount;
		_frameRate = frameRate;
		_rateScale = rateScale;
		Looping = looping;
		PhaseControlled = phaseControlled;
		SetRate( 1f );
		Seek( 0f );
	}

	/// <summary>Authored frame interval count divided by the current effective frame rate.</summary>
	public float Duration { get; private set; }

	/// <summary>Position capped at the endpoint, retaining negative seek positions.</summary>
	public float Elapsed => Duration - Position >= 0f ? Position : Duration;
	internal float Position { get; private set; }
	internal float PreviousPosition { get; private set; }
	internal float Rate { get; private set; }
	internal uint WrapCount { get; private set; }
	internal bool Looping { get; }
	internal bool PhaseControlled { get; }
	internal float AuthoredDuration => ( _frameCount - 1f ) / _frameRate;
	internal float EffectiveRate => _rateScale * Rate;

	internal void ConsumePoseHistory()
	{
		PreviousPosition = Position;
		WrapCount = 0;
	}

	/// <summary>Selects an authored frame while retaining the preceding sample and detecting backward movement.</summary>
	internal void SelectFrame( int frame )
	{
		PreviousPosition = Position;
		var phase = frame / ( _frameCount - 1f );
		var bounded = -phase >= 0f ? 0f : phase;
		bounded = 1f - bounded >= 0f ? bounded : 1f;
		Position = bounded * Duration;
		WrapCount = Position < PreviousPosition ? 1u : 0u;
	}

	/// <summary>Advances elapsed time or follows an external phase, preserving untouched completion fields.</summary>
	public void Advance( float delta, float phase, ref AnimationPlaybackCompletion completion )
	{
		if ( PhaseControlled )
		{
			PreviousPosition = Position;
			var clampedPhase = -phase >= 0f ? 0f : phase;
			clampedPhase = 1f - clampedPhase >= 0f ? clampedPhase : 1f;
			Position = clampedPhase * Duration;
			WrapCount = Position < PreviousPosition ? 1u : 0u;
			return;
		}

		Position += delta;
		completion = completion with { Remaining = Duration - Position };
		if ( Position <= Duration )
		{
			completion = completion with { Finished = false };
			return;
		}

		completion = completion with { Finished = true, UnusedDelta = Position - Duration };
		if ( !Looping )
			return;

		do
		{
			completion = completion with { UnusedDelta = Position - Duration };
			Position -= Duration;
			WrapCount = unchecked( WrapCount + 1 );
		}
		while ( Position > Duration );
	}

	/// <summary>Sets both time samples without clamping and clears the wrap counter.</summary>
	public void Seek( float time )
	{
		PreviousPosition = time;
		Position = time;
		WrapCount = 0;
	}

	/// <summary>Rescales both time samples and duration while preserving frame position.</summary>
	public void SetRate( float rate )
	{
		var previousScaledRate = _rateScale * Rate;
		var scaledFrameRate = _frameRate * _rateScale;
		Rate = rate;
		var nextScaledRate = rate * _rateScale;
		PreviousPosition = ( previousScaledRate * PreviousPosition ) / nextScaledRate;
		Duration = ( _frameCount - 1f ) / ( scaledFrameRate * rate );
		Position = ( previousScaledRate * Position ) / nextScaledRate;
	}
}
