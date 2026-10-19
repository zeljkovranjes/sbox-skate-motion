using System;
using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class LimbBoneTransformTests
{
	[Fact]
	public void ShearAndScaleAreRemovedWithoutMovingTheOrigin()
	{
		var bone = new LimbRigidTransform( new LimbFrame(
			new Vector3( 2f, 1f, 3f ), new Vector3( 0f, 3f, 2f ), new Vector3( 0f, 0f, 4f ) ),
			new Vector3( 4f, 5f, 6f ) );
		var result = LimbRigidTransform.Identity.ApplyToBone( bone );
		Assert.Equal( LimbRigidTransform.Identity.Rotation, result.Rotation );
		Assert.Equal( bone.Translation, result.Translation );
	}

	[Fact]
	public void BoneOriginUsesTheDeltaRotationBeforeItsTranslation()
	{
		var delta = new LimbRigidTransform( new LimbFrame( Vector3.UnitY, -Vector3.UnitX, Vector3.UnitZ ),
			new Vector3( 5f, 6f, 7f ) );
		var bone = new LimbRigidTransform( LimbRigidTransform.Identity.Rotation, new Vector3( 2f, 3f, 4f ) );
		var result = delta.ApplyToBone( bone );
		Assert.Equal( new Vector3( 2f, 8f, 11f ), result.Translation );
		Assert.Equal( delta.Rotation, result.Rotation );
	}

	[Fact]
	public void ReflectionKeepsTheBoneHandedness()
	{
		var reflected = new LimbRigidTransform( new LimbFrame( -Vector3.UnitX * 2f,
			Vector3.UnitY * 3f, Vector3.UnitZ * 4f ), Vector3.Zero );
		var result = LimbRigidTransform.Identity.ApplyToBone( reflected );
		Assert.Equal( -Vector3.UnitX, result.Rotation.X );
		Assert.Equal( Vector3.UnitY, result.Rotation.Y );
		Assert.Equal( Vector3.UnitZ, result.Rotation.Z );
	}

	[Fact]
	public void CollapsedAxisRemainsNonfiniteForTheCallingRig()
	{
		var bone = new LimbRigidTransform( new LimbFrame( Vector3.UnitX, Vector3.UnitY, Vector3.Zero ), Vector3.One );
		var result = LimbRigidTransform.Identity.ApplyToBone( bone );
		Assert.True( float.IsNaN( result.Rotation.X.X ) );
		Assert.True( float.IsNaN( result.Rotation.Y.Y ) );
		Assert.True( float.IsNaN( result.Rotation.Z.Z ) );
		Assert.Equal( Vector3.One, result.Translation );
	}

	[Fact]
	public void SpatialFrameKeepsItsLastAxisAndRemovesEarlierProjections()
	{
		var frame = new LimbFrame( new Vector3( 2f, .2f, .4f ),
			new Vector3( -.3f, 3f, .7f ), new Vector3( .1f, .4f, 2f ) );
		var result = frame.Orthonormalized();
		Assert.InRange( Vector3.Distance( Vector3.Normalize( frame.Z ), result.Z ), 0f, 1e-6f );
		foreach ( var axis in new[] { result.X, result.Y, result.Z } )
			Assert.InRange( axis.Length(), .999999f, 1.000001f );
		Assert.InRange( MathF.Abs( Vector3.Dot( result.X, result.Y ) ), 0f, 1e-6f );
		Assert.InRange( MathF.Abs( Vector3.Dot( result.X, result.Z ) ), 0f, 1e-6f );
		Assert.InRange( MathF.Abs( Vector3.Dot( result.Y, result.Z ) ), 0f, 1e-6f );
	}

	[Fact]
	public void DeltaActsOnTheExistingBoneOrientation()
	{
		var delta = new LimbRigidTransform( new LimbFrame( Vector3.UnitY, -Vector3.UnitX, Vector3.UnitZ ), Vector3.Zero );
		var bone = new LimbRigidTransform( new LimbFrame( Vector3.UnitX, Vector3.UnitZ, -Vector3.UnitY ), Vector3.Zero );
		var result = delta.ApplyToBone( bone );
		Assert.Equal( Vector3.UnitY, result.Rotation.X );
		Assert.Equal( Vector3.UnitZ, result.Rotation.Y );
		Assert.Equal( Vector3.UnitX, result.Rotation.Z );
	}
}
