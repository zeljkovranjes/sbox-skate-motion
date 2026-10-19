using System;
using System.IO;
using System.Text;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public sealed class ContinuousPoseAssetTests
{
	[Fact]
	public void ContinuousKeysCrossBlockBoundaryAndPreserveMask()
	{
		var clip = Load();
		Assert.Equal( AnimationClipSamplingMode.Continuous, clip.SamplingMode );
		var pose = clip.SampleBone( 7.5f, 0, true );
		Assert.Equal( 7.4999924f, pose.Position.X );
		Assert.Equal( .25f, pose.Position.W );
		Assert.Equal( 1f, pose.Scale.W );
		Assert.Equal( 1f, pose.Rotation.W );
	}

	[Fact]
	public void DirectAndFinalFramesKeepRawRotationAndScaleLanes()
	{
		var clip = Load();
		foreach ( var pose in new[] { clip.SampleBone( 7.8f, 0, false ), clip.SampleBone( 100f, 0, true ) } )
		{
			Assert.Equal( 2f, pose.Rotation.W );
			Assert.Equal( .75f, pose.Scale.W );
		}
		Assert.Equal( 7f, clip.SampleBone( 7.8f, 0, false ).Position.X );
		Assert.Equal( 9f, clip.SampleBone( 100f, 0, true ).Position.X );
	}

	[Fact]
	public void VersionFiveStillSupportsBlockSampling()
	{
		var clip = Load( sampler: 1 );
		Assert.Equal( AnimationClipSamplingMode.Block, clip.SamplingMode );
		Assert.Equal( 8f, clip.SampleBone( 7.5f, 0, true ).Position.X );
		Assert.Equal( 3f, clip.SampleBone( 7.5f, 0, true ).Scale.W );
	}

	[Theory]
	[InlineData( 2u )]
	[InlineData( 3u )]
	[InlineData( 4u )]
	public void EarlierVersionsRejectContinuousSampling( uint version )
	{
		Assert.Throws<FormatException>( () => Load( version ) );
	}

	[Fact]
	public void UnknownModeAndTruncatedContainersAreRejected()
	{
		Assert.Throws<FormatException>( () => Load( sampler: 3 ) );
		var bytes = Build();
		for ( var length = 0; length < bytes.Length; length++ )
		{
			var prefix = bytes.AsSpan( 0, length ).ToArray();
			Assert.Throws<FormatException>( () => RawAnimationClipSet.Read( prefix ) );
		}
	}

	[Fact]
	public void DisabledTrajectoryIntervalUsesContinuousSampling()
	{
		var clip = Load();
		var output = new AnimationBoneTransform[ 1 ];
		clip.SampleInterval( 6.5f, 7.5f, 0, true, output );
		Assert.Equal( clip.SampleBone( 7.5f, 0, true ), output[ 0 ] );
	}

	private static RawAnimationClip Load( uint version = 5, byte sampler = 2 ) =>
		RawAnimationClipSet.Read( Build( version, sampler ) ).Find( "sample" );

	private static byte[] Build( uint version = 5, byte sampler = 2 )
	{
		using var stream = new MemoryStream();
		using var writer = new BinaryWriter( stream );
		writer.Write( Encoding.ASCII.GetBytes( "SMAN" ) );
		writer.Write( version );
		writer.Write( (ushort)1 );
		Text( writer, "root" );
		writer.Write( (short)-1 );
		writer.Write( (ushort)1 );
		foreach ( var value in new[] { 0f, 0f, 0f, 1f, 0f, 0f, 0f, 1f, 1f, 1f, 1f, 1f } )
			writer.Write( value );
		writer.Write( 1u );
		Text( writer, "sample" );
		writer.Write( 1f );
		writer.Write( 1f );
		writer.Write( (ushort)10 );
		writer.Write( (ushort)0 );
		writer.Write( sampler );
		writer.Write( (byte)0 );
		if ( version >= 3 )
		{
			writer.Write( -1 );
			foreach ( var value in new[] { 0f, 0f, 0f, 0f, 0f, 0f, 1f } )
				writer.Write( value );
		}
		if ( version >= 4 )
			writer.Write( (byte)0 );
		writer.Write( new byte[] { 2, 1, 1 } );
		for ( var frame = 0; frame < 10; frame++ )
		{
			writer.Write( (float)frame );
			writer.Write( 0f );
			writer.Write( 0f );
			writer.Write( .25f );
		}
		foreach ( var value in new[] { 0f, 0f, 0f, 2f, 2f, 3f, 4f, .75f } )
			writer.Write( value );
		return stream.ToArray();
	}

	private static void Text( BinaryWriter writer, string value )
	{
		var bytes = Encoding.UTF8.GetBytes( value );
		writer.Write( (byte)bytes.Length );
		writer.Write( bytes );
	}
}
