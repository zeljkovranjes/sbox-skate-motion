using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class PhysicalPosePreparationTests
{
	[Fact]
	public void UnboundTargetsKeepTheirRequestedScale()
	{
		var pose = new LimbRigidTransform( new( Vector3.UnitX * 2, Vector3.UnitY * 3, Vector3.UnitZ * 4 ), new( 5, 6, 7 ) );
		var target = new PhysicalBodyBinding();
		target.Write( pose );
		Assert.Equal( pose, target.CachedPose );
		Assert.Equal( pose, target.Read() );
		Assert.Null( target.Body );
	}

	[Fact]
	public void LocalOffsetSeparatesCachedTargetFromBodyPosition()
	{
		var target = Bound();
		target.LocalOffset = LimbRigidTransform.Identity with { Translation = new( 1, 2, 3 ) };
		var pose = LimbRigidTransform.Identity with { Translation = new( 4, 6, 8 ) };
		target.Write( pose );
		Assert.Equal( new Vector3( 3, 4, 5 ), target.Body.Pose.Translation );
		Assert.Equal( pose, target.CachedPose );
		Assert.Equal( pose, target.Read() );
	}

	[Theory]
	[InlineData( 0 )]
	[InlineData( 1 )]
	[InlineData( 2 )]
	public void OneMissingAxisIsReconstructedFromTheOtherTwo( int missing )
	{
		var axes = new[] { Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ };
		axes[ missing ] = Vector3.Zero;
		var target = Bound();
		target.Write( new( new( axes[ 0 ], axes[ 1 ], axes[ 2 ] ), Vector3.Zero ) );
		Assert.Equal( LimbRigidTransform.Identity, target.Body.Pose );
		Assert.Equal( new Vector4( 0, 0, 0, 1 ), target.Body.Orientation );
	}

	[Fact]
	public void BodyWithoutMassPropertiesRetainsExistingInertia()
	{
		var target = Bound();
		target.Body.InverseInertiaDiagonal = new( 2, 3, 4 );
		target.Body.InverseInertiaOffDiagonal = new( 5, 6, 7 );
		target.Body.InverseMass = .25f;
		target.Write( LimbRigidTransform.Identity with { Translation = Vector3.UnitX } );
		Assert.Equal( new Vector3( 2, 3, 4 ), target.Body.InverseInertiaDiagonal );
		Assert.Equal( new Vector3( 5, 6, 7 ), target.Body.InverseInertiaOffDiagonal );
		Assert.Equal( .25f, target.Body.InverseMass );
	}

	[Fact]
	public void RotatedBodyUpdatesWorldInertiaFromLocalMassProperties()
	{
		var target = Bound();
		target.Body.Mass = new( new( 2, 3, 4 ), .5f );
		target.Write( new( new( Vector3.UnitY, -Vector3.UnitX, Vector3.UnitZ ), Vector3.Zero ) );
		Assert.Equal( new Vector3( 3, 2, 4 ), target.Body.InverseInertiaDiagonal );
		Assert.Equal( Vector3.Zero, target.Body.InverseInertiaOffDiagonal );
		Assert.Equal( .5f, target.Body.InverseMass );
	}

	[Fact]
	public void FailedDistanceGateStillWritesTargetsAndClampedFeedback()
	{
		var state = new PhysicalPosePreparationState
		{
			ModelToWorld = LimbRigidTransform.Identity with { Translation = new( 2, 0, 0 ) },
			SolverFrame = LimbRigidTransform.Identity, ModelOffset = LimbRigidTransform.Identity,
			ContactFrame = LimbRigidTransform.Identity, SecondaryTarget = LimbRigidTransform.Identity,
			FirstFeedbackFrame = LimbRigidTransform.Identity with { Translation = new( 10, 0, 0 ) },
			SecondFeedbackFrame = LimbRigidTransform.Identity with { Translation = new( 0, -10, 0 ) }
		};
		var targets = new[] { Bound(), Bound(), Bound(), Bound() };
		var feedback = new[] { Bound(), Bound() };
		Assert.Equal( 0, PhysicalPosePreparation.Update( state, targets, feedback ) );
		Assert.Equal( new Vector3( 2, 0, 0 ), targets[ 0 ].Body.Pose.Translation );
		Assert.Equal( new Vector3( 40, 0, 0 ), feedback[ 0 ].Body.Velocity );
		Assert.Equal( new Vector3( 0, -40, 0 ), feedback[ 1 ].Body.Velocity );
		Assert.Equal( state.SecondFeedbackFrame.Translation, state.CachedPosition );
	}

	private static PhysicalBodyBinding Bound() => new()
	{
		CachedPose = LimbRigidTransform.Identity,
		Body = new() { Pose = LimbRigidTransform.Identity }
	};
}
