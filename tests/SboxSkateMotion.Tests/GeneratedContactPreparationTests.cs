using System;
using System.Numerics;
using SkateMotion.Core.Animation;
using SkateMotion.Core.Constraints;
using SkateMotion.Core.Physics;
using Xunit;

namespace SboxSkateMotion.Tests;

public class GeneratedContactPreparationTests
{
	[Theory]
	[InlineData( 0f, 1f, 0f )]
	[InlineData( 1f, 0f, 0f )]
	[InlineData( 0f, 0f, -1f )]
	public void StationaryGeneratedContactsRetainNormalAndZeroFrictionAxes( float x, float y, float z )
	{
		var normal = new Vector3( x, y, z );
		var geometry = GeneratedContactPreparation.Prepare( default, default, Point( normal ) );
		Assert.Equal( normal, geometry.Normal );
		Assert.Equal( Vector3.Zero, geometry.TangentA );
		Assert.Equal( Vector3.Zero, geometry.TangentB );
		Assert.Equal( 0u, BitConverter.SingleToUInt32Bits( geometry.TangentA.X ) );
		Assert.Equal( 0xabcd1234u, geometry.Materials );
	}

	[Theory]
	[InlineData( 1f, 0x3f7ffc00u )]
	[InlineData( -1f, 0xbf7ffc00u )]
	public void SlidingDirectionsRetainEstimatePrecisionAndDirection( float speed, uint expected )
	{
		var moving = new DynamicBody { Velocity = new( speed, 0f, 0f ) };
		var geometry = GeneratedContactPreparation.Prepare( default, moving, Point( Vector3.UnitY ) );
		Assert.Equal( new Vector3( speed, 0f, 0f ), geometry.MotionTerm );
		Assert.Equal( expected, BitConverter.SingleToUInt32Bits( geometry.TangentA.Z ) );
		Assert.Equal( expected, BitConverter.SingleToUInt32Bits( geometry.TangentB.X ) );
		Assert.Equal( 0f, geometry.TangentA.X );
		Assert.Equal( 0f, geometry.TangentB.Z );
	}

	[Fact]
	public void MovingNonrespondingBodyStillContributesPointVelocity()
	{
		var a = new DynamicBody { Position = new( 2f, 0f, 0f ), AngularVelocity = Vector3.UnitZ };
		var b = new DynamicBody { Velocity = new( 0f, 2f, 0f ), InverseMass = 0f };
		var contact = Point( Vector3.UnitZ ) with { PointA = new( 3f, 0f, 0f ) };
		var inputs = ContactInputs.FromGeneratedManifold( a, b, contact );
		Assert.Equal( Vector3.UnitY, inputs.Geometry.MotionTerm );
		Assert.Equal( 0x3f7ffc00u, BitConverter.SingleToUInt32Bits( inputs.Geometry.TangentA.X ) );
		Assert.False( inputs.B.Responds );
	}

	[Fact]
	public void GeneratedContactWithZeroFrictionAxesSolvesAndReportsToOwner()
	{
		var a = new PhysicalBodyState { MotionMode = 4, InverseMass = 1f, Velocity = -Vector3.UnitY };
		var b = new PhysicalBodyState { MotionMode = 4, InverseMass = 1f };
		var world = new PhysicalSolverWorld( [a, b], new BodyDynamicsSettings[ 2 ], [] );
		var owner = new PhysicalContactOwner();
		var contact = Point( Vector3.UnitY );
		world.RegisterContactBody( a, owner, 9 );
		Assert.Throws<InvalidOperationException>( () => world.CompileGeneratedContact( contact, .1f ) );
		world.Prepare();
		ContactConstraint[] constraints = [world.CompileGeneratedContact( contact, .1f )];
		Assert.Equal( Vector3.Zero, constraints[ 0 ].TangentA );
		Assert.Equal( Vector3.Zero, constraints[ 0 ].TangentB );
		var output = new PhysicalWorldContact[ 1 ];
		Assert.Equal( 1, world.SolveIntegrateAndCollect( constraints, [], [], output, 4, .1f, Vector3.Zero ) );
		Assert.True( constraints[ 0 ].Impulse.X > 0f );
		Assert.True( float.IsFinite( constraints[ 0 ].Impulse.X ) );
		Assert.Equal( 0f, constraints[ 0 ].Impulse.Y );
		Assert.Equal( 0f, constraints[ 0 ].Impulse.Z );
		Assert.Equal( 9, owner.Contact( 0 ).RiderContact.Bone );
		Assert.Equal( 0x1234, owner.Contact( 0 ).RiderContact.SurfaceFlags );
	}

	private static ContactManifoldPoint Point( Vector3 normal ) =>
		new( 0, 1, Vector3.Zero, Vector3.Zero, normal, .75f, .5f, .25f ) { Materials = 0xabcd1234 };
}
