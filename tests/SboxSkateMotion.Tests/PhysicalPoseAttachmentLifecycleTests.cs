using System;
using System.Numerics;
using SkateMotion.Core.Animation;
using SkateMotion.Core.Constraints;
using SkateMotion.Core.Physics;
using Xunit;

namespace SboxSkateMotion.Tests;

public class PhysicalPoseAttachmentLifecycleTests
{
	[Fact]
	public void ReplacingAnAttachmentMovesItBehindOtherRegistrations()
	{
		var (world, _) = World();
		var first = Joint();
		var other = Joint();
		world.RegisterPoseAttachment( first, false, 0, 1 );
		world.RegisterPoseAttachment( first, true, 0, 2 );
		world.RegisterPoseAttachment( other, false, 1, 2 );
		world.RegisterPoseAttachment( first, false, 0, 2 );
		var drives = Compile( world );
		Assert.Equal( 3, world.PoseAttachmentCount );
		Assert.Equal( 0, drives[ 0 ].Pair.BodyA );
		Assert.Equal( 2, drives[ 0 ].Pair.BodyB );
		Assert.Equal( 1, drives[ 1 ].Pair.BodyA );
		Assert.Equal( 0, drives[ 2 ].Pair.BodyA );
		Assert.False( first.FirstEnabled );
		Assert.True( first.SecondEnabled );
	}

	[Fact]
	public void TargetUpdateFlagsDoNotRemoveRegisteredConstraints()
	{
		var (world, _) = World();
		var joint = Joint();
		joint.FirstEnabled = false;
		world.RegisterPoseAttachment( joint, false, 0, 1 );
		Assert.False( joint.FirstEnabled );
		Assert.Single( Compile( world ) );
		joint.FirstEnabled = true;
		Assert.Single( Compile( world ) );
	}

	[Theory]
	[InlineData( 1, 2, 0 )]
	[InlineData( 4, 2, 1 )]
	[InlineData( 1, 12, 1 )]
	public void SelectionUsesEitherBodyResponseFlagWithoutSubstitutingMass( int first, int second, int expected )
	{
		var (world, bodies) = World();
		world.RegisterPoseAttachment( Joint(), false, 0, 1 );
		bodies[ 0 ].MotionMode = first;
		bodies[ 1 ].MotionMode = second;
		bodies[ 0 ].InverseMass = bodies[ 1 ].InverseMass = 0f;
		if ( expected == 0 )
			Assert.Empty( Compile( world ) );
		else
			Assert.Throws<ArgumentException>( () => Compile( world ) );
		bodies[ 0 ].InverseMass = bodies[ 1 ].InverseMass = 1f;
		Assert.Equal( expected, Compile( world ).Length );
		Assert.Equal( 1, world.PoseAttachmentCount );
		bodies[ 0 ].MotionMode = 4;
		Assert.Single( Compile( world ) );
	}

	[Fact]
	public void ReleasingOneSlotKeepsTheOtherAndClearsEvenAnAbsentTargetFlag()
	{
		var (world, _) = World();
		var joint = Joint();
		world.RegisterPoseAttachment( joint, false, 0, 1 );
		world.RegisterPoseAttachment( joint, true, 0, 2 );
		world.ReleasePoseAttachment( joint, false );
		joint.FirstEnabled = true;
		world.ReleasePoseAttachment( joint, false );
		Assert.False( joint.FirstEnabled );
		Assert.True( joint.SecondEnabled );
		Assert.Equal( 2, Assert.Single( Compile( world ) ).Pair.BodyB );
	}

	[Fact]
	public void FullCapacityAllowsReplacementAndRejectsNewMembershipWithoutChangingExistingBindings()
	{
		var (world, _) = World( 1 );
		var joint = Joint();
		world.RegisterPoseAttachment( joint, false, 0, 1 );
		Assert.Throws<InvalidOperationException>( () => world.RegisterPoseAttachment( Joint(), false, 1, 2 ) );
		Assert.True( joint.FirstEnabled );
		Assert.Throws<ArgumentOutOfRangeException>( () => world.RegisterPoseAttachment( joint, false, 0, 3 ) );
		Assert.Equal( 1, Assert.Single( Compile( world ) ).Pair.BodyB );
		world.RegisterPoseAttachment( joint, false, 0, 2 );
		Assert.Equal( 2, Assert.Single( Compile( world ) ).Pair.BodyB );
	}

