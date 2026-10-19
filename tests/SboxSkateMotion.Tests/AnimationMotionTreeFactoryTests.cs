using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class AnimationMotionTreeFactoryTests
{
	[Fact]
	public void NestedParametersPreserveAuthoredSamplesAndIndependentSlotClocks()
	{
		var clips = Bank();
		var definitions = Definitions();
		var factory = new AnimationMotionTreeFactory( clips, definitions, new[] { "outer" }, clips.Reference( 0 ).ToArray() );
		factory.UpdateOverrides( new[] { Parameter( "height", .5f, true ), Parameter( "tilt", 0f, false ) } );
		var first = (AnimationMotionTree)factory.Create( 0, 0 );
		Assert.Equal( 1.5f, first.Root.Duration );
		Assert.True( first.PoseRoot.Prepare( true, false, 1 ) );
		Assert.Equal( 10f, first.PoseRoot.Pose.Positions[ 0 ].X, 5 );
		var complete = AnimationPlaybackCompletion.Empty;
		first.Root.Advance( .3f, 0f, ref complete );
		var second = (AnimationMotionTree)factory.Create( 1, 0 );
		Assert.Equal( 0f, second.Root.Elapsed );
		Assert.Equal( .3f, first.Root.Elapsed, 5 );
		factory.UpdateOverrides( new[] { Parameter( "height", .5f, true ), Parameter( "tilt", 1f, false ) } );
		Assert.True( first.PoseRoot.Prepare( true, false, 1 ) );
		Assert.Equal( 15f, first.PoseRoot.Pose.Positions[ 0 ].X, 5 );
		Assert.False( factory.UpdateOverrides( Array.Empty<AnimationAttributeOverride>() ) );
		Assert.True( first.PoseRoot.Prepare( true, false, 1 ) );
		Assert.Equal( 15f, first.PoseRoot.Pose.Positions[ 0 ].X, 5 );
		factory.Publish( first.PoseRoot );
		Assert.Same( first.PoseRoot, factory.PublishedRoot );
	}

	[Fact]
	public void StaticSelectorsUseConstructionKeyAndUnsupportedKindsAreExplicit()
	{
		var clips = Bank();
		var definitions = Definitions();
		definitions[ "choice" ] = new BlendDefinition
		{
			SourceKind = 8, SampleAttribute = "style", Children = new()
			{
				new() { Name = "low", Key = "first" }, new() { Name = "high", Key = "second" }
			}
		};
		definitions[ "unknown" ] = new BlendDefinition { SourceKind = 11 };
		var selectors = new Dictionary<AnimationAttributeKey, AnimationAttributeKey>
		{
			[ AnimationAttributeKey.Encode( "style" ) ] = AnimationAttributeKey.Encode( "second" )
		};
		var factory = new AnimationMotionTreeFactory( clips, definitions,
			new[] { "choice", "unknown", "missing" }, clips.Reference( 0 ).ToArray(), selectors );
		var tree = (AnimationMotionTree)factory.Create( 0, 0 );
		Assert.True( tree.PoseRoot.Prepare( true, false, 1 ) );
		Assert.Equal( 30f, tree.PoseRoot.Pose.Positions[ 0 ].X, 5 );
		Assert.Throws<NotSupportedException>( () => factory.Create( 1, 1 ) );
		Assert.Null( factory.Create( 2, 2 ) );
	}

	private static Dictionary<string, BlendDefinition> Definitions() => new()
	{
		[ "outer" ] = new() { SourceKind = 7, SampleAttribute = "height", Children = new()
		{
			new() { Name = "low", At = 123f }, new() { Name = "inner", At = -456f }
		} },
		[ "inner" ] = new() { SourceKind = 7, SampleAttribute = "tilt", Children = new()
		{
			new() { Name = "middle", At = 100f }, new() { Name = "high", At = -100f }
		} }
	};

	private static AnimationAttributeOverride Parameter( string name, float value, bool normalized ) => new(
		new( new( BitConverter.SingleToUInt32Bits( value ), 0, 0, 0, 0, 0 ), AnimationAttributeKey.Encode( name ),
			-1f, -1f, 2, 0 ), -1, normalized );

	private static RawAnimationClipSet Bank()
	{
		using var raw = new MemoryStream();
		using var writer = new BinaryWriter( raw );
		writer.Write( 0x4e414d53u ); writer.Write( 2u ); writer.Write( (ushort)1 );
		Text( writer, "root" ); writer.Write( (short)-1 ); writer.Write( (ushort)1 );
		Transform( writer, 0f ); writer.Write( 3u );
		var names = new[] { "low", "middle", "high" };
		for ( var i = 0; i < names.Length; ++i )
		{
			Text( writer, names[ i ] ); writer.Write( i == 0 ? 1f : 2f ); writer.Write( 1f );
			writer.Write( (ushort)3 ); writer.Write( (ushort)0 ); writer.Write( (byte)1 ); writer.Write( (byte)0 );
			writer.Write( new byte[] { 1, 1, 1 } ); Transform( writer, i == 0 ? 0f : i == 1 ? 10f : 30f );
		}
		using var attributes = new MemoryStream();
		using var output = new BinaryWriter( attributes );
		output.Write( 0x54414d53u ); output.Write( 1u ); output.Write( 3u );
		for ( var i = 0; i < names.Length; ++i )
		{
			Text( output, names[ i ] );
			for ( var word = 0; word < 6; ++word ) output.Write( 0u );
			output.Write( 2u );
			Attribute( output, "height", i == 0 ? 0f : 10f );
			Attribute( output, "tilt", i == 1 ? -1f : 1f );
		}
		return RawAnimationClipSet.ReadWithAttributes( raw.ToArray(), AnimationClipAttributeBank.Read( attributes.ToArray() ) );
	}

	private static void Text( BinaryWriter writer, string value )
	{
		writer.Write( (byte)value.Length ); writer.Write( System.Text.Encoding.UTF8.GetBytes( value ) );
	}

	private static void Transform( BinaryWriter writer, float position )
	{
		foreach ( var value in new[] { position, 0f, 0f, 1f, 0f, 0f, 0f, 1f, 1f, 1f, 1f, 1f } ) writer.Write( value );
	}

	private static void Attribute( BinaryWriter writer, string name, float value )
	{
		var key = AnimationAttributeKey.Encode( name );
		foreach ( var word in new[] { key.A, key.B, key.C, key.D, key.E } ) writer.Write( word );
		writer.Write( -1f ); writer.Write( -1f ); writer.Write( (byte)0 ); writer.Write( value );
		for ( var i = 0; i < 5; ++i ) writer.Write( 0u );
		writer.Write( 0u );
	}
}
