using System;
using System.IO;
using System.Numerics;
using System.Text;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class RawRiderPoseBindingTests
{
	[Fact]
	public void BoundGraphSamplingUsesRawReferenceMasksAndScale()
	{
		var clips = Legacy();
		var raw = RawAnimationClipSet.Read( Bank() );
		clips.BindRawPoses( raw );
		var actual = new SkeletonPose( 1 );
		var expected = new SkeletonPose( 1 );
		actual.Sample( clips.Find( "sample" ), clips.Skeleton, .25f, false );
		expected.Sample( raw.CreateReferencedClip( "sample" ), .25f, true );
		Assert.Equal( expected.Transform( 0 ), actual.Transform( 0 ) );
		Assert.Equal( .25f, actual.BoneWeights[ 0 ] );
		Assert.NotEqual( Vector4.One, actual.Scales[ 0 ] );
		Assert.Same( raw, clips.RawPoses );
	}

	[Fact]
	public void ExplicitGraphMaskOverridesRawMaskWithoutChangingTransform()
	{
		var clips = Legacy();
		clips.BindRawPoses( RawAnimationClipSet.Read( Bank() ) );
		var clip = clips.Find( "sample" );
		var pose = new SkeletonPose( 1 );
		pose.Sample( clip, clips.Skeleton, .25f, false );
		var before = pose.Transform( 0 );
		clip.SetBoneWeights( new[] { .75f } );
		pose.Sample( clip, clips.Skeleton, .25f, false );
		Assert.Equal( .75f, pose.BoneWeights[ 0 ] );
		Assert.Equal( before.Rotation, pose.Rotations[ 0 ] );
		Assert.Equal( before.Scale, pose.Scales[ 0 ] );
	}

	[Theory]
	[InlineData( -0.25f, true, .75f )]
	[InlineData( 1.25f, true, .25f )]
	[InlineData( 1f, true, 0f )]
	[InlineData( 1.25f, false, 1f )]
	[InlineData( -0.25f, false, 0f )]
	public void GraphLoopRequestsMapTimeBeforeRawSampling( float time, bool looping, float expected )
	{
		var clip = Legacy().Find( "sample" );
		Assert.Equal( expected, clip.RawSampleTime( time, looping ) );
	}

	[Fact]
	public void ExplicitNonloopPermissionHoldsTheEndpoint()
	{
		var clip = Legacy().Find( "sample" );
		clip.Looping = false;
		Assert.Equal( 1f, clip.RawSampleTime( 1.25f, true ) );
	}

	[Theory]
	[InlineData( "other", "root", 2f )]
	[InlineData( "sample", "other", 2f )]
	[InlineData( "sample", "root", 3f )]
	public void IncompatibleBankFailsBeforeBinding( string name, string bone, float fps )
	{
		var clips = Legacy();
		Assert.Throws<ArgumentException>( () => clips.BindRawPoses( RawAnimationClipSet.Read( Bank( name, bone, fps ) ) ) );
		Assert.Null( clips.RawPoses );
		Assert.Null( clips.Find( "sample" ).RawPose );
	}

	private static ClipSet Legacy()
	{
		var skeleton = new ClipSkeleton( new[] { "root" }, new[] { -1 }, new[] { Vector3.Zero }, new[] { Quaternion.Identity } );
		var clip = new AnimationClip( "sample", 2f, 3, new int[ 1 ], new int[ 1 ], Array.Empty<Quaternion>(),
			new int[ 1 ], new int[ 1 ], Array.Empty<Vector3>() );
		return new ClipSet( skeleton, new[] { clip } );
	}

	private static byte[] Bank( string name = "sample", string bone = "root", float fps = 2f )
	{
		using var stream = new MemoryStream();
		using var writer = new BinaryWriter( stream );
		writer.Write( 0x4e414d53u );
		writer.Write( 2u );
		writer.Write( (ushort)1 );
		Text( writer, bone );
		writer.Write( (short)-1 );
		writer.Write( (ushort)1 );
		foreach ( var value in new[] { 1f, 2f, 3f, 1f, 0f, 0f, 0f, 1f, 1f, 1f, 1f, 1f } )
			writer.Write( value );
		writer.Write( 1u );
		Text( writer, name );
		writer.Write( fps );
		writer.Write( 1f );
		writer.Write( (ushort)3 );
		writer.Write( (ushort)0 );
		writer.Write( (byte)1 );
		writer.Write( (byte)0 );
		writer.Write( new byte[] { 1, 1, 1 } );
		foreach ( var value in new[] { 4f, 5f, 6f, .25f, 0f, 0f, 0f, 1f, 2f, 3f, 4f, 5f } )
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
