using System;
using System.IO;
using System.Text;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public sealed class PoseStreamAssetTests
{
	private static readonly AnimationPoseStreamKey Name = new( 1, 2, 3, 4, 5, 6 );

	[Fact]
	public void SignedKeysAndAllNameWordsSurviveLoading()
	{
		var clip = RawAnimationClipSet.Read( Build() ).Find( "sample" );
		Assert.True( clip.HasPoseStreams );
		Assert.Single( clip.PoseStreams.ToArray() );
		var track = clip.PoseStreams[ 0 ];
		Assert.Equal( Name, track.Name );
		Assert.Equal( Name, track.PairedName );
		Assert.Equal( new AnimationPoseStreamKeyframe( -8191, 2 ), track.Keys[ 0 ] );
		Assert.Equal( new AnimationPoseStreamKeyframe( 8191, 6 ), track.Keys[ 1 ] );
		Assert.Equal( -1, clip.Trajectory.Value.BoneIndex );
		Assert.Equal( 1f, clip.SampleBone( .5f, 0, false ).Scale.W );
	}

	[Fact]
	public void LoadedStreamsUseThePlaybackClockIncludingRateChanges()
	{
		var clip = RawAnimationClipSet.Read( Build() ).Find( "sample" );
		var playback = new AnimationClipPlayback( clip, 1, new AnimationPoseSamplingContext( 0, false ) );
		var context = new AnimationPoseStreamContext( 16 );
		playback.Seek( .5f );
		playback.SamplePoseStreams( context );
		var before = context.Commands( 0 ).ToArray();
		Assert.Equal( new uint[] { 2u << 23, 6u << 23, 0x400bffffu, 0x80200000u }, before );
		Assert.True( playback.Prepare( true, false, 0 ) );
		playback.SetRate( 2f );
		context.Reset();
		playback.SamplePoseStreams( context );
		Assert.Equal( .25f, playback.Elapsed );
		Assert.Equal( before, context.Commands( 0 ).ToArray() );
	}

	[Theory]
	[InlineData( 2u )]
	[InlineData( 3u )]
	public void LegacyStreamsAreUnknownAndCannotSilentlySampleEmpty( uint version )
	{
		var clip = RawAnimationClipSet.Read( Build( version: version ) ).Find( "sample" );
		Assert.False( clip.HasPoseStreams );
		var playback = new AnimationClipPlayback( clip, 1, new AnimationPoseSamplingContext( 0, false ) );
		var context = new AnimationPoseStreamContext( 16 );
		Assert.Throws<InvalidOperationException>( () => playback.SamplePoseStreams( context ) );
		Assert.Empty( context.Commands( 0 ).ToArray() );
	}

	[Fact]
	public void ExplicitlyEmptyTracksStillEmitUnsuppliedSlotValues()
	{
		var clip = RawAnimationClipSet.Read( Build( tracks: 0 ) ).Find( "sample" );
		Assert.True( clip.HasPoseStreams );
		var playback = new AnimationClipPlayback( clip, 1, new AnimationPoseSamplingContext( 0, false ) );
		var context = new AnimationPoseStreamContext( 16 );
		playback.SamplePoseStreams( context );
		Assert.Equal( 0, context.NameCount );
		for ( var slot = 0; slot < 3; slot++ )
			Assert.Equal( new uint[] { 0 }, context.Commands( slot ).ToArray() );
	}

	[Fact]
	public void EmptyNamedTrackPreservesRegistration()
	{
		var clip = RawAnimationClipSet.Read( Build( keys: 0 ) ).Find( "sample" );
		var playback = new AnimationClipPlayback( clip, 1, new AnimationPoseSamplingContext( 0, false ) );
		var context = new AnimationPoseStreamContext( 16 );
		playback.SamplePoseStreams( context );
		Assert.Equal( 0, context.Find( Name ) );
		Assert.Equal( new uint[] { 0 }, context.Commands( 0 ).ToArray() );
	}

	[Theory]
	[InlineData( 128, -8191 )]
	[InlineData( 6, 8192 )]
	public void InvalidValueOrDecreasingTimesAreRejected( ushort value, short firstTime )
	{
		Assert.Throws<FormatException>( () => RawAnimationClipSet.Read( Build( value: value, firstTime: firstTime ) ) );
	}

	[Fact]
	public void ExcessiveCountsAndEveryTruncatedPrefixAreRejected()
	{
		Assert.Throws<FormatException>( () => RawAnimationClipSet.Read( Build( tracks: 32 ) ) );
		Assert.Throws<FormatException>( () => RawAnimationClipSet.Read( Build( keys: uint.MaxValue ) ) );
		var bytes = Build();
		for ( var length = 0; length < bytes.Length; length++ )
		{
			var prefix = bytes.AsSpan( 0, length ).ToArray();
			Assert.Throws<FormatException>( () => RawAnimationClipSet.Read( prefix ) );
		}
	}

	private static byte[] Build( uint version = 4, byte tracks = 1, uint keys = 2,
		ushort value = 6, short firstTime = -8191 )
	{
		using var stream = new MemoryStream();
		using var writer = new BinaryWriter( stream );
		writer.Write( Encoding.ASCII.GetBytes( "SMAN" ) );
		writer.Write( version );
		writer.Write( (ushort)1 );
		Text( writer, "root" );
		writer.Write( (short)-1 );
		writer.Write( (ushort)1 );
		foreach ( var valuePart in new[] { 0f, 0f, 0f, 1f, 0f, 0f, 0f, 1f, 1f, 1f, 1f, 1f } )
			writer.Write( valuePart );
		writer.Write( 1u );
		Text( writer, "sample" );
		writer.Write( 1f );
		writer.Write( 1f );
		writer.Write( (ushort)2 );
		writer.Write( (ushort)0 );
		writer.Write( (byte)1 );
		writer.Write( (byte)0 );
		if ( version >= 3 )
		{
			writer.Write( -1 );
			foreach ( var part in new[] { 0f, 0f, 0f, 0f, 0f, 0f, 1f } )
				writer.Write( part );
		}
		if ( version >= 4 )
		{
			writer.Write( tracks );
			for ( var track = 0; track < tracks; track++ )
			{
				for ( var side = 0; side < 2; side++ )
					for ( var word = 1u; word <= 6; word++ )
						writer.Write( word );
				writer.Write( keys );
				if ( keys != 0 )
				{
					writer.Write( firstTime );
					writer.Write( (ushort)2 );
					writer.Write( (short)8191 );
					writer.Write( value );
				}
			}
		}
		writer.Write( new byte[] { 0, 0, 0 } );
		return stream.ToArray();
	}

	private static void Text( BinaryWriter writer, string value )
	{
		var bytes = Encoding.UTF8.GetBytes( value );
		writer.Write( (byte)bytes.Length );
		writer.Write( bytes );
	}
}
