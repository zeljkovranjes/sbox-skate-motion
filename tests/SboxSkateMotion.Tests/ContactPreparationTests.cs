using System;
using System.Numerics;
using SkateMotion.Core.Animation;
using SkateMotion.Core.Constraints;
using SkateMotion.Core.Physics;
using Xunit;

namespace SboxSkateMotion.Tests;

public class ContactPreparationTests
{
	[Theory]
	[InlineData( 0f, 1f, 0f, 1f, 0f, 0f )]
	[InlineData( 1f, 0f, 0f, 0f, 0f, -1f )]
	[InlineData( -1f, 0f, 0f, 0f, 0f, -1f )]
	[InlineData( 0f, 0f, 1f, 1f, 0f, 0f )]
	public void StationaryContactsUseFiniteNormalDependentFrame( float nx, float ny, float nz, float tx, float ty, float tz )
	{
		var normal = new Vector3( nx, ny, nz );
		var geometry = ContactPreparation.Prepare( default, default, Point( normal ) );
		Assert.Equal( new Vector3( tx, ty, tz ), geometry.TangentA );
		Assert.Equal( Vector3.Cross( normal, geometry.TangentA ), geometry.TangentB );
		Assert.Equal( 1f, geometry.TangentA.Length() );
		Assert.Equal( 1f, geometry.TangentB.Length() );
	}

	[Theory]
	[InlineData( 0x1ffffffd, false )]
	[InlineData( 0x1ffffffe, false )]
	[InlineData( 0x1fffffff, true )]
	[InlineData( 0x20000000, true )]
	[InlineData( 0x20000001, true )]
	public void SquaredSlipCutoffPreservesEqualityAndAdjacentValues( int bits, bool sliding )
	{
		var moving = new DynamicBody { Velocity = new Vector3( BitConverter.Int32BitsToSingle( bits ), 0f, 0f ) };
		var geometry = ContactPreparation.Prepare( default, moving, Point( Vector3.UnitY ) );
		Assert.Equal( sliding ? 0f : 1f, geometry.TangentA.X );
		Assert.InRange( geometry.TangentA.Z, sliding ? .9999998f : 0f, sliding ? 1.0000002f : 0f );
		Assert.True( float.IsFinite( geometry.TangentB.X ) );
	}

	[Fact]
	public void AngularPointVelocityAndKinematicMotionDetermineFrictionDirections()
	{
		var a = new DynamicBody { Position = new( 2f, 0f, 0f ), AngularVelocity = Vector3.UnitZ };
		var b = new DynamicBody { Velocity = new( 0f, 2f, 0f ), InverseMass = 0f };
		var contact = Point( Vector3.UnitZ ) with { PointA = new( 3f, 0f, 0f ) };
		var inputs = ContactInputs.FromManifold( a, b, contact );
		Assert.Equal( Vector3.UnitY, inputs.Geometry.MotionTerm );
		Assert.Equal( Vector3.UnitX, inputs.Geometry.TangentA );
		Assert.Equal( Vector3.UnitY, inputs.Geometry.TangentB );
		Assert.False( inputs.B.Responds );
	}

	[Fact]
	public void PreparedManifoldCompilesSolvesAndReachesRegisteredOwner()
	{
		var a = new PhysicalBodyState { MotionMode = 4, InverseMass = 1f, Velocity = -Vector3.UnitY };
		var b = new PhysicalBodyState { MotionMode = 4, InverseMass = 1f };
		var world = new PhysicalSolverWorld( [a, b], new BodyDynamicsSettings[ 2 ], [] );
		var owner = new PhysicalContactOwner();
		var contact = Point( Vector3.UnitY );
		world.RegisterContactBody( a, owner, 9 );
		Assert.Throws<InvalidOperationException>( () => world.CompileManifoldContact( contact, .1f ) );
		world.Prepare();
		ContactConstraint[] constraints = [world.CompileManifoldContact( contact, .1f )];
		Assert.Equal( Vector3.UnitX, constraints[ 0 ].TangentA );
		Assert.Equal( -Vector3.UnitZ, constraints[ 0 ].TangentB );
		var output = new PhysicalWorldContact[ 1 ];
		Assert.Equal( 1, world.SolveIntegrateAndCollect( constraints, [], [], output, 4, .1f, Vector3.Zero ) );
		Assert.True( constraints[ 0 ].Impulse.X > 0f );
		Assert.Equal( 9, owner.Contact( 0 ).RiderContact.Bone );
		Assert.Equal( 0x1234, owner.Contact( 0 ).RiderContact.SurfaceFlags );
	}

	private static ContactManifoldPoint Point( Vector3 normal ) =>
		new( 0, 1, Vector3.Zero, Vector3.Zero, normal, .75f, .5f, .25f ) { Materials = 0xabcd1234 };
}
