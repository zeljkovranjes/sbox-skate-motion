using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class AnimationBaseMotionTreeFactoryTests
{
	[Fact]
	public void IndexedReferenceConsumesOnlySuccessfulRequestsAndPreservesSignedSelection()
	{
		var indices = new List<int>();
		var indexed = new AnimationIndexedReferenceState( index =>
		{
			indices.Add( index );
			return Reference( 7f );
		} );
		var motion = Reference( 1f );
		Assert.Same( motion, indexed.Compose( motion ) );
		indexed.Pending = true;
		indexed.BaseIndex = 4;
		indexed.MaximumIndex = 5;
		Assert.Same( motion, indexed.Compose( motion ) );
		Assert.True( indexed.Pending );
		indexed.Offset = 3;
		Assert.Null( indexed.Compose( null ) );
		Assert.True( indexed.Pending );
		var root = indexed.Compose( motion );
		Assert.False( indexed.Pending );
		Assert.Equal( new[] { 5 }, indices );
		Assert.True( root.Prepare( true, false, 1 ) );
		Assert.Equal( 8f, root.Pose.Positions[ 0 ].X );
		Assert.Same( motion, indexed.Compose( motion ) );
		indexed.Pending = true;
		indexed.BaseIndex = int.MaxValue;
		indexed.Offset = 1;
		indexed.Compose( motion );
		Assert.Equal( int.MinValue, indices[ 1 ] );
	}

	[Fact]
	public void BaseOwnerConstructsIndexedAndOwnerReferencesBeforeRatingAndPublishing()
	{
		var clips = Bank();
		var indices = new List<int>();
		var events = new List<string>();
		var indexed = new AnimationIndexedReferenceState( index =>
		{
			indices.Add( index );
			events.Add( "indexed" );
			return Reference( 7f );
		} ) { BaseIndex = 4, MaximumIndex = 12, Offset = 3, Pending = true };
		ulong flags = 0x00200000;
		var composition = new AnimationOwnerTreeComposition( () =>
		{
			events.Add( "owner" );
			return Reference( 10f );
		}, () => Reference( 0f ), () => Reference( 0f ),
			() => throw new InvalidOperationException( "No posture requested in this case." ),
			() => flags, value => flags = value );
		var motions = new AnimationMotionTreeFactory( clips, new Dictionary<string, BlendDefinition>(),
			new[] { "motion", "missing" }, new[] { Transform( 0f ) },
			publish: _ => events.Add( "published" ) );
		var factory = new AnimationBaseMotionTreeFactory( motions, indexed, composition );
		var owner = new AnimationBasePoseTreeOwner( factory );
		Assert.False( owner.Replace( new( 1, AnimationBaseTransition.Play, 2f ) ) );
		Assert.True( indexed.Pending );
		Assert.Equal( 0x00200000UL, flags );
		Assert.Empty( events );
		Assert.True( owner.Replace( new( 0, AnimationBaseTransition.Play, 2f ) ) );
		Assert.Equal( new[] { "indexed", "owner", "published" }, events );
		Assert.Equal( new[] { 7 }, indices );
		Assert.False( indexed.Pending );
		Assert.Equal( 0UL, flags );
		Assert.Equal( .5f, owner.Duration );
		Assert.Same( owner.Root, motions.PublishedRoot );
		Assert.True( owner.Prepare( true, false, 1 ) );
		Assert.Equal( 18f, owner.Pose.Positions[ 0 ].X );
		owner.AdvanceFrame( .25f, 0f );
		Assert.Equal( .25f, owner.Elapsed );
		Assert.True( owner.Replace( new( 0, AnimationBaseTransition.Sequence, 1f ) ) );
		Assert.Single( indices );
		Assert.Equal( 1, owner.RetainedCount );
		Assert.True( owner.Prepare( true, false, 1 ) );
		Assert.Equal( 18f, owner.Pose.Positions[ 0 ].X );
	}

	private static AnimationBoneTransform Transform( float x ) =>
		new( new Vector4( x, 0f, 0f, 1f ), Quaternion.Identity, Vector4.One );

	private static IAnimationPosePlaybackNode Reference( float x ) =>
		new AnimationReferencePosePlayback( new[] { Transform( x ) } );

	private static RawAnimationClipSet Bank()
	{
		using var data = new MemoryStream();
		using var writer = new BinaryWriter( data );
		writer.Write( 0x4e414d53u );
		writer.Write( 2u );
		writer.Write( (ushort)1 );
		Text( writer, "root" );
		writer.Write( (short)-1 );
		writer.Write( (ushort)1 );
		WriteTransform( writer, 0f );
		writer.Write( 1u );
		Text( writer, "motion" );
		writer.Write( 2f );
		writer.Write( 1f );
		writer.Write( (ushort)3 );
		writer.Write( (ushort)0 );
		writer.Write( (byte)1 );
		writer.Write( (byte)0 );
		writer.Write( new byte[] { 1, 1, 1 } );
		WriteTransform( writer, 1f );
		using var attributes = new MemoryStream();
		using var output = new BinaryWriter( attributes );
		output.Write( 0x54414d53u );
		output.Write( 1u );
		output.Write( 1u );
		Text( output, "motion" );
		for ( var i = 0; i < 7; i++ )
			output.Write( 0u );
		return RawAnimationClipSet.ReadWithAttributes( data.ToArray(),
			AnimationClipAttributeBank.Read( attributes.ToArray() ) );
	}

	private static void Text( BinaryWriter writer, string value )
	{
		writer.Write( (byte)value.Length );
		writer.Write( System.Text.Encoding.UTF8.GetBytes( value ) );
	}

	private static void WriteTransform( BinaryWriter writer, float x )
	{
		foreach ( var value in new[] { x, 0f, 0f, 1f, 0f, 0f, 0f, 1f, 1f, 1f, 1f, 1f } )
			writer.Write( value );
	}
}
