using System.Numerics;
using SkateMotion.Core;
using SkateMotion.Core.Animation;
using SkateMotion.Core.Physics;
using SkateMotion.Core.Vehicle;
using Xunit;

namespace SboxSkateMotion.Tests;

public class PhysicalSolverBodyTests
{
	[Theory]
	[InlineData( 0, false )]
	[InlineData( 1, false )]
	[InlineData( 2, false )]
	[InlineData( 4, true )]
	[InlineData( 7, true )]
	[InlineData( 12, true )]
	public void ResponseUsesTheMotionFlagWithoutChangingStoredMass( int mode, bool responds )
	{
		var physical = Body( mode );
		var result = PhysicalSolverBody.Read( physical );
		Assert.Equal( responds ? .25f : 0f, result.InverseMass );
		Assert.Equal( responds ? new Matrix4x4( 2f, .1f, .2f, 0f, .1f, 3f, .3f, 0f,
			.2f, .3f, 4f, 0f, 0f, 0f, 0f, 1f ) : default, result.WorldInverseInertia );
		Assert.Equal( .25f, physical.InverseMass );
		Assert.Equal( new Vector3( 2f, 3f, 4f ), physical.InverseInertiaDiagonal );
		Assert.Equal( new Vector3( .1f, .2f, .3f ), physical.InverseInertiaOffDiagonal );
		Assert.Equal( mode, physical.MotionMode );
	}

	[Fact]
	public void NonRespondingBodyStillSuppliesItsPoseAndPredictedMotion()
	{
		var physical = Body( 2 );
		var result = PhysicalSolverBody.Read( physical );
		Assert.Equal( physical.Pose.Translation, result.Position );
		Assert.Equal( new Quaternion( .1f, -.2f, .3f, .9f ), result.Rotation );
		Assert.Equal( physical.Velocity, result.Velocity );
		Assert.Equal( physical.AngularVelocity, result.AngularVelocity );
		Assert.Equal( physical.LinearAccumulator, result.Acceleration );
		Assert.Equal( physical.AngularAccumulator, result.AngularAcceleration );
		Assert.Equal( physical.MotionMetric, result.MotionMetric );
	}

	[Theory]
	[InlineData( 0 )]
	[InlineData( 1 )]
	public void BoardBindsPhysicalHandAndTargetWithoutIntegratingTheirOwner( int hand )
	{
		var board = new ArticulatedBoard( new SkateSettings(), new Vector3[]
		{
			new( .26f, .095f, -.04f ), new( .26f, -.095f, -.04f ),
			new( -.26f, .095f, -.04f ), new( -.26f, -.095f, -.04f )
		}, .0285f );
		board.SetRiderAttached( false );
		var physical = Body( 2 );
		physical.Pose = new( LimbRigidTransform.Identity.Rotation, new( .01f, 0f, 0f ) );
		physical.Orientation = new( 0f, 0f, 0f, 1f );
		physical.Velocity = new( .2f, 0f, 0f );
		physical.AngularVelocity = Vector3.Zero;
		physical.LinearAccumulator = Vector3.Zero;
		physical.AngularAccumulator = Vector3.Zero;
		board.SetPhysicalHandBody( hand, physical );
		board.SetTargetBody( physical );
		board.HeldConstraints.Activate( hand, LimbRigidTransform.Identity, LimbRigidTransform.Identity );
		board.TargetDrive.FollowPose( 1f );
		board.BeginStep();
		board.Step( [], 0f, 0f, 1f / 240f );
		Assert.True( board.Deck.Velocity.X > 0f );
		Assert.Equal( Vector3.Zero, board.HandResponse( hand ).LinearVelocity );
		Assert.Equal( 0f, board.HandBody( hand ).InverseMass );
		Assert.Equal( 0f, board.TargetBody.InverseMass );
		Assert.Equal( physical.Velocity, board.TargetBody.Velocity );
		Assert.Equal( physical.Pose.Translation, board.HandBody( hand ).Position );
		Assert.Equal( new Vector3( .01f, 0f, 0f ), physical.Pose.Translation );
		Assert.Equal( .25f, physical.InverseMass );
	}

	private static PhysicalBodyState Body( int mode ) => new()
	{
		MotionMode = mode, InverseMass = .25f,
		InverseInertiaDiagonal = new( 2f, 3f, 4f ),
		InverseInertiaOffDiagonal = new( .1f, .2f, .3f ),
		Pose = new( LimbRigidTransform.Identity.Rotation, new( 1f, 2f, 3f ) ),
		Orientation = new( .1f, -.2f, .3f, .9f ),
		Velocity = new( 4f, 5f, 6f ), AngularVelocity = new( 7f, 8f, 9f ),
		LinearAccumulator = new( 10f, 11f, 12f ), AngularAccumulator = new( 13f, 14f, 15f ),
		MotionMetric = 16f
	};
}
