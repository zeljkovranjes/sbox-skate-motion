using System.Numerics;
using SkateMotion.Core.Animation;
using SkateMotion.Core.Constraints;
using SkateMotion.Core.Physics;
using Xunit;

namespace SboxSkateMotion.Tests;

public class PhysicalStructuralMembershipTests
{
	[Theory]
	[InlineData( 0, 0, false )]
	[InlineData( 1, 2, false )]
	[InlineData( 4, 0, true )]
	[InlineData( 0, 4, true )]
	[InlineData( 12, 1, true )]
	public void SelectionUsesEitherEndpointResponseBit( int first, int second, bool selected )
	{
		var bodies = Bodies( 2 );
		bodies[ 0 ].MotionMode = first;
		bodies[ 1 ].MotionMode = second;
		var world = World( bodies, 1 );
		Assert.True( world.TryRegisterStructuralJoint( new( Joint( 1 ) ), 0, 1 ) );
		world.Prepare();
		Assert.Equal( selected ? 1 : 0, world.CompileStructuralJoints( new JointConstraint[ 1 ], .1f ) );
	}

	[Fact]
	public void FullCapacityPreservesExistingOrderAndLeavesNewJointUnowned()
	{
		var world = World( Bodies( 2 ), 1 );
		var first = new PhysicalStructuralJointRegistration( Joint( 1 ) );
		var second = new PhysicalStructuralJointRegistration( Joint( 2 ) );
		Assert.True( world.TryRegisterStructuralJoint( first, 0, 1 ) );
		Assert.False( world.TryRegisterStructuralJoint( second, 1, 0 ) );
		Assert.Null( second.Owner );
		Assert.Equal( 1, world.StructuralJointCount );
		world.Prepare();
		var output = new JointConstraint[ 1 ];
		Assert.Equal( 1, world.CompileStructuralJoints( output, .1f ) );
		Assert.Equal( 1f, output[ 0 ].Pair.OffsetA.X );
	}

	[Fact]
	public void ReleaseAndRebindAppendsAfterSurvivingJoints()
	{
		var world = World( Bodies( 2 ), 2 );
		var first = new PhysicalStructuralJointRegistration( Joint( 1 ) );
		var second = new PhysicalStructuralJointRegistration( Joint( 2 ) );
		world.TryRegisterStructuralJoint( first, 0, 1 );
		world.TryRegisterStructuralJoint( second, 1, 0 );
		world.ReleaseStructuralJoint( first );
		world.ReleaseStructuralJoint( first );
		Assert.True( world.TryRegisterStructuralJoint( first, 1, 0 ) );
		world.Prepare();
		var output = new JointConstraint[ 2 ];
		Assert.Equal( 2, world.CompileStructuralJoints( output, .1f ) );
		Assert.Equal( 2f, output[ 0 ].Pair.OffsetA.X );
		Assert.Equal( 1f, output[ 1 ].Pair.OffsetA.X );
	}

	[Fact]
	public void DifferentWorldCannotTakeOrReleaseAnOwnedJoint()
	{
		var first = World( Bodies( 2 ), 1 );
		var second = World( Bodies( 2 ), 1 );
		var joint = new PhysicalStructuralJointRegistration( Joint( 1 ) );
		first.TryRegisterStructuralJoint( joint, 0, 1 );
		Assert.Throws<ArgumentException>( () => second.TryRegisterStructuralJoint( joint, 0, 1 ) );
		second.ReleaseStructuralJoint( joint );
		Assert.Same( first, joint.Owner );
		Assert.Equal( 1, first.StructuralJointCount );
		Assert.Equal( 0, second.StructuralJointCount );
	}

	[Fact]
	public void ShortDestinationFailsBeforeWritingAnyJoint()
	{
		var world = World( Bodies( 2 ), 2 );
		world.TryRegisterStructuralJoint( new( Joint( 1 ) ), 0, 1 );
		world.TryRegisterStructuralJoint( new( Joint( 2 ) ), 1, 0 );
		world.Prepare();
		var output = new JointConstraint[ 1 ];
		Assert.Throws<ArgumentException>( () => world.CompileStructuralJoints( output, .1f ) );
		Assert.Equal( default, output[ 0 ] );
	}

