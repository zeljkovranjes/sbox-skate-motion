using System;
using System.Linq;
using System.Numerics;
using SkateMotion.Core.Animation;
using SkateMotion.Core.Constraints;
using SkateMotion.Core.Physics;
using Xunit;

namespace SboxSkateMotion.Tests;

public class PhysicalContactRegistrationTests
{
	[Theory]
	[InlineData( 0 )]
	[InlineData( 1 )]
	[InlineData( 4 )]
	[InlineData( 5 )]
	[InlineData( 0x12345674 )]
	[InlineData( -1 )]
	public void WorldRegistrationEnablesReportingAndRemovalRestoresOnlyBaseMode( int mode )
	{
		var body = new PhysicalBodyState { MotionMode = mode };
		var world = World( [body] );
		world.RegisterContactBody( body, new(), 2 );
		Assert.Equal( mode | 8, body.MotionMode );
		world.UnregisterContactBody( body );
		Assert.Equal( mode & 7, body.MotionMode );
		body.MotionMode |= 0x80;
		world.UnregisterContactBody( body );
		Assert.Equal( mode & 7, body.MotionMode );
	}

	[Fact]
	public void RegistrationUpdatesPreparedCompilationAndRoutesTheNewOwner()
	{
		var a = new PhysicalBodyState { MotionMode = 4, InverseMass = 1f };
		var b = new PhysicalBodyState { MotionMode = 4, InverseMass = 1f };
		var first = new PhysicalContactOwner();
		var replacement = new PhysicalContactOwner();
		var world = World( [a, b] );
		world.Prepare();
		var collision = new CollisionContact( 0, 1, Vector3.Zero, Vector3.Zero,
			Vector3.UnitY, Vector3.UnitX, Vector3.UnitZ, .5f, .25f, 0f );
		Assert.False( world.CompileContact( collision, .1f ).ReportContact );
		world.RegisterContactBody( a, first, 2 );
		Assert.True( world.CompileContact( collision, .1f ).ReportContact );
		world.ContactCollector.Collect( [Contact( a, b )] );
		Assert.Equal( 1, first.Count );
		world.RegisterContactBody( a, replacement, 17 );
		world.ContactCollector.Collect( [Contact( a, b )] );
		Assert.Equal( 0, first.Count );
		Assert.Equal( 17, replacement.Contact( 0 ).RiderContact.Bone );
		world.UnregisterContactBody( a );
		Assert.False( world.CompileContact( collision, .1f ).ReportContact );
		world.ContactCollector.Collect( [Contact( a, b )] );
		Assert.Equal( 0, replacement.Count );
	}

	[Fact]
	public void RegisteredBodyReceivesSolvedContactsAndRemovalStopsReporting()
	{
		var a = new PhysicalBodyState { MotionMode = 4, InverseMass = 1f, Velocity = -Vector3.UnitY };
		var b = new PhysicalBodyState { MotionMode = 4, InverseMass = 1f };
		var owner = new PhysicalContactOwner();
		var world = World( [a, b] );
		var collision = new CollisionContact( 0, 1, Vector3.Zero, Vector3.Zero,
			Vector3.UnitY, Vector3.UnitX, Vector3.UnitZ, .5f, .25f, 0f ) { Materials = 0xabcd1234 };
		var output = new PhysicalWorldContact[ 1 ];
		world.RegisterContactBody( a, owner, 7 );
		world.Prepare();
		ContactConstraint[] contacts = [world.CompileContact( collision, .1f )];
		Assert.Equal( 1, world.SolveIntegrateAndCollect( contacts, [], [], output, 4, .1f, Vector3.Zero ) );
		Assert.True( contacts[ 0 ].Impulse.X > 0f );
		Assert.Equal( 7, owner.Contact( 0 ).RiderContact.Bone );
		Assert.Equal( 0x1234, owner.Contact( 0 ).RiderContact.SurfaceFlags );
		world.UnregisterContactBody( a );
		world.Prepare();
		contacts[ 0 ] = world.CompileContact( collision, .1f );
		Assert.Equal( 0, world.SolveIntegrateAndCollect( contacts, [], [], output, 4, .1f, Vector3.Zero ) );
		Assert.True( contacts[ 0 ].Impulse.X > 0f );
		Assert.Equal( 0, owner.Count );
	}

	[Fact]
	public void InvalidRegistrationDoesNotChangeBodyFlags()
	{
		var body = new PhysicalBodyState { MotionMode = 4 };
		var world = World( [body] );
		Assert.Throws<ArgumentNullException>( () => world.RegisterContactBody( body, null, 0 ) );
		Assert.Throws<ArgumentOutOfRangeException>( () => world.RegisterContactBody( body, new(), -1 ) );
		Assert.Equal( 4, body.MotionMode );
		var unrelated = new PhysicalBodyState { MotionMode = 0x84 };
		Assert.Throws<ArgumentException>( () => world.RegisterContactBody( unrelated, new(), 0 ) );
		Assert.Throws<ArgumentException>( () => world.UnregisterContactBody( unrelated ) );
		Assert.Equal( 0x84, unrelated.MotionMode );
	}

	[Fact]
	public void RigRegistersOnlyCanonicalBodiesAndPreservesBoneIdentityInPermutedWorld()
	{
		var bodies = Enumerable.Range( 0, 26 ).Select( _ => new PhysicalBodyState { MotionMode = 4 } ).ToArray();
		var bindings = bodies.Select( b => new PhysicalBodyBinding { Body = b } ).ToArray();
		var world = World( bodies.Reverse().ToArray() );
		var owner = new PhysicalContactOwner();
		var registration = new PhysicalRigContactBinding( world, bindings, owner );
		for ( var i = 0; i < bodies.Length; ++i )
			Assert.Equal( i < 24 ? 12 : 4, bodies[ i ].MotionMode );
		world.ContactCollector.Collect( [Contact( bodies[ 17 ], bodies[ 24 ] )] );
		Assert.Equal( 1, owner.Count );
		Assert.Equal( 17, owner.Contact( 0 ).RiderContact.Bone );
		Assert.Null( owner.Contact( 0 ).OtherOwner );
		registration.Release();
		registration.Release();
		Assert.All( bodies, b => Assert.Equal( 4, b.MotionMode ) );
		world.ContactCollector.Collect( [Contact( bodies[ 17 ], bodies[ 24 ] )] );
		Assert.Equal( 0, owner.Count );
	}

	[Theory]
	[InlineData( false )]
	[InlineData( true )]
	public void InvalidRigMembershipFailsBeforeChangingAnyBody( bool duplicate )
	{
		var bodies = Enumerable.Range( 0, 24 ).Select( _ => new PhysicalBodyState { MotionMode = 4 } ).ToArray();
		var bindings = bodies.Select( b => new PhysicalBodyBinding { Body = b } ).ToArray();
		bindings[ 23 ].Body = duplicate ? bodies[ 0 ] : new PhysicalBodyState();
		Assert.Throws<ArgumentException>( () => new PhysicalRigContactBinding( World( bodies ), bindings, new() ) );
		Assert.All( bodies, b => Assert.Equal( 4, b.MotionMode ) );
	}

	private static PhysicalSolverWorld World( PhysicalBodyState[] bodies ) =>
		new( bodies, new BodyDynamicsSettings[ bodies.Length ], [] );

	private static PhysicalWorldContact Contact( PhysicalBodyState first, PhysicalBodyState second ) =>
		new( first, second, Vector4.UnitY, Vector4.Zero, Vector3.Zero, 0 );
}