	[Fact]
	public void RetainedMembershipReadsNewFramesAndCoefficientsEachStep()
	{
		var (world, _) = World();
		var joint = Joint();
		world.RegisterPoseAttachment( joint, false, 0, 1 );
		var before = Assert.Single( Compile( world ) );
		joint.FirstFrame = joint.FirstFrame with { BodyPosition = new( .2f, 0f, 0f ) };
		joint.FirstConstraint = new( 0f, 0f, 0f, 0 );
		var after = Assert.Single( Compile( world ) );
		Assert.Equal( Vector3.Zero, before.Pair.OffsetB );
		Assert.Equal( new Vector3( .2f, 0f, 0f ), after.Pair.OffsetB );
		Assert.True( before.LinearLimit.X > 0f );
		Assert.Equal( Vector3.Zero, after.LinearLimit );
	}

	[Fact]
	public void RegisteredAttachmentSolvesAndWritesBackBothBodies()
	{
		var (world, bodies) = World();
		world.RegisterPoseAttachment( Joint(), false, 0, 1 );
		var drives = Compile( world );
		world.SolveAndIntegrate( [], [], drives, 1, .1f, Vector3.Zero );
		Assert.InRange( bodies[ 0 ].Pose.Translation.Length(), 0f, 1e-6f );
		Assert.InRange( bodies[ 1 ].Pose.Translation.Length(), 0f, 1e-6f );
		Assert.InRange( MathF.Abs( bodies[ 0 ].Velocity.X - 1f ), 0f, 1e-6f );
		Assert.InRange( (bodies[ 0 ].Velocity + bodies[ 1 ].Velocity).Length(), 0f, 1e-6f );
		Assert.Throws<InvalidOperationException>( () => world.CompilePoseAttachments( new DriveConstraint[ 3 ], .1f ) );
	}

	[Fact]
	public void DestinationFailureDoesNotWritePartialOutputOrRemoveMembership()
	{
		var (world, _) = World();
		var joint = Joint();
		world.RegisterPoseAttachment( joint, false, 0, 1 );
		world.RegisterPoseAttachment( joint, true, 0, 2 );
		world.Prepare();
		var destination = new DriveConstraint[ 1 ];
		Assert.Throws<ArgumentException>( () => world.CompilePoseAttachments( destination, .1f ) );
		Assert.Equal( default, destination[ 0 ] );
		Assert.Equal( 2, world.PoseAttachmentCount );
		Assert.Equal( 2, Compile( world ).Length );
	}

	private static DriveConstraint[] Compile( PhysicalSolverWorld world )
	{
		world.Prepare();
		var output = new DriveConstraint[ world.PoseAttachmentCount ];
		return output.AsSpan( 0, world.CompilePoseAttachments( output, .1f ) ).ToArray();
	}

	private static PhysicalJointState Joint() => new()
	{
		FirstEnabled = true, SecondEnabled = true,
		FirstFrame = new( Vector4.UnitW, Vector3.Zero, Vector4.UnitW, Vector3.Zero ),
		SecondFrame = new( Vector4.UnitW, Vector3.Zero, Vector4.UnitW, Vector3.Zero ),
		FirstConstraint = new( 0f, 0f, 1000f, 0 ), SecondConstraint = new( 0f, 0f, 1000f, 0 ),
		ThirdConstraint = new( 0f, 0f, 1000f, 0 ), FourthConstraint = new( 0f, 0f, 1000f, 0 )
	};

	private static (PhysicalSolverWorld World, PhysicalBodyState[] Bodies) World( int capacity = 3 )
	{
		PhysicalBodyState Body( float x ) => new()
		{
			Pose = new( LimbRigidTransform.Identity.Rotation, new( x, 0f, 0f ) ),
			Orientation = Vector4.UnitW, MotionMode = 4,
			InverseMass = 1f, InverseInertiaDiagonal = Vector3.One
		};
		PhysicalBodyState[] bodies = [ Body( -.1f ), Body( .1f ), Body( 1f ) ];
		var settings = new BodyDynamicsSettings( Vector3.One, 0f, 0f, 100f, 100f, 1f, 1f, 4 );
		return (new( bodies, [ settings, settings, settings ], [ 0, 1, 2 ], capacity ), bodies);
	}
}
