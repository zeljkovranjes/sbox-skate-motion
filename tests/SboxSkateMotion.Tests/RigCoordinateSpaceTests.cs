using System;
using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class RigCoordinateSpaceTests
{
	[Fact]
	public void CyclicAxesConvertVectorsAndUnnormalizedQuaternions()
	{
		var space = new RigCoordinateSpace( new[] { 3, 1, 2 } );
		Assert.Equal( new Vector3( 3f, 1f, 2f ), space.ToClip( new Vector3( 1f, 2f, 3f ) ) );
		var rotation = new Quaternion( 1f, 2f, 3f, 4f );
		Assert.Equal( new Quaternion( 3f, 1f, 2f, 4f ), space.ToClip( rotation ) );
		Assert.Equal( rotation, space.ToRig( space.ToClip( rotation ) ) );
	}

	[Theory]
	[InlineData( 1, 2, 3 )]
	[InlineData( 3, 1, 2 )]
	[InlineData( 1, -2, -3 )]
	[InlineData( -3, 2, 1 )]
	public void AffineTransformChangesBothItsInputAndOutputBasis( int x, int y, int z )
	{
		var space = new RigCoordinateSpace( new[] { x, y, z } );
		var pose = new LimbRigidTransform( new LimbFrame( new Vector3( 1f, 2f, 3f ), new Vector3( 4f, 5f, 6f ), new Vector3( 7f, 8f, 10f ) ), new Vector3( 11f, 12f, 13f ) );
		var point = new Vector3( 2f, 3f, 5f );
		Assert.Equal( space.ToClip( pose.Transform( point ) ), space.ToClip( pose ).Transform( space.ToClip( point ) ) );
		Assert.Equal( pose, space.ToRig( space.ToClip( pose ) ) );
	}

	[Theory]
	[InlineData( 0, 2, 3 )]
	[InlineData( 1, 1, 3 )]
	[InlineData( 1, 2, 4 )]
	[InlineData( 1, 3, 2 )]
	[InlineData( -1, 2, 3 )]
	public void InvalidOrReflectedAxesAreRejected( int x, int y, int z )
	{
		Assert.Throws<ArgumentException>( () => new RigCoordinateSpace( new[] { x, y, z } ) );
	}

	[Fact]
	public void MutatingDefinitionAxesCannotChangeTheBoundSpace()
	{
		var axes = new[] { 3, 1, 2 };
		var space = new RigCoordinateSpace( axes );
		axes[ 0 ] = 2;
		Assert.Equal( Vector3.UnitY, space.ToClip( Vector3.UnitX ) );
	}
}
