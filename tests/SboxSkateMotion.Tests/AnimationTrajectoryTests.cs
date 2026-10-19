using System;
using System.IO;
using System.Numerics;
using System.Text;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class AnimationTrajectoryTests
{
	[Fact]
	public void ReaderPreservesTrajectoryWithoutNormalizingItsRotation()
	{
		var clip = RawAnimationClipSet.Read( Build( rotationW: 2f ) ).Find( "sample" );
		Assert.Equal( 0, clip.Trajectory.Value.BoneIndex );
		Assert.Equal( new Vector3( 10f, 0f, 0f ), clip.Trajectory.Value.Translation );
		Assert.Equal( 2f, clip.Trajectory.Value.Rotation.W );
	}

	[Fact]
	public void IntervalSamplingChangesOnlyTrajectoryAndKeepsScaleMaskAndSpareCapacity()
	{
		var clip = RawAnimationClipSet.Read( Build() ).Find( "sample" );
		var output = new AnimationBoneTransform[ 3 ];
		output[ 2 ] = new AnimationBoneTransform( new Vector4( 88f ), Quaternion.Identity, Vector4.One );
		clip.SampleInterval( 0f, 1f, 0, false, output );
		Assert.Equal( new Vector4( 2f, 0f, 0f, .25f ), output[ 0 ].Position );
		Assert.Equal( new Vector4( 2f, 3f, 4f, 5f ), output[ 0 ].Scale );
		Assert.Equal( clip.SampleBone( 1f, 1, false ).Position, output[ 1 ].Position );
		Assert.Equal( new Vector4( 88f ), output[ 2 ].Position );
	}

	[Fact]
	public void CycleTransformIsAppliedOnceForAnyNonzeroWrapCount()
	{
		var clip = RawAnimationClipSet.Read( Build() ).Find( "sample" );
		var first = new AnimationBoneTransform[ 2 ];
		var second = new AnimationBoneTransform[ 2 ];
		clip.SampleInterval( 1f, 0f, 1, false, first );
		clip.SampleInterval( 1f, 0f, 2, false, second );
		Assert.Equal( new Vector4( 8f, 0f, 0f, .25f ), first[ 0 ].Position );
		Assert.Equal( first[ 0 ].Position, second[ 0 ].Position );
		Assert.Equal( first[ 0 ].Rotation, second[ 0 ].Rotation );
	}

	[Fact]
	public void UnknownTrajectoryFailsBeforeMutationButExplicitlyDisabledTrajectorySamplesLocals()
	{
		var unknown = RawAnimationClipSet.Read( Build( version: 2 ) ).Find( "sample" );
		var disabled = RawAnimationClipSet.Read( Build( bone: -1 ) ).Find( "sample" );
		var output = new AnimationBoneTransform[ 2 ];
		Assert.Null( unknown.Trajectory );
		Assert.Throws<InvalidOperationException>( () => unknown.SampleInterval( 1f, 1f, 0, false, output ) );
		Assert.Equal( Vector4.Zero, output[ 0 ].Position );
		disabled.SampleInterval( 1f, 1f, 0, false, output );
		Assert.Equal( new Vector4( 2f, 0f, 0f, .25f ), output[ 0 ].Position );
	}

	[Fact]
	public void InvalidTrajectoryAndIntervalInputsAreRejected()
	{
		Assert.Throws<FormatException>( () => RawAnimationClipSet.Read( Build( bone: 2 ) ) );
		Assert.Throws<FormatException>( () => RawAnimationClipSet.Read( Build( bone: -2 ) ) );
		Assert.Throws<FormatException>( () => RawAnimationClipSet.Read( Build( rotationW: float.NaN ) ) );
		var clip = RawAnimationClipSet.Read( Build() ).Find( "sample" );
		var output = new AnimationBoneTransform[ 2 ];
		Assert.Throws<ArgumentOutOfRangeException>( () => clip.SampleInterval( -1f, 0f, 0, false, output ) );
		Assert.Throws<ArgumentOutOfRangeException>( () => clip.SampleInterval( 0f, float.NaN, 0, false, output ) );
		Assert.Throws<ArgumentException>( () => clip.SampleInterval( 0f, 0f, 0, false, new AnimationBoneTransform[ 1 ] ) );
		Assert.Equal( Vector4.Zero, output[ 0 ].Position );
	}

	[Fact]
	public void DeltaRetainsOrderedNoncommutingRotationProducts()
	{
		var previous = new AnimationBoneTransform( Vector4.Zero, new Quaternion( 1f, 0f, 0f, 0f ), Vector4.One );
		var current = new AnimationBoneTransform( new Vector4( 0f, 0f, 0f, .75f ),
			new Quaternion( 0f, 1f, 0f, 0f ), new Vector4( 2f, 3f, 4f, 5f ) );
		var trajectory = new AnimationClipTrajectory( 0, Vector3.Zero, new Quaternion( 0f, 0f, 1f, 0f ) );
		var result = AnimationPoseTrajectory.Delta( previous, current, trajectory, true );
		Assert.Equal( new Quaternion( 0f, 0f, 0f, -1f ), result.Rotation );
		Assert.Equal( .75f, result.Position.W );
		Assert.Equal( current.Scale, result.Scale );
	}

	[Fact]
	public void ReferencedIntervalsComposeOnceOnRepeatedSampling()
	{
		var sampler = RawAnimationClipSet.Read( Build() ).CreateReferencedClip( "sample" );
		var output = new AnimationBoneTransform[ 2 ];
		for ( var i = 0; i < 2; i++ )
			sampler.SampleInterval( 1f, 0f, 1, false, output );
		Assert.Equal( new Vector4( 15f, 0f, 0f, .25f ), output[ 0 ].Position );
	}

	[Fact]
	public void IntervalSamplingReusesOutputWithoutAllocating()
	{
		var sampler = RawAnimationClipSet.Read( Build() ).CreateReferencedClip( "sample" );
		var output = new AnimationBoneTransform[ 2 ];
		for ( var i = 0; i < 1000; i++ )
			sampler.SampleInterval( .25f, .75f, 0, true, output );
		var before = GC.GetAllocatedBytesForCurrentThread();
		for ( var i = 0; i < 1000; i++ )
			sampler.SampleInterval( .25f, .75f, 0, true, output );
		Assert.Equal( 0, GC.GetAllocatedBytesForCurrentThread() - before );
	}

	private static byte[] Build( uint version = 3, int bone = 0, float rotationW = 1f )
	{
		using var stream = new MemoryStream();
		using var writer = new BinaryWriter( stream );
		writer.Write( Encoding.ASCII.GetBytes( "SMAN" ) );
		writer.Write( version );
		writer.Write( (ushort)2 );
		Text( writer, "root" );
		writer.Write( (short)-1 );
		Text( writer, "child" );
		writer.Write( (short)0 );
		writer.Write( (ushort)1 );
		for ( var i = 0; i < 2; i++ )
		{
			Vector( writer, new Vector4( 7f, 0f, 0f, 1f ) );
			Vector( writer, new Vector4( 0f, 0f, 0f, 1f ) );
			Vector( writer, Vector4.One );
		}
		writer.Write( 1u );
		Text( writer, "sample" );
		writer.Write( 1f );
		writer.Write( 1f );
		writer.Write( (ushort)2 );
		writer.Write( (ushort)0 );
		writer.Write( (byte)1 );
		writer.Write( (byte)0 );
		if ( version == 3 )
		{
			writer.Write( bone );
			writer.Write( 10f );
			writer.Write( 0f );
			writer.Write( 0f );
			Vector( writer, new Vector4( 0f, 0f, 0f, rotationW ) );
		}
		writer.Write( new byte[] { 2, 0, 1, 1, 0, 0 } );
		Vector( writer, new Vector4( 0f, 0f, 0f, .25f ) );
		Vector( writer, new Vector4( 2f, 0f, 0f, .25f ) );
		Vector( writer, new Vector4( 2f, 3f, 4f, 5f ) );
		Vector( writer, new Vector4( 20f, 0f, 0f, .75f ) );
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
