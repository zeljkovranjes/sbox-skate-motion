using System;
using System.IO;
using System.Text;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class AnimationAttributeBankTests
{
	private static readonly AnimationAttributeKey Key = new( 1, 2, 3, 4, uint.MaxValue );

	[Theory]
	[InlineData( 2u )]
	[InlineData( 3u )]
	[InlineData( 4u )]
	[InlineData( 5u )]
	public void ExistingPoseVersionsBindAllIdentityAndValueWords( uint version )
	{
		var bank = AnimationClipAttributeBank.Read( Bank() );
		var set = RawAnimationClipSet.ReadWithAttributes( Poses( version ), bank );
		var clip = set.Find( "sample" );
		Assert.Same( bank.Find( "sample" ), clip.Attributes );
		Assert.Equal( new uint[] { 6, 5, 4, 3, 2, 1 }, clip.Attributes.NameWords.ToArray() );
		var playback = new AnimationClipPlayback( clip, 1, new AnimationPoseSamplingContext( 0, false ) );
		Assert.True( playback.HasAttributes );
		var result = default( AnimationAttributeQueryResult );
		Assert.True( playback.TryGetAuthoredAttribute( Key, 15, ref result ) );
		Assert.Equal( new AnimationAttributePayload( 0x80000000, 2, 3, 4, 5, uint.MaxValue ), result.Payload );
		Assert.Equal( Key, result.Name );
		Assert.True( playback.Prepare( true, false, 0 ) );
		Assert.Equal( 1f, playback.Pose.Scales[ 0 ].W );
	}

	[Fact]
	public void UnknownAttributesThrowWhileExplicitlyEmptyAttributesReturnAMiss()
	{
		var old = new AnimationClipPlayback( RawAnimationClipSet.Read( Poses() ).Find( "sample" ), 1, new AnimationPoseSamplingContext( 0, false ) );
		var result = default( AnimationAttributeQueryResult );
		Assert.False( old.HasAttributes );
		Assert.Throws<InvalidOperationException>( () => old.TryGetAuthoredAttribute( Key, 15, ref result ) );
		var set = RawAnimationClipSet.ReadWithAttributes( Poses(), AnimationClipAttributeBank.Read( Bank( empty: true ) ) );
		var playback = new AnimationClipPlayback( set.Find( "sample" ), 1, new AnimationPoseSamplingContext( 0, false ) );
		Assert.True( playback.HasAttributes );
		Assert.False( playback.TryGetAuthoredAttribute( Key, 15, ref result ) );
	}

	[Fact]
	public void MissingClipMetadataFailsDuringBinding()
	{
		var bank = AnimationClipAttributeBank.Read( Bank( name: "other" ) );
		Assert.Throws<FormatException>( () => RawAnimationClipSet.ReadWithAttributes( Poses(), bank ) );
	}

	[Fact]
	public void LoadedCurveQueriesFollowTheBoundPlaybackClock()
	{
		var bank = AnimationClipAttributeBank.Read( Bank( curve: true ) );
		var clip = RawAnimationClipSet.ReadWithAttributes( Poses(), bank ).Find( "sample" );
		var playback = new AnimationClipPlayback( clip, 1, new AnimationPoseSamplingContext( 0, false ) );
		playback.Seek( .5f );
		var result = default( AnimationAttributeQueryResult );
		Assert.True( playback.TryGetAuthoredAttribute( Key, 15, ref result ) );
		Assert.Equal( 3.5f, result.Payload.Scalar );
		playback.SetRate( 2f );
		Assert.True( playback.TryGetAuthoredAttribute( Key, 15, ref result ) );
		Assert.Equal( 3.5f, result.Payload.Scalar );
	}

	[Fact]
	public void EveryTruncatedPrefixAndTrailingBytesAreRejected()
	{
		var bytes = Bank( curve: true );
		for ( var length = 0; length < bytes.Length; length++ )
		{
			var prefix = bytes.AsSpan( 0, length ).ToArray();
			Assert.Throws<FormatException>( () => AnimationClipAttributeBank.Read( prefix ) );
		}
		Array.Resize( ref bytes, bytes.Length + 1 );
		Assert.Throws<FormatException>( () => AnimationClipAttributeBank.Read( bytes ) );
	}

	[Theory]
	[InlineData( 8 )]
	[InlineData( 43 )]
	[InlineData( 100 )]
	public void OversizedCountsAreRejectedBeforeAllocation( int offset )
	{
		var bytes = Bank();
		for ( var index = 0; index < 4; index++ )
			bytes[ offset + index ] = 255;
		Assert.Throws<FormatException>( () => AnimationClipAttributeBank.Read( bytes ) );
	}

	[Fact]
	public void DuplicateClipNamesAreRejected()
	{
		Assert.Throws<FormatException>( () => AnimationClipAttributeBank.Read( Bank( clips: 2 ) ) );
	}

	[Fact]
	public void AttributeSetCopiesCallerOwnedIdentityAndRecordArrays()
	{
		var words = new uint[ 6 ];
		var record = new AnimationClipAttribute( Key, -1f, -1f, 0, default );
		var records = new[] { record };
		var set = new AnimationClipAttributeSet( "sample", words, records );
		words[ 0 ] = 42;
		records[ 0 ] = null;
		Assert.Equal( 0u, set.NameWords[ 0 ] );
		Assert.Same( record, set.Records[ 0 ] );
	}

	private static byte[] Bank( bool curve = false, bool empty = false, string name = "sample", uint clips = 1 )
	{
		using var stream = new MemoryStream();
		using var writer = new BinaryWriter( stream );
		writer.Write( Encoding.ASCII.GetBytes( "SMAT" ) );
		writer.Write( 1u );
		writer.Write( clips );
		for ( var clip = 0u; clip < clips; clip++ )
		{
			Text( writer, name );
			for ( var word = 6u; word > 0; word-- )
				writer.Write( word );
			writer.Write( empty ? 0u : 1u );
			if ( empty )
				continue;
			foreach ( var word in new uint[] { 1, 2, 3, 4, uint.MaxValue } )
				writer.Write( word );
			writer.Write( -1f );
			writer.Write( -1f );
			writer.Write( curve ? (byte)2 : (byte)3 );
			foreach ( var word in new uint[] { 0x80000000, 2, 3, 4, 5, uint.MaxValue } )
				writer.Write( word );
			writer.Write( curve ? 2u : 0u );
			if ( curve )
			{
				writer.Write( 0f );
				writer.Write( 0f );
				writer.Write( 30f );
				writer.Write( 7f );
			}
		}
		return stream.ToArray();
	}

	private static byte[] Poses( uint version = 4 )
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
		writer.Write( 30f );
		writer.Write( 1f );
		writer.Write( (ushort)31 );
		writer.Write( (ushort)0 );
		writer.Write( (byte)1 );
		writer.Write( (byte)0 );
		if ( version >= 3 )
		{
			writer.Write( -1 );
			foreach ( var value in new[] { 0f, 0f, 0f, 0f, 0f, 0f, 1f } )
				writer.Write( value );
		}
		if ( version >= 4 )
			writer.Write( (byte)0 );
		writer.Write( new byte[ 3 ] );
		return stream.ToArray();
	}

	private static void Text( BinaryWriter writer, string value )
	{
		var bytes = Encoding.UTF8.GetBytes( value );
		writer.Write( (byte)bytes.Length );
		writer.Write( bytes );
	}
}
