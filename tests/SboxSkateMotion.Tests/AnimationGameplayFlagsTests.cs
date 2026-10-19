using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public sealed class AnimationGameplayFlagsTests
{
	[Fact]
	public void LifecycleAssignmentsPreserveOtherBitsAndDoNotCountRepeatedEntries()
	{
		var flags = new AnimationGameplayFlags( 0xa1234567 );
		flags.Enter( AnimationGameplayFlag.Manual );
		flags.Enter( AnimationGameplayFlag.Manual );
		flags.Enter( AnimationGameplayFlag.Trick );
		flags.Enter( AnimationGameplayFlag.PowerSlide );
		Assert.Equal( 0xbd234567u, flags.Word );
		Assert.True( flags.IsManualing );
		Assert.True( flags.IsDoingTrick );
		Assert.True( flags.IsPowerSliding );
		flags.Exit( AnimationGameplayFlag.Manual );
		flags.Update();
		Assert.False( flags.IsManualing );
		Assert.Equal( 0xad234567u, flags.Word );
		flags.Exit( AnimationGameplayFlag.Trick );
		flags.Exit( AnimationGameplayFlag.PowerSlide );
		Assert.Equal( 0xa1234567u, flags.Word );
	}

	[Fact]
	public void CompiledBindingsUseTheSharedProviderAndExplicitOwnerCallbacks()
	{
		var flags = new AnimationGameplayFlags( uint.MaxValue );
		var transitions = 0;
		var manual = flags.Bind( AnimationGameplayFlag.Manual, () => 57, () => transitions++ );
		var slide = flags.Bind( AnimationGameplayFlag.PowerSlide, () => 93, () => transitions++ );
		Assert.Equal( 57, manual.Create() );
		Assert.Equal( 93, slide.Create() );
		slide.Exit( 93 );
		flags.Exit( AnimationGameplayFlag.Trick );
		manual.Exit( 57 );
		Assert.Equal( 0xe3ffffffu, flags.Word );
		slide.Enter( 93 );
		manual.Enter( 57 );
		Assert.Equal( 0xf7ffffffu, flags.Word );
		manual.Transition();
		Assert.Equal( 1, transitions );
		Assert.Throws<ArgumentOutOfRangeException>( () => flags.Enter( (AnimationGameplayFlag)0 ) );
		Assert.Equal( 0xf7ffffffu, flags.Word );
	}
}
