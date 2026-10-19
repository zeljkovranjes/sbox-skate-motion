using System;
using System.Numerics;
using SkateMotion.Core.Animation;
using SkateMotion.Core.Constraints;
using SkateMotion.Core.Physics;
using Xunit;

namespace SboxSkateMotion.Tests;

public class PhysicalPoseConstraintsTests
{
	[Theory]
	[InlineData( false )]
	[InlineData( true )]
	public void AttachmentUsesResolvedSolverIndicesAndItsOwnFrames( bool second )
	{
		var world = World();
		var joint = Joint();
		joint.FirstFrame = new( Vector4.UnitW, new( 1f, 2f, 3f ), Vector4.UnitW, new( 4f, 5f, 6f ) );
		joint.SecondFrame = new( Vector4.UnitW, new( 7f, 8f, 9f ), Vector4.UnitW, new( 10f, 11f, 12f ) );
		world.Prepare();
		var drive = PhysicalPoseConstraints.Compile( world, joint, second, 1, 0, .1f );
		Assert.Equal( 1, drive.Pair.BodyA );
		Assert.Equal( 0, drive.Pair.BodyB );
		Assert.Equal( second ? joint.SecondFrame.ChildPosition : joint.FirstFrame.ChildPosition, drive.Pair.OffsetA );
		Assert.Equal( second ? joint.SecondFrame.BodyPosition : joint.FirstFrame.BodyPosition, drive.Pair.OffsetB );
	}

	[Fact]
	public void PoseOutputMovesBothRegisteredBodiesThroughTheSharedSolve()
	{
		var world = World();
		world.Prepare();
		var drive = PhysicalPoseConstraints.Compile( world, Joint(), false, 0, 1, .1f );
		world.SolveAndIntegrate( [], [], [ drive ], 1, .1f, Vector3.Zero );
		Assert.InRange( world.Body( 0 ).Position.Length(), 0f, 1e-6f );
		Assert.InRange( world.Body( 1 ).Position.Length(), 0f, 1e-6f );
		Assert.InRange( MathF.Abs( world.Body( 0 ).Velocity.X - 1f ), 0f, 1e-6f );
		Assert.InRange( (world.Body( 0 ).Velocity + world.Body( 1 ).Velocity).Length(), 0f, 1e-6f );
	}

	[Fact]
	public void ReusedJointReadsNewPoseOutputWithoutRebindingBodies()
	{
		var world = World();
		var joint = Joint();
		world.Prepare();
		var first = PhysicalPoseConstraints.Compile( world, joint, false, 0, 1, .1f );
		joint.FirstFrame = joint.FirstFrame with { BodyPosition = new( .2f, 0f, 0f ) };
		joint.FirstConstraint = new( 0f, 0f, 0f, 0 );
		var updated = PhysicalPoseConstraints.Compile( world, joint, false, 0, 1, .1f );
		Assert.Equal( Vector3.Zero, first.Pair.OffsetB );
		Assert.Equal( new Vector3( .2f, 0f, 0f ), updated.Pair.OffsetB );
		Assert.True( first.LinearLimit.X > 0f );
		Assert.Equal( Vector3.Zero, updated.LinearLimit );
	}

	[Fact]
	public void SignedPoseLimitsProjectUpperThenLowerBeforeApplyingBothReactions()
	{
		var world = World();
		var joint = Joint();
		joint.FirstConstraint = new( 0f, 0f, 0f, 0 );
		joint.SecondConstraint = new( -2f, 200f, -4f, 1 );
		world.Prepare();
		var drive = PhysicalPoseConstraints.Compile( world, joint, false, 0, 1, .1f );
		world.SolveAndIntegrate( [], [], [ drive ], 1, .1f, Vector3.Zero );
		Assert.InRange( Vector3.Distance( new( .2f ), world.Body( 0 ).AngularVelocity ), 0f, 1e-6f );
		Assert.InRange( Vector3.Distance( new( -.2f ), world.Body( 1 ).AngularVelocity ), 0f, 1e-6f );
	}

	[Fact]
	public void CompilationRequiresFreshPhysicalInputs()
	{
		var world = World();
		Assert.Throws<InvalidOperationException>( () => PhysicalPoseConstraints.Compile( world, Joint(), false, 0, 1, .1f ) );
		world.Prepare();
		world.SolveAndIntegrate( [], [], [], 0, .1f, Vector3.Zero );
		Assert.Throws<InvalidOperationException>( () => PhysicalPoseConstraints.Compile( world, Joint(), false, 0, 1, .1f ) );
	}

	[Fact]
	public void NonfinitePoseCoefficientsStillFailBeforeSolving()
	{
		var world = World();
		var joint = Joint();
		joint.FirstConstraint = new( float.NaN, 0f, 1f, 1 );
		world.Prepare();
		Assert.Throws<ArgumentOutOfRangeException>( () => PhysicalPoseConstraints.Compile( world, joint, false, 0, 1, .1f ) );
	}

	private static PhysicalJointState Joint() => new()
	{
		ChildBone = 17, FirstBodyBone = 9, SecondBodyBone = 22,
		FirstFrame = new( Vector4.UnitW, Vector3.Zero, Vector4.UnitW, Vector3.Zero ),
		SecondFrame = new( Vector4.UnitW, Vector3.Zero, Vector4.UnitW, Vector3.Zero ),
		FirstConstraint = new( 0f, 0f, 1000f, 0 ), SecondConstraint = new( 0f, 0f, 1000f, 0 ),
		ThirdConstraint = new( 0f, 0f, 1000f, 0 ), FourthConstraint = new( 0f, 0f, 1000f, 0 )
	};

	private static PhysicalSolverWorld World()
	{
		PhysicalBodyState Body( float x ) => new()
		{
			Pose = new( LimbRigidTransform.Identity.Rotation, new( x, 0f, 0f ) ),
			Orientation = Vector4.UnitW, MotionMode = 4,
			InverseMass = 1f, InverseInertiaDiagonal = Vector3.One
		};
		var settings = new BodyDynamicsSettings( Vector3.One, 0f, 0f, 100f, 100f, 1f, 1f, 4 );
		return new( [ Body( -.1f ), Body( .1f ) ], [ settings, settings ], [ 0, 1 ] );
	}
}
