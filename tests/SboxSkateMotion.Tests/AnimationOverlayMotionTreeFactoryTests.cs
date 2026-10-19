using SkateMotion.Core.Animation;
using SkateMotion.Core.Input;
using Xunit;

namespace SboxSkateMotion.Tests;

public class AnimationOverlayMotionTreeFactoryTests
{
	[Fact]
	public void RatedSlotsRetainIndependentClocksAndStopUpdatingAfterRelease()
	{
		var clips = Bank();
		var definitions = new Dictionary<string, BlendDefinition>
		{
			[ "pair" ] = new() { SourceKind = 7, SampleAttribute = "height", Children = new()
			{
				new() { Name = "low" }, new() { Name = "high" }
			} }
		};
		var motions = new AnimationMotionTreeFactory( clips, definitions, new[] { "pair", "missing" },
			clips.Reference( 0 ).ToArray(), null, null, null, 6 );
		ulong flags = 0;
		var composition = new AnimationOwnerTreeComposition( Reference, Reference, Reference,
			() => throw new InvalidOperationException( "This case does not request posture." ),
			() => flags, value => flags = value );
		var factory = new AnimationOverlayMotionTreeFactory( motions, composition );
		factory.UpdateOverrides( new[] { Parameter( "height", 5f, false ) } );
		var request = new AnimationChannelReplacement( 0, 2f, false, true, .1f, 0f, .3f, 0x10000000, true );
		var first = (AnimationMotionTree)factory.Create( 5, request );
		Assert.Equal( .75f, first.Root.Duration );
		var complete = AnimationPlaybackCompletion.Empty;
		first.Root.Advance( .125f, 0f, ref complete );
		var second = (AnimationMotionTree)factory.Create( 4, request );
		Assert.Equal( .125f, first.Root.Elapsed, 5 );
		Assert.Equal( 0f, second.Root.Elapsed );
		Assert.True( first.PoseRoot.Prepare( true, false, 1 ) );
		Assert.Equal( 15f, first.PoseRoot.Pose.Positions[ 0 ].X, 5 );
		factory.Release( 5, first );
		factory.UpdateOverrides( new[] { Parameter( "height", 10f, false ) } );
		Assert.True( first.PoseRoot.Prepare( true, false, 1 ) );
		Assert.True( second.PoseRoot.Prepare( true, false, 1 ) );
		Assert.Equal( 15f, first.PoseRoot.Pose.Positions[ 0 ].X, 5 );
		Assert.Equal( 30f, second.PoseRoot.Pose.Positions[ 0 ].X, 5 );
		var replacement = factory.Create( 5, request );
		Assert.Throws<InvalidOperationException>( () => factory.Release( 5, first ) );
		factory.Release( 5, replacement );
		factory.Release( 4, second );
		flags = 0x00200000;
		Assert.Null( factory.Create( 5, request with { Definition = 1 } ) );
		Assert.Equal( 0x00200000UL, flags );

		var owner = new AnimationOverlayOwner( 3, factory );
		owner.PublishBase( Reference() );
		var key = new InputCommandKey( 1, 0, 0, 0, 0, 0 );
		Assert.Equal( AnimationOverlayRequestResult.Success, owner.Create( key, 0, request ) );
		Assert.Equal( 0UL, flags );
		Assert.Equal( AnimationOverlayRequestResult.Success, owner.Request( key, 0, false, request ) );
		Assert.Equal( 4, owner.FreeTreeCount );
		owner.Remove( key );
		Assert.Equal( 6, owner.FreeTreeCount );
		Assert.Equal( 0, owner.Count );

		IAnimationPosePlaybackNode Reference() => new AnimationReferencePosePlayback( clips.Reference( 0 ).ToArray() );
	}

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
