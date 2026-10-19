using System.Numerics;
using SkateMotion.Core.Collision;
using SkateMotion.Core.Constraints;
using Xunit;

namespace SboxSkateMotion.Tests;

public class BoardCollisionInputsTests
{
	[Fact]
	public void CacheKeepsCurrentInertiaAndForcesWhileOwnerAssignsResponseMode()
	{
		var body = new DynamicBody
		{
			Rotation = Quaternion.Identity,
			Position = new Vector3( 1, 2, 3 ),
			Velocity = new Vector3( 4, 5, 6 ),
			AngularVelocity = new Vector3( 7, 8, 9 ),
			Acceleration = new Vector3( 10, 11, 12 ),
			AngularAcceleration = new Vector3( 13, 14, 15 ),
			InverseMass = .25f,
			WorldInverseInertia = new Matrix4x4( 16, 17, 18, 0, 17, 19, 20, 0, 18, 20, 21, 0, 0, 0, 0, 1 ),
			MotionMetric = 22,
			MotionFlags = 99,
			QuietSteps = 23
		};
		var output = new uint[ 44 ];
		CollisionBodyCache.Write( body, 4, output );
		Assert.Equal( new uint[] { W( 16 ), W( 17 ), W( 18 ), W( .25f ), W( 21 ), W( 19 ), W( 20 ), 4 }, output[ 28..36 ] );
		Assert.Equal( new uint[] { W( 10 ), W( 11 ), W( 12 ), W( 22 ), W( 13 ), W( 14 ), W( 15 ), 23 }, output[ 36..44 ] );
		Assert.Equal( W( 1 ), output[ 7 ] );
		Assert.Equal( 99, body.MotionFlags );
		CollisionBodyCache.Write( body, 1, output );
		Assert.Equal( 1u, output[ 35 ] );
	}

	[Fact]
	public void GeometryKeepsOwnerOriginsAndRefreshesRetainedInputs()
	{
		var producer = new BoardCollisionInputs( new Vector3( .8f, .22f, .025f ),
			new Vector3( .045f, .19f, .025f ), .028f, .035f, 50 );
		var bodies = Enumerable.Range( 0, 7 ).Select( i => new DynamicBody
		{
			Rotation = Quaternion.Identity,
			Position = new Vector3( i, 2, 3 ),
			InverseMass = 1,
			WorldInverseInertia = Matrix4x4.Identity
		} ).ToArray();
		producer.Update( bodies, 3, 17, .6f, .4f, .1f );
		var retained = producer.Bodies.Span[ 3 ];
		Assert.Equal( 5, retained.ShapeKind );
		Assert.Equal( 4u, retained.BodyIdentity );
		Assert.Equal( 53u, retained.ShapeIdentity );
		Assert.Equal( W( 1 ), retained.Shape.Span[ 9 ] );
		Assert.Equal( W( .0175f ), retained.Shape.Span[ 17 ] );
		Assert.Equal( W( .028f ), retained.Shape.Span[ 18 ] );
		Assert.Equal( W( 3 ), retained.Transform.Span[ 12 ] );
		Assert.Equal( 0x3d4ccccdu, retained.BaseMargin );
		Assert.Equal( 3u, retained.Category );
		Assert.Equal( 17u, retained.Material );
		bodies[ 3 ].Position = new Vector3( 4, 5, 6 );
		bodies[ 3 ].Acceleration = new Vector3( 1, 2, 3 );
		producer.Update( bodies, 3, 17, .6f, .4f, .1f, false );
		Assert.Equal( W( 4 ), retained.Transform.Span[ 12 ] );
		Assert.Equal( W( 1 ), retained.BodyState.Span[ 36 ] );
		Assert.False( producer.Bodies.Span[ 3 ].Active );
		Assert.Equal( 4u, retained.BodyState.Span[ 35 ] );
	}

	[Theory]
	[InlineData( 4, 0x3f5de9e2u, 0xbf637e90u, 0x4000e0deu, 0x3fdb487fu, 0xbf26bee0u, 0x4002f62cu )]
	[InlineData( 5, 0x3fa0f41fu, 0xbf4aa162u, 0x3fffac72u, 0x3fa94951u, 0xbf3f9c0eu, 0x400400d1u )]
	public void StationaryBoundsPreserveRoundedPrimitiveExtentOrder( int kind, uint minX, uint minY,
		uint minZ, uint maxX, uint maxY, uint maxZ )
	{
		var shape = new uint[ 24 ];
		shape[ 0 ] = shape[ 5 ] = shape[ 10 ] = shape[ 15 ] = W( 1 );
		shape[ 12 ] = W( .04f );
		shape[ 13 ] = W( -.02f );
		shape[ 14 ] = W( .03f );
		shape[ 17 ] = W( .4f );
		shape[ 18 ] = W( .11f );
		shape[ 19 ] = W( .0125f );
		shape[ 20 ] = W( .003f );
		if ( kind == 5 )
		{
			shape[ 5 ] = shape[ 10 ] = 0;
			shape[ 6 ] = W( -1 );
			shape[ 9 ] = W( 1 );
			shape[ 17 ] = W( .0175f );
			shape[ 18 ] = W( .028f );
		}
		var body = new DynamicBody { Rotation = Quaternion.Identity, Position = new Vector3( 1.25f, -.75f, 2 ) };
		var transform = new uint[ 16 ];
		var cache = new uint[ 44 ];
		var output = new uint[ 8 ];
		CollisionBodyCache.WriteTransform( body, transform );
		CollisionBodyCache.Write( body, 4, cache );
		CollisionBodyBounds.Write( kind, shape, transform, cache, output );
		Assert.Equal( new uint[] { minX, minY, minZ, 0, maxX, maxY, maxZ, 0 }, output );
	}

	private static uint W( float value ) => BitConverter.SingleToUInt32Bits( value );
}