	[Fact]
	public void RigResolvesPermutedPhysicalBodiesAndReleasesOnlyItsOwnJoints()
	{
		var bodies = Bodies( 25 );
		var world = World( bodies, 3 );
		world.TryRegisterStructuralJoint( new( Joint( 9 ) ), 24, 0 );
		var bindings = Enumerable.Range( 0, 24 ).Select( i => new PhysicalBodyBinding { Body = bodies[ 23 - i ] } ).ToArray();
		var owner = new PhysicalStructuralRigBinding( world, bindings, [ Joint( 1 ), Joint( 2 ) ] );
		world.Prepare();
		var output = new JointConstraint[ 3 ];
		Assert.Equal( 3, world.CompileStructuralJoints( output, .1f ) );
		Assert.Equal( 22, output[ 1 ].Pair.BodyA );
		Assert.Equal( 21, output[ 1 ].Pair.BodyB );
		owner.Release();
		owner.Release();
		Assert.Equal( 1, world.CompileStructuralJoints( output, .1f ) );
		Assert.Equal( 24, output[ 0 ].Pair.BodyA );
		Assert.Throws<InvalidOperationException>( () => owner.UpdateJoint( 0, Joint( 1 ) ) );
	}

	[Theory]
	[InlineData( 0 )]
	[InlineData( 1 )]
	[InlineData( 2 )]
	[InlineData( 3 )]
	public void InvalidRigBindingLeavesWorldUnchanged( int problem )
	{
		var bodies = Bodies( 24 );
		var world = World( bodies, problem == 0 ? 1 : 2 );
		var bindings = bodies.Select( body => new PhysicalBodyBinding { Body = body } ).ToArray();
		var joints = new[] { Joint( 1 ), Joint( 2 ) };
		if ( problem == 1 ) bindings[ 4 ].Body = Bodies( 1 )[ 0 ];
		if ( problem == 2 ) bindings[ 4 ].Body = bodies[ 3 ];
		if ( problem == 3 ) joints[ 1 ] = joints[ 1 ] with { ChildBone = 24 };
		Assert.Throws<ArgumentException>( () => new PhysicalStructuralRigBinding( world, bindings, joints ) );
		Assert.Equal( 0, world.StructuralJointCount );
	}

	[Fact]
	public void RetainedUpdatesChangeCompiledGeometryWithoutChangingMembership()
	{
		var bodies = Bodies( 24 );
		var world = World( bodies, 1 );
		var bindings = bodies.Select( body => new PhysicalBodyBinding { Body = body } ).ToArray();
		var owner = new PhysicalStructuralRigBinding( world, bindings, [ Joint( 1 ) ] );
		var updated = Joint( 1 ) with { Frame = Joint( 1 ).Frame with { ChildPosition = new( 3f, 2f, 1f ) } };
		owner.UpdateJoint( 0, updated );
		Assert.Throws<ArgumentException>( () => owner.UpdateJoint( 0, updated with { ChildBone = 3 } ) );
		world.Prepare();
		var output = new JointConstraint[ 1 ];
		Assert.Equal( 1, world.CompileStructuralJoints( output, .1f ) );
		Assert.Equal( new Vector3( 3f, 2f, 1f ), output[ 0 ].Pair.OffsetA );
	}

	private static PhysicalStructuralJoint Joint( int index ) => new( index + 1, index,
		new( Vector4.UnitW, new( index, 0f, 0f ), Vector4.UnitW, Vector3.Zero ), Vector4.UnitW,
		new( Vector4.One, 1f, 2f, .2f, .3f, .98f, .95f, 4, 2 ) );

	private static PhysicalBodyState[] Bodies( int count ) => Enumerable.Range( 0, count ).Select( _ => new PhysicalBodyState
	{
		Pose = LimbRigidTransform.Identity, Orientation = Vector4.UnitW, InverseMass = 1f,
		InverseInertiaDiagonal = Vector3.One, MotionMode = 4
	} ).ToArray();

	private static PhysicalSolverWorld World( PhysicalBodyState[] bodies, int capacity ) =>
		new( bodies, new BodyDynamicsSettings[ bodies.Length ], [], 0, capacity );
}
