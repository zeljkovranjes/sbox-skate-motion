using System.Collections.Generic;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public sealed class AnimationClipPoseControlTests
{
	[Fact]
	public void SamplingUsesAuthoredSecondsAndPreservesUnsignedArguments()
	{
		var clock = new AnimationClipClock( 61, 30, .5f, false, false );
		clock.Seek( 1f );
		clock.SetRate( 2f );
		var completion = AnimationPlaybackCompletion.Empty;
		clock.Advance( .25f, 0f, ref completion );
		var backend = new Backend( clock );
		var control = new AnimationClipPoseControl( clock, backend );
		Assert.True( control.Prepare( true, false, 0xffffffff ) );
		Assert.Equal( ( ushort.MaxValue, .5f, .75f, 0u, uint.MaxValue ), backend.Sampled );
		Assert.Equal( new[] { "query", "resolve", "sample", "query", "query" }, backend.Events );
		Assert.True( control.HasPose );
	}

	[Fact]
	public void WrappedSampleRetainsPreviousTimeUntilAfterSampling()
	{
		var clock = new AnimationClipClock( 31, 30, 1, true, false );
		clock.Seek( .75f );
		var completion = AnimationPlaybackCompletion.Empty;
		clock.Advance( .5f, 0f, ref completion );
		var backend = new Backend( clock );
		var control = new AnimationClipPoseControl( clock, backend );
		control.Prepare( true, true, 7 );
		Assert.Equal( ( ushort.MaxValue, .75f, .25f, 1u, 7u ), backend.Sampled );
		Assert.Equal( ( .75f, 1u ), backend.SampleHistory );
		Assert.Equal( new[] { ( .75f, 1u ), ( .25f, 0u ), ( .25f, 0u ) }, backend.QueryHistory );
		Assert.Equal( .25f, clock.PreviousPosition );
		Assert.Equal( 0u, clock.WrapCount );
	}

	[Fact]
	public void ReverseUnwrappedSampleStartsAtCurrentTime()
	{
		var clock = new AnimationClipClock( 31, 30, 1, false, false );
		clock.Seek( .75f );
		var completion = AnimationPlaybackCompletion.Empty;
		clock.Advance( -.5f, 0f, ref completion );
		var backend = new Backend( clock );
		new AnimationClipPoseControl( clock, backend ).Prepare( true, false, 0 );
		Assert.Equal( ( ushort.MaxValue, .25f, .25f, 0u, 0u ), backend.Sampled );
		Assert.Equal( .75f, clock.PreviousPosition );
	}

	[Fact]
	public void DisabledPreparationConsumesHistoryWithoutBackendCalls()
	{
		var clock = new AnimationClipClock( 31, 30, 1, true, false );
		var backend = new Backend( clock );
		var control = new AnimationClipPoseControl( clock, backend );
		control.Prepare( true, false, 0 );
		backend.Events.Clear();
		var completion = AnimationPlaybackCompletion.Empty;
		clock.Advance( 2.25f, 0f, ref completion );
		Assert.False( control.Prepare( false, true, 0 ) );
		Assert.False( control.HasPose );
		Assert.Empty( backend.Events );
		Assert.Equal( .25f, clock.PreviousPosition );
		Assert.Equal( 0u, clock.WrapCount );
	}

	[Fact]
	public void SuppressionSkipsSamplingButStillConsumesHistory()
	{
		var clock = new AnimationClipClock( 31, 30, 1, true, false );
		var completion = AnimationPlaybackCompletion.Empty;
		clock.Advance( 1.25f, 0f, ref completion );
		var backend = new Backend( clock ) { Observations = new[] { true, true, true } };
		var control = new AnimationClipPoseControl( clock, backend );
		Assert.False( control.Prepare( true, true, 0 ) );
		Assert.False( control.HasPose );
		Assert.Equal( new[] { "query", "query", "query" }, backend.Events );
		Assert.Equal( new[] { ( 0f, 1u ), ( .25f, 0u ), ( .25f, 0u ) }, backend.QueryHistory );
	}

	[Fact]
	public void StoredValidityAndReturnedValidityUseSeparateObservations()
	{
		var clock = new AnimationClipClock( 31, 30, 1, false, false );
		var backend = new Backend( clock ) { Observations = new[] { true, false, true } };
		var control = new AnimationClipPoseControl( clock, backend );
		Assert.False( control.Prepare( true, false, 0 ) );
		Assert.True( control.HasPose );
		Assert.Equal( new[] { "query", "query", "query" }, backend.Events );
	}

	[Fact]
	public void SampleRangeClampsButConsumedHistoryKeepsRawOvershoot()
	{
		var clock = new AnimationClipClock( 31, 30, 1, false, false );
		clock.Seek( -.5f );
		var completion = AnimationPlaybackCompletion.Empty;
		clock.Advance( 2f, 0f, ref completion );
		var backend = new Backend( clock );
		new AnimationClipPoseControl( clock, backend ).Prepare( true, true, 0 );
		Assert.Equal( ( ushort.MaxValue, 0f, 1f, 0u, 0u ), backend.Sampled );
		Assert.Equal( 1.5f, clock.PreviousPosition );
		Assert.Equal( 1f, clock.Elapsed );
	}

	private sealed class Backend : IAnimationClipPoseBackend
	{
		private readonly AnimationClipClock _clock;
		private int _query;

		internal Backend( AnimationClipClock clock ) => _clock = clock;

		internal bool[] Observations { get; set; } = new bool[ 3 ];
		internal List<string> Events { get; } = new();
		internal List<(float, uint)> QueryHistory { get; } = new();
		internal (ushort, float, float, uint, uint) Sampled { get; private set; }
		internal (float, uint) SampleHistory { get; private set; }

		public bool Suppressed
		{
			get
			{
				Events.Add( "query" );
				QueryHistory.Add( ( _clock.PreviousPosition, _clock.WrapCount ) );
				return Observations[ _query++ % Observations.Length ];
			}
		}

		public ushort ResolveClip()
		{
			Events.Add( "resolve" );
			return ushort.MaxValue;
		}

		public void Sample( ushort clip, float start, float current, uint wraps, uint flags )
		{
			Events.Add( "sample" );
			Sampled = ( clip, start, current, wraps, flags );
			SampleHistory = ( _clock.PreviousPosition, _clock.WrapCount );
		}
	}
}
