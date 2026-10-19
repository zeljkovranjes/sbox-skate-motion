using System;
using System.Numerics;
using SkateMotion.Core.Animation;
using SkateMotion.Core.Constraints;
using SkateMotion.Core.Physics;
using Xunit;

namespace SboxSkateMotion.Tests;

public class PhysicalRigSolverBindingTests
{
	[Fact]
	public void FullBindingRegistersRootsBeforeMappedChildrenUsingSharedBodyIdentity()
	{
		var f = Setup();
		f.Bind();
		var drives = f.Compile();
		Assert.Equal( 8, drives.Length );
		int[] children = [ 23, 0, 24, 25, 1, 1, 5, 5 ];
		int[] attached = [ 26, 27, 28, 29, 0, 23, 2, 23 ];
		for ( var i = 0; i < drives.Length; ++i )
		{
			Assert.Equal( 29 - children[ i ], drives[ i ].Pair.BodyA );
			Assert.Equal( 29 - attached[ i ], drives[ i ].Pair.BodyB );
		}
		Assert.Equal( Vector3.Zero, drives[ 0 ].AngularBias );
		Assert.True( drives[ 2 ].AngularBias.Length() > 0f );
		Assert.Equal( Vector3.Zero, drives[ 2 ].LinearLimit );
		Assert.True( f.Joints[ 1 ].FirstEnabled );
		Assert.True( f.Joints[ 1 ].SecondEnabled );
	}

	[Fact]
	public void RootRefreshChangesOnlyPrimaryLinearDriveAndRetainedJointOutputStaysConnected()
	{
		var f = Setup();
		var binding = f.Bind();
		var before = f.Compile();
		f.State.RootDrive = new( 0f, 0f, 0f, 2 );
		f.State.Joints[ 0 ].FirstFrame = f.State.Joints[ 0 ].FirstFrame with { BodyPosition = new( .1f, 0f, 0f ) };
		binding.RefreshRootDrive();
		var after = f.Compile();
		Assert.True( before[ 0 ].LinearLimit.X > 0f );
		Assert.Equal( Vector3.Zero, after[ 0 ].LinearLimit );
		Assert.Equal( before[ 0 ].AngularLimit, after[ 0 ].AngularLimit );
		Assert.Equal( before[ 1 ].LinearLimit, after[ 1 ].LinearLimit );
		Assert.Equal( new Vector3( .1f, 0f, 0f ), after[ 4 ].Pair.OffsetB );
	}

	[Fact]
	public void ReleaseRemovesOnlyOwnedDrivesAndPreservesUnregisteredCanonicalEntries()
	{
		var f = Setup( 9 );
		f.Mapping[ 0 ] = 2;
		var unrelated = new PhysicalJointState();
		f.World.RegisterPoseAttachment( unrelated, false, 0, 1 );
		var binding = f.Bind();
		Assert.Equal( 9, f.World.PoseAttachmentCount );
		binding.Release();
		binding.Release();
		Assert.Equal( 1, f.World.PoseAttachmentCount );
		Assert.True( f.World.HasPoseAttachment( unrelated, false ) );
		Assert.False( f.Joints[ 1 ].FirstEnabled );
		Assert.False( f.Joints[ 1 ].SecondEnabled );
		Assert.True( f.Joints[ 0 ].FirstEnabled );
		Assert.Throws<InvalidOperationException>( binding.RefreshRootDrive );
	}

	[Fact]
	public void InsufficientCapacityLeavesTheRigAndWorldUntouched()
	{
		var f = Setup( 7 );
		f.Joints[ 1 ].Dirty = true;
		Assert.Throws<ArgumentException>( () => f.Bind() );
		Assert.Equal( 0, f.World.PoseAttachmentCount );
		Assert.True( f.Joints[ 1 ].Dirty );
		Assert.False( f.Joints[ 1 ].FirstEnabled );
		Assert.Equal( default, f.State.RootDrive );
	}

	[Fact]
	public void OutputAndSolverMustShareTheSameCanonicalJointObjects()
	{
		var f = Setup();
		f.Joints[ 1 ] = new();
		Assert.Throws<ArgumentException>( () => f.Bind() );
		Assert.Equal( 0, f.World.PoseAttachmentCount );
	}

	[Fact]
	public void MissingSharedBodyIsRejectedBeforeRegistration()
	{
		var f = Setup();
		f.State.Bodies[ 2 ].Body = new();
		Assert.Throws<ArgumentException>( () => f.Bind() );
		Assert.Equal( 0, f.World.PoseAttachmentCount );
	}

	[Fact]
	public void ASecondOwnerCannotReplaceAnExistingRigsAttachments()
	{
		var f = Setup( 16 );
		f.Bind();
		Assert.Throws<ArgumentException>( () => f.Bind() );
		Assert.Equal( 8, f.World.PoseAttachmentCount );
		Assert.True( f.Joints[ 1 ].FirstEnabled );
	}

