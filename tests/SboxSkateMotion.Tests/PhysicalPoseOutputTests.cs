using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class PhysicalPoseOutputTests
{
	private static readonly PhysicalJointPreset[] Presets = [ new( 1, 2, 3, 4 ), new( 5, 6, 7, 8 ) ];

	[Fact]
	public void MissingChildPreservesFramesAndDrivesButUpdatesMode()
	{
		var joint = Joint();
		joint.ChildBone = 0;
		joint.FirstStrength = 17f;
		joint.FirstConstraint = new( 1, 2, 3, 4 );
		var oldFrame = joint.FirstFrame;
		Apply( joint, new( .5f, true, .75f, .5f ), 1 );
		Assert.Equal( 3, joint.Mode );
		Assert.Equal( 17f, joint.FirstStrength );
		Assert.Equal( new PhysicalJointDrive( 1, 2, 3, 4 ), joint.FirstConstraint );
		Assert.Equal( oldFrame, joint.FirstFrame );
		Assert.True( joint.Dirty );
	}

	[Fact]
	public void DisabledFallbackDoesNotSuppressMaskedStrength()
	{
		var joint = Joint();
		Apply( joint, new( .5f, false, .75f, .5f ), 1 );
		Assert.Equal( .46875f, joint.FirstStrength );
		Assert.Equal( .375f, joint.SecondStrength );
		Assert.Equal( 1, joint.SecondConstraint.Mode );
		Assert.Equal( 200f, joint.SecondConstraint.MaximumForce );
		Apply( joint, new( .5f, false, .75f, .5f ), 0 );
		Assert.Equal( 0f, joint.FirstStrength );
		Assert.Equal( 0f, joint.SecondStrength );
		Assert.Equal( 2, joint.SecondConstraint.Mode );
	}

	[Fact]
	public void DisabledAttachmentsKeepTheirPreviousOutputs()
	{
		var joint = Joint();
		Apply( joint, new( 1, true, 1, 1 ), 0 );
		var frame = joint.FirstFrame;
		var drive = joint.SecondConstraint;
		joint.FirstEnabled = false;
		joint.SecondEnabled = false;
		joint.Dirty = true;
		Apply( joint, new( 0, true, 2, 3 ), 1 );
		Assert.Equal( frame, joint.FirstFrame );
		Assert.Equal( drive, joint.SecondConstraint );
		Assert.Equal( .5f, joint.FirstStrength );
		Assert.Equal( 1.5f, joint.SecondStrength );
		Assert.True( joint.Dirty );
	}

	[Fact]
	public void AttachmentsExpressOneWorldAnchorInBothBodies()
	{
		var poses = new[] { LimbRigidTransform.Identity,
			LimbRigidTransform.Identity with { Translation = new( 3, 5, 7 ) },
			LimbRigidTransform.Identity with { Translation = new( 1, 2, 4 ) } };
		var inverses = new LimbRigidTransform[ 3 ];
		inverses[ 0 ] = LimbRigidTransform.Identity;
		var joint = Joint();
		joint.FirstBodyBone = 2;
		var root = PhysicalPoseOutput.Update( poses, inverses, [ joint ], [ 0, 0 ], new( 1, true, 1, 1 ), Presets );
		Assert.Equal( LimbRigidTransform.Identity, inverses[ 0 ] );
		Assert.Equal( Vector3.Zero, joint.FirstFrame.ChildPosition );
		Assert.Equal( new Vector3( 2, 3, 3 ), joint.FirstFrame.BodyPosition );
		Assert.Equal( new Vector4( 0, 0, 0, 1 ), joint.FirstFrame.BodyRotation );
		Assert.False( joint.Dirty );
		Assert.Equal( 2, root.Mode );
		Assert.Equal( 35999.99609375f, root.PositionGain );
	}

	[Fact]
	public void BlendWeightsAreNotClamped()
	{
		var joint = Joint();
		joint.FirstWeight = -1;
		joint.SecondWeight = 2;
		Apply( joint, new( -.5f, true, -.5f, 2f ), 1 );
		Assert.Equal( 1f, joint.FirstStrength );
		Assert.Equal( 5f, joint.SecondStrength );
	}

	private static PhysicalJointState Joint() => new()
	{
		ChildBone = 1, FirstBodyBone = 1, SecondBodyBone = 2,
		FirstEnabled = true, SecondEnabled = true, Dirty = true,
		FirstWeight = .25f, SecondWeight = .5f, VelocityGain = 2f, PositionGain = .5f
	};

	private static void Apply( PhysicalJointState joint, PhysicalPoseSettings settings, byte mask )
	{
		var poses = new[] { LimbRigidTransform.Identity, LimbRigidTransform.Identity, LimbRigidTransform.Identity };
		PhysicalPoseOutput.Update( poses, new LimbRigidTransform[ 3 ], [ joint ], [ 0, mask ], settings, Presets );
	}
}
