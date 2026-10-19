using System;
using System.IO;
using System.Numerics;
using System.Text;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class RawAnimationClipTests
{
	[Fact]
	public void ReaderKeepsRawKeysReferenceAndMetadataSeparate()
	{
		var set = RawAnimationClipSet.Read( Build() );
		var clip = set.Find( "sample" );
		Assert.Equal( 1, set.Count );
		Assert.Equal( 1, set.ReferenceCount );
		Assert.Equal( -1, set.Parents[ 0 ] );
		Assert.Equal( 7f, set.ReferenceBone( 0, 0 ).Position.X );
		Assert.Equal( .5f, clip.RateScale );
		Assert.True( clip.Looping );
		Assert.True( clip.PhaseControlled );
		var pose = clip.SampleBone( 3.75f, 0, false );
		Assert.Equal( 3f, pose.Position.X );
		Assert.Equal( .375f, pose.Position.W );
		Assert.Equal( 2f, pose.Rotation.W );
		Assert.Equal( 1f, pose.Scale.W );
	}

	[Fact]
	public void InterpolationUsesPackedWeightAndAuxiliaryScaleLane()
	{
		var clip = RawAnimationClipSet.Read( Build() ).Find( "sample" );
		var pose = clip.SampleBone( 3.5f, 0, true );
		Assert.Equal( 3.4999924f, pose.Position.X );
		Assert.Equal( .375f, pose.Position.W );
		Assert.InRange( pose.Rotation.W, .9999998f, 1.0000002f );
		Assert.Equal( 3f, pose.Scale.W );
	}

	[Theory]
	[InlineData( 7f, 8f, true )]
	[InlineData( 7.75f, 8f, true )]
	[InlineData( 9f, 9f, false )]
	[InlineData( 100f, 9f, false )]
	public void BoundaryAndEndKeepTheirDistinctNormalizationModes( float time, float position, bool normalized )
	{
		var pose = RawAnimationClipSet.Read( Build() ).Find( "sample" ).SampleBone( time, 0, true );
		Assert.Equal( position, pose.Position.X );
		Assert.InRange( pose.Rotation.W, normalized ? .9999998f : 2f, normalized ? 1.0000002f : 2f );
		Assert.Equal( normalized ? 3f : 1f, pose.Scale.W );
	}

	[Fact]
	public void SingleFrameDirectFetchRetainsMagnitude()
	{
		var clip = RawAnimationClipSet.Read( Build( 1 ) ).Find( "sample" );
		var pose = clip.SampleBone( 100f, 0, true );
		Assert.Equal( 2f, pose.Rotation.W );
		Assert.Equal( 1f, pose.Scale.W );
	}

	[Fact]
	public void ReaderRejectsTruncatedAndTrailingBytes()
	{
		var bytes = Build();
		Assert.Throws<FormatException>( () => RawAnimationClipSet.Read( bytes.AsSpan( 0, bytes.Length - 1 ) ) );
		Array.Resize( ref bytes, bytes.Length + 1 );
		Assert.Throws<FormatException>( () => RawAnimationClipSet.Read( bytes ) );
	}

	[Fact]
	public void InvalidReferenceAndNonfiniteValuesAreRejected()
	{
		Assert.Throws<FormatException>( () => RawAnimationClipSet.Read( Build( reference: 1 ) ) );
		Assert.Throws<FormatException>( () => RawAnimationClipSet.Read( Build( rotationW: float.NaN ) ) );
	}

	[Fact]
	public void ReusedOutputDoesNotAllocateDuringSampling()
	{
		var clip = RawAnimationClipSet.Read( Build() ).Find( "sample" );
		var output = new AnimationBoneTransform[ 1 ];
		for ( var i = 0; i < 1000; i++ )
			clip.Sample( 3.5f, true, output );
		var before = GC.GetAllocatedBytesForCurrentThread();
		for ( var i = 0; i < 1000; i++ )
			clip.Sample( 3.5f, true, output );
		Assert.Equal( 0, GC.GetAllocatedBytesForCurrentThread() - before );
	}

	private static byte[] Build( int frames = 10, int reference = 0, float rotationW = 2f )
	{
		using var stream = new MemoryStream();
		using var writer = new BinaryWriter( stream );
		writer.Write( Encoding.ASCII.GetBytes( "SMAN" ) );
		writer.Write( 2u );
		writer.Write( (ushort)1 );
		Text( writer, "root" );
		writer.Write( (short)-1 );
		writer.Write( (ushort)1 );
		Vector( writer, new Vector4( 7f, 0f, 0f, 1f ) );
		Vector( writer, new Vector4( 0f, 0f, 0f, 1f ) );
		Vector( writer, Vector4.One );
		writer.Write( 1u );
		Text( writer, "sample" );
		writer.Write( 1f );
		writer.Write( .5f );
		writer.Write( (ushort)frames );
		writer.Write( (ushort)reference );
		writer.Write( (byte)1 );
		writer.Write( (byte)3 );
		writer.Write( new byte[] { 2, 1, 1 } );
		for ( var frame = 0; frame < frames; frame++ )
			Vector( writer, new Vector4( frame, 2f, 3f, .375f ) );
		Vector( writer, new Vector4( 0f, 0f, 0f, rotationW ) );
		Vector( writer, new Vector4( 1f, 3f, 4f, 1f ) );
		return stream.ToArray();
	}

	private static void Text( BinaryWriter writer, string value )
	{
		var bytes = Encoding.UTF8.GetBytes( value );
		writer.Write( (byte)bytes.Length );
		writer.Write( bytes );
	}

	private static void Vector( BinaryWriter writer, Vector4 value )
	{
		writer.Write( value.X );
		writer.Write( value.Y );
		writer.Write( value.Z );
		writer.Write( value.W );
	}
}
