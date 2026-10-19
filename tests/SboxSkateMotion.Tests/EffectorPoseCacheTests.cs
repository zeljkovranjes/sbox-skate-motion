using System;
using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class EffectorPoseCacheTests
{
	[Fact]
	public void FreshCacheSuppressesItsInitialRootOffsetDelta()
	{
		var cache = new EffectorPoseCache( 2 );
		Assert.Equal( 0f, cache.DeltaScale );
		Assert.All( cache.Bones, bone => Assert.Equal( LimbRigidTransform.Identity, bone ) );
		cache.Update( new[] { At( 1f ), At( 5f ) }, new[] { 0f, 1f }, LimbRigidTransform.Identity );
		Assert.Equal( new Vector3( 4f, 0f, 0f ), cache.RootOffset );
		Assert.Equal( Vector3.Zero, cache.RootOffsetDelta );
		Assert.Equal( 1f, cache.DeltaScale );
	}

	[Fact]
	public void CacheOwnsAnExactCopyOfMutablePoses()
	{
		var pose = new LimbRigidTransform( new LimbFrame( Vector3.UnitY, -Vector3.UnitX, 3f * Vector3.UnitZ ), new Vector3( 2f, 3f, 4f ) );
		var source = new[] { pose };
		var cache = new EffectorPoseCache( 1 );
		cache.Update( source, new[] { 1f }, LimbRigidTransform.Identity );
		source[ 0 ] = At( 50f );
		Assert.Equal( pose, cache.Bones[ 0 ] );
	}

	[Fact]
	public void WeightsAreNeitherNormalizedNorClamped()
	{
		var cache = new EffectorPoseCache( 2 );
		cache.Update( new[] { At( 2f ), At( 5f ) }, new[] { -1f, 2f }, LimbRigidTransform.Identity );
		Assert.Equal( new Vector3( 8f, 0f, 0f ), cache.WeightedPosition );
		Assert.Equal( new Vector3( 6f, 0f, 0f ), cache.RootOffset );
	}

	[Fact]
	public void SubsequentUpdatesUsePriorPositionAndOffset()
	{
		var cache = new EffectorPoseCache( 2 );
		cache.Update( new[] { At( 1f ), At( 5f ) }, new[] { 0f, 1f }, LimbRigidTransform.Identity );
		cache.Update( new[] { At( 3f ), At( 8f ) }, new[] { 0f, 1f }, LimbRigidTransform.Identity );
		Assert.Equal( new Vector3( 3f, 0f, 0f ), cache.WeightedPositionDelta );
		Assert.Equal( Vector3.UnitX, cache.RootOffsetDelta );
		Assert.Equal( At( 3f ), cache.Bones[ 0 ] );
	}

	[Fact]
	public void ZeroDeltaScaleSuppressesOneUpdateAndThenResets()
	{
		var cache = new EffectorPoseCache( 2 ) { DeltaScale = 0f };
		cache.Update( new[] { At( 1f ), At( 5f ) }, new[] { 0f, 1f }, LimbRigidTransform.Identity );
		Assert.Equal( Vector3.Zero, cache.RootOffsetDelta );
		Assert.Equal( new Vector3( 4f, 0f, 0f ), cache.RootOffset );
		Assert.Equal( 1f, cache.DeltaScale );
		cache.Update( new[] { At( 1f ), At( 7f ) }, new[] { 0f, 1f }, LimbRigidTransform.Identity );
		Assert.Equal( new Vector3( 2f, 0f, 0f ), cache.RootOffsetDelta );
	}

	[Fact]
	public void DeltaScaleAppliesOnlyToTheRootOffsetChange()
	{
		var cache = new EffectorPoseCache( 2 )
		{
			WeightedPosition = new Vector3( 3f, 0f, 0f ),
			RootOffset = Vector3.UnitX,
			DeltaScale = -2f
		};
		cache.Update( new[] { At( 2f ), At( 5f ) }, new[] { 0f, 1f }, LimbRigidTransform.Identity );
		Assert.Equal( new Vector3( 2f, 0f, 0f ), cache.WeightedPositionDelta );
		Assert.Equal( new Vector3( -4f, 0f, 0f ), cache.RootOffsetDelta );
		Assert.Equal( new Vector3( 3f, 0f, 0f ), cache.RootOffset );
	}

	[Fact]
	public void FrameRotationAndScaleApplyToTheRootOffset()
	{
		var frame = new LimbRigidTransform( new LimbFrame( 2f * Vector3.UnitY, -Vector3.UnitX, Vector3.UnitZ ), new Vector3( 10f, 20f, 30f ) );
		var cache = new EffectorPoseCache( 2 );
		cache.Update( new[] { At( 1f ), At( 4f ) }, new[] { 0f, 1f }, frame );
		Assert.Equal( new Vector3( 0f, 6f, 0f ), cache.RootOffset );
	}

	[Fact]
	public void TransformedPointsAreRoundedBeforeSubtraction()
	{
		var cache = new EffectorPoseCache( 2 );
		cache.Update( new[] { At( 0f ), At( 1f ) }, new[] { 0f, 1f }, At( 16777216f ) );
		Assert.Equal( Vector3.UnitX, cache.WeightedPosition );
		Assert.Equal( Vector3.Zero, cache.RootOffset );
	}

	[Fact]
	public void WeightedPositionAccumulatesInBoneOrder()
	{
		var cache = new EffectorPoseCache( 3 );
		cache.Update( new[] { At( 16777216f ), At( 1f ), At( -16777216f ) }, new[] { 1f, 1f, 1f }, LimbRigidTransform.Identity );
		Assert.Equal( Vector3.Zero, cache.WeightedPosition );
	}

	[Theory]
	[InlineData( 1, 2 )]
	[InlineData( 2, 1 )]
	public void InvalidCountsLeaveCacheAndStateUntouched( int poses, int weights )
	{
		var cache = new EffectorPoseCache( 2 ) { WeightedPosition = Vector3.UnitY, DeltaScale = .5f };
		cache.Bones[ 0 ] = At( 9f );
		Assert.Throws<ArgumentException>( () => cache.Update( new LimbRigidTransform[ poses ], new float[ weights ], LimbRigidTransform.Identity ) );
		Assert.Equal( At( 9f ), cache.Bones[ 0 ] );
		Assert.Equal( Vector3.UnitY, cache.WeightedPosition );
		Assert.Equal( .5f, cache.DeltaScale );
	}

	[Theory]
	[InlineData( 0 )]
	[InlineData( -1 )]
	public void CacheRequiresARootBone( int count )
	{
		Assert.Throws<ArgumentOutOfRangeException>( () => new EffectorPoseCache( count ) );
	}

	private static LimbRigidTransform At( float x ) => new( LimbRigidTransform.Identity.Rotation, new Vector3( x, 0f, 0f ) );
}