	[Fact]
	public void AliasedCanonicalJointsAreRejectedWithoutMutatingTheirSharedState()
	{
		var f = Setup();
		f.Joints[ 5 ] = f.Joints[ 1 ];
		f.State.Joints[ 4 ] = f.Joints[ 1 ];
		Assert.Throws<ArgumentException>( () => f.Bind() );
		Assert.Equal( 0, f.World.PoseAttachmentCount );
		Assert.False( f.Joints[ 1 ].FirstEnabled );
	}

	[Fact]
	public void TargetsCannotAliasCharacterBodies()
	{
		var f = Setup();
		f.State.Targets[ 0 ].Body = f.State.Bodies[ 23 ].Body;
		Assert.Throws<ArgumentException>( () => f.Bind() );
		Assert.Equal( 0, f.World.PoseAttachmentCount );
	}

	[Fact]
	public void BoundRootRespondsInSharedSolveWhileItsTargetRemainsFixed()
	{
		var f = Setup();
		f.Bind();
		var body = f.State.Bodies[ 23 ].Body;
		body.Pose = body.Pose with { Translation = new( .1f, 0f, 0f ) };
		var drives = f.Compile();
		f.World.SolveAndIntegrate( [], [], drives, 25, .01f, Vector3.Zero );
		Assert.True( body.Pose.Translation.X < .1f );
		Assert.True( body.Velocity.X < 0f );
		Assert.Equal( Vector3.Zero, f.State.Targets[ 0 ].Body.Pose.Translation );
		Assert.Equal( Vector3.Zero, f.State.Targets[ 0 ].Body.Velocity );
	}

	[Fact]
	public void IdenticalEndpointRotationsProduceExactlyZeroRelativeError()
	{
		var rotation = Quaternion.Normalize( new Quaternion( .13f, .27f, .31f, .71f ) );
		Assert.Equal( Vector3.Zero, QuaternionFrame.Between( rotation, rotation ).Error );
	}

	private static Fixture Setup( int capacity = 8 )
	{
		var identity = LimbRigidTransform.Identity;
		var poses = new LimbRigidTransform[ 24 ];
		Array.Fill( poses, identity );
		var mapping = new int[ 24 ];
		Array.Fill( mapping, -1 );
		mapping[ 1 ] = 0;
		mapping[ 5 ] = 2;
		var joints = new PhysicalJointState[ 24 ];
		for ( var i = 0; i < joints.Length; ++i )
			joints[ i ] = new();
		var retained = joints.AsSpan( 1, 22 ).ToArray();
		var bodies = new PhysicalBodyBinding[ 26 ];
		var targets = new PhysicalBodyBinding[ 4 ];
		var shared = new PhysicalBodyState[ 30 ];
		var integration = new int[ 26 ];
		var settings = new BodyDynamicsSettings[ 30 ];
		for ( var i = 0; i < 30; ++i )
		{
			var body = new PhysicalBodyState
			{
				Pose = identity, Orientation = Vector4.UnitW, MotionMode = i < 26 ? 4 : 1,
				InverseMass = 1f, InverseInertiaDiagonal = Vector3.One
			};
			shared[ 29 - i ] = body;
			settings[ 29 - i ] = new( Vector3.One, 0f, 0f, 100f, 100f, 1f, 1f, 4 );
			var binding = new PhysicalBodyBinding { Body = body, CachedPose = identity };
			if ( i < 26 )
			{
				bodies[ i ] = binding;
				integration[ i ] = 29 - i;
			}
			else
			{
				targets[ i - 26 ] = binding;
			}
		}
		var skeleton = new EffectorRigSkeleton( new EffectorBoneBinding[ 4 ], new int[ 24 ], poses, poses, poses );
		var rig = new EffectorRigState( new EffectorWeightState[ 4 ], new EffectorPoseState[ 4 ],
			new FootReachState[ 2 ], skeleton, default );
		var state = new PhysicalRigState( rig, new( 24 ), new(), new( 26 ), targets, bodies, retained,
			[ new( .5f, .25f, .75f, .125f ), new( 1.5f, 1.25f, 1.75f, 1.125f ) ] );
		return new( new( shared, settings, integration, capacity ), state, joints, poses, mapping );
	}

	private sealed record Fixture( PhysicalSolverWorld World, PhysicalRigState State,
		PhysicalJointState[] Joints, LimbRigidTransform[] Poses, int[] Mapping )
	{
		internal PhysicalRigSolverBinding Bind() => new( World, State, Joints, Poses, Mapping );

		internal DriveConstraint[] Compile()
		{
			World.Prepare();
			var output = new DriveConstraint[ World.PoseAttachmentCount ];
			return output.AsSpan( 0, World.CompilePoseAttachments( output, .01f ) ).ToArray();
		}
	}
}
