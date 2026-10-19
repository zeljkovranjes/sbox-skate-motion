using System;
using System.Numerics;
using SkateMotion.Core.Animation;
using SkateMotion.Core.Constraints;
using SkateMotion.Core.Physics;
using Xunit;

namespace SboxSkateMotion.Tests;

public class PhysicalJointInitializationTests
{
	[Fact]
	public void InitializationKeepsTheFirstAttachmentAngularOnlyAndDrivesBothReferenceChannels()
	{
		var (joints, poses, mapping, presets) = Inputs();
		mapping[ 5 ] = 2;
		PhysicalJointInitialization.Initialize( joints, poses, mapping, presets );
		var joint = joints[ 5 ];
		Assert.Equal( 5, joint.ChildBone );
		Assert.Equal( 2, joint.FirstBodyBone );
		Assert.Equal( 23, joint.SecondBodyBone );
		Assert.True( joint.FirstEnabled );
		Assert.True( joint.SecondEnabled );
		Assert.Equal( new PhysicalJointDrive( 0f, 0f, 0f, 2 ), joint.FirstConstraint );
		Assert.Equal( new PhysicalJointDrive( .125f * 59.999996f, 0f, .75f * 3599.9995f, 2 ), joint.SecondConstraint );
		Assert.Equal( new PhysicalJointDrive( 1.25f * 59.999996f, 0f, 1.5f * 3599.9995f, 2 ), joint.ThirdConstraint );
		Assert.Equal( new PhysicalJointDrive( 1.125f * 59.999996f, 0f, 1.75f * 3599.9995f, 2 ), joint.FourthConstraint );
		Assert.Equal( 0, joint.Mode );
		Assert.False( joint.Dirty );
	}

	[Fact]
	public void BothLocalAnchorsResolveToTheChildBindPosition()
	{
		var (joints, poses, mapping, presets) = Inputs();
		poses[ 2 ] = new( new LimbFrame( Vector3.UnitY, -Vector3.UnitX, Vector3.UnitZ ), new( 2f, 3f, 4f ) );
		poses[ 5 ] = new( new LimbFrame( -Vector3.UnitX, Vector3.UnitY, -Vector3.UnitZ ), new( 5f, 6f, 7f ) );
		mapping[ 5 ] = 2;
		PhysicalJointInitialization.Initialize( joints, poses, mapping, presets );
		var joint = joints[ 5 ];
		Assert.Equal( Vector3.Zero, joint.FirstFrame.ChildPosition );
		Assert.Equal( poses[ 5 ].Translation, poses[ 2 ].Transform( joint.FirstFrame.BodyPosition ) );
		Assert.Equal( poses[ 5 ].Translation, poses[ 23 ].Transform( joint.SecondFrame.BodyPosition ) );
		Assert.Equal( new Vector4( 0f, 1f, 0f, 0f ), joint.FirstFrame.ChildRotation );
	}

	[Fact]
	public void UnmappedEntriesAndAuthoredTuningRemainUntouched()
	{
		var (joints, poses, mapping, presets) = Inputs();
		mapping[ 5 ] = 2;
		foreach ( var joint in joints )
		{
			joint.Mode = 4;
			joint.Dirty = true;
			joint.ChildBone = 17;
			joint.FirstWeight = .25f;
			joint.VelocityGain = 11f;
			joint.FirstStrength = .75f;
		}
		PhysicalJointInitialization.Initialize( joints, poses, mapping, presets );
		Assert.Equal( 4, joints[ 4 ].Mode );
		Assert.True( joints[ 4 ].Dirty );
		Assert.Equal( 17, joints[ 4 ].ChildBone );
		Assert.False( joints[ 4 ].FirstEnabled );
		Assert.Equal( .25f, joints[ 5 ].FirstWeight );
		Assert.Equal( 11f, joints[ 5 ].VelocityGain );
		Assert.Equal( .75f, joints[ 5 ].FirstStrength );
	}

	[Fact]
	public void FullCanonicalInitializationIncludesMappedReservedEntries()
	{
		var (joints, poses, mapping, presets) = Inputs();
		mapping[ 0 ] = 2;
		mapping[ 23 ] = 5;
		PhysicalJointInitialization.Initialize( joints, poses, mapping, presets );
		Assert.True( joints[ 0 ].FirstEnabled );
		Assert.Equal( 0, joints[ 0 ].ChildBone );
		Assert.True( joints[ 23 ].SecondEnabled );
		Assert.Equal( 23, joints[ 23 ].ChildBone );
	}

	[Fact]
	public void InvalidMappingIsRejectedBeforeAnyJointChanges()
	{
		var (joints, poses, mapping, presets) = Inputs();
		mapping[ 1 ] = 0;
		mapping[ 23 ] = 24;
		Assert.Throws<ArgumentOutOfRangeException>( () => PhysicalJointInitialization.Initialize( joints, poses, mapping, presets ) );
		Assert.False( joints[ 1 ].FirstEnabled );
	}

	[Fact]
	public void InitializedFramesFeedRegisteredSharedBodyReactions()
	{
		var (joints, poses, mapping, presets) = Inputs();
		mapping[ 1 ] = 0;
		PhysicalJointInitialization.Initialize( joints, poses, mapping, presets );
		PhysicalBodyState Body( int bone ) => new()
		{
			Pose = poses[ bone ], Orientation = Vector4.UnitW, MotionMode = 4,
			InverseMass = 1f, InverseInertiaDiagonal = Vector3.One
		};
		PhysicalBodyState[] bodies = [ Body( 1 ), Body( 0 ), Body( 23 ) ];
		bodies[ 0 ].Pose = bodies[ 0 ].Pose with { Translation = new( 1.1f, 0f, 0f ) };
		var settings = new BodyDynamicsSettings( Vector3.One, 0f, 0f, 100f, 100f, 1f, 1f, 4 );
		var world = new PhysicalSolverWorld( bodies, [ settings, settings, settings ], [ 0, 1, 2 ], 2 );
		world.RegisterPoseAttachment( joints[ 1 ], false, 0, 1 );
		world.RegisterPoseAttachment( joints[ 1 ], true, 0, 2 );
		world.Prepare();
		var drives = new DriveConstraint[ 2 ];
		Assert.Equal( 2, world.CompilePoseAttachments( drives, .1f ) );
		world.SolveAndIntegrate( [], [], drives, 1, .1f, Vector3.Zero );
		Assert.InRange( MathF.Abs( bodies[ 0 ].Velocity.X + .5f ), 0f, 1e-5f );
		Assert.InRange( MathF.Abs( bodies[ 2 ].Velocity.X - .5f ), 0f, 1e-5f );
		Assert.Equal( Vector3.Zero, bodies[ 1 ].Velocity );
	}

	private static (PhysicalJointState[], LimbRigidTransform[], int[], PhysicalJointPreset[]) Inputs()
	{
		var joints = new PhysicalJointState[ 24 ];
		var poses = new LimbRigidTransform[ 24 ];
		var mapping = new int[ 24 ];
		for ( var i = 0; i < 24; ++i )
		{
			joints[ i ] = new();
			poses[ i ] = new( LimbRigidTransform.Identity.Rotation, new( i, 0f, 0f ) );
			mapping[ i ] = -1;
		}
		return (joints, poses, mapping, [ new( .5f, .25f, .75f, .125f ), new( 1.5f, 1.25f, 1.75f, 1.125f ) ]);
	}
}
