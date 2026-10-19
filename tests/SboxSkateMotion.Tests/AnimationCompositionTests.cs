using System.Collections.Generic;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class AnimationCompositionTests
{
	[Theory]
	[InlineData( true, 0, 1 )]
	[InlineData( false, 1, 0 )]
	public void PreparationRequiresActivePoseAndIgnoresReferenceReadiness( bool firstActive, int active, int reference )
	{
		var operations = new Operations { Active = active };
		var control = new AnimationCompositionPoseControl( operations, firstActive );
		Assert.True( control.Prepare( false ) );
		Assert.True( control.HasPose );
		control.Sample();
		Assert.Equal( new[] { $"prepare:{active}:False", $"prepare:{reference}:False", $"compose:{firstActive}:True", $"sample:{active}" }, operations.Events );
		operations.Events.Clear();
		operations.Ready = false;
		Assert.False( control.Prepare( true ) );
		Assert.False( control.HasPose );
		Assert.Equal( new[] { $"prepare:{active}:True" }, operations.Events );
	}

	[Theory]
	[InlineData( true )]
	[InlineData( false )]
	public void PlaybackUsesOnlyActiveClockButSeeksAndRatesBothChildren( bool firstActive )
	{
		var first = new AnimationClipClock( 31f, 30f, 1f, false, false );
		var second = new AnimationClipClock( 61f, 30f, 1f, false, false );
		var wrapper = new AnimationCompositionPlayback( first, second, firstActive );
		Assert.Equal( firstActive ? 1f : 2f, wrapper.Duration );
		wrapper.Seek( .25f );
		wrapper.SetRate( .5f );
		Assert.Equal( .5f, first.Elapsed );
		Assert.Equal( .5f, second.Elapsed );
		var completion = new AnimationPlaybackCompletion( 17, true, -1f, -1f );
		wrapper.Advance( .125f, 0f, ref completion );
		Assert.Equal( .625f, wrapper.Elapsed );
		Assert.Equal( .5f, (firstActive ? second : first).Elapsed );
		Assert.Equal( (byte)17, completion.Flags );
		Assert.False( completion.Finished );
		Assert.Equal( -1f, completion.UnusedDelta );
		Assert.Equal( wrapper.Duration - wrapper.Elapsed, completion.Remaining );
	}

	private sealed class Operations : IAnimationCompositionPoseOperations
	{
		internal readonly List<string> Events = new();
		internal int Active;
		internal bool Ready = true;
		public bool Prepare( int child, bool enabled )
		{
			Events.Add( $"prepare:{child}:{enabled}" );
			return child == Active && Ready;
		}
		public void Compose( bool reverse, bool retainFirstTranslationW ) => Events.Add( $"compose:{reverse}:{retainFirstTranslationW}" );
		public void Sample( int child ) => Events.Add( $"sample:{child}" );
	}
}
