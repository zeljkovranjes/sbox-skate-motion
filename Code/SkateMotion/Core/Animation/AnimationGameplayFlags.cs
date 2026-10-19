using System;

namespace SkateMotion.Core.Animation;

/// <summary>Retains gameplay flags assigned by compiled behavior entry and exit.</summary>
internal sealed class AnimationGameplayFlags
{
	internal AnimationGameplayFlags( uint initialWord ) => Word = initialWord;

	internal uint Word { get; private set; }

	internal bool IsBodyTiltEnabled => (Word & (uint)AnimationGameplayFlag.BodyTilt) != 0;

	internal bool IsManualing => (Word & (uint)AnimationGameplayFlag.Manual) != 0;

	internal bool IsDoingTrick => (Word & (uint)AnimationGameplayFlag.Trick) != 0;

	internal bool IsPowerSliding => (Word & (uint)AnimationGameplayFlag.PowerSlide) != 0;

	internal void Enter( AnimationGameplayFlag flag ) => Set( flag, true );

	internal void Exit( AnimationGameplayFlag flag ) => Set( flag, false );

	internal void Update()
	{
		// These behavior families retain their entry assignment throughout Update.
	}

	internal CompiledStateBehaviorBinding Bind( AnimationGameplayFlag flag,
		Func<int> create, Action transition )
	{
		Validate( flag );
		ArgumentNullException.ThrowIfNull( create );
		ArgumentNullException.ThrowIfNull( transition );
		// Instance allocation, reference lifetime and transition dispatch belong to
		// the surrounding compiled owner. Entry and exit do not inspect the instance.
		return new( create, _ => Enter( flag ), _ => Exit( flag ), transition );
	}

	private void Set( AnimationGameplayFlag flag, bool value )
	{
		Validate( flag );
		var mask = (uint)flag;
		Word = value ? Word | mask : Word & ~mask;
	}

	private static void Validate( AnimationGameplayFlag flag )
	{
		if ( flag is not AnimationGameplayFlag.BodyTilt and not AnimationGameplayFlag.Manual and not AnimationGameplayFlag.Trick
			and not AnimationGameplayFlag.PowerSlide )
			throw new ArgumentOutOfRangeException( nameof( flag ) );
	}
}
