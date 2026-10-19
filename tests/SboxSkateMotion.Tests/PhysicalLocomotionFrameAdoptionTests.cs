using System;
using System.Linq;
using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class PhysicalLocomotionFrameAdoptionTests
{
	private static readonly LimbSolverMath Math = new( [ 0f, 0f, -.5f ], [ 0f, 0f, -1f / 6f ],
		new float[ 8 ], 2f / MathF.PI, MathF.PI / 2f, 0f, MathF.PI );

	[Fact]
	public void AdoptsSolvedFrameAndStanceIntoPhysicalOwnerWithoutChangingMotion()
	{
		var rig = CreateRig();
		var state = new PhysicalLocomotionFrameState { TurnAngle = .4f, TurnRemaining = .3f };
		var solved = new PhysicalLocomotionMotionState( Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ,
			new( 4, 2, 7 ), new( 3, 1, -1 ) );
		new PhysicalLocomotionFrameAdoption( Math ).Update( state, rig, solved, 4 );
		Assert.Equal( new LimbFrame( -Vector3.UnitX, Vector3.UnitY, -Vector3.UnitZ ), rig.Preparation.ModelToWorld.Rotation );
		Assert.Equal( solved.Position, rig.Preparation.ModelToWorld.Translation );
		Assert.Equal( new Vector3( 4, -2, 7 ), state.WorldToModel.Translation );
		Assert.Equal( .4f, state.TurnAngle );
		Assert.Equal( .3f, state.TurnRemaining );
		Assert.True( state.FrameDirty );
	}

	[Fact]
	public void ExpiredTurnStillAppliesFinalCorrectionAndRetainsSolvedTranslation()
	{
		var rig = CreateRig();
		var state = new PhysicalLocomotionFrameState { TurnEnabled = true, TurnAngle = .4f, TurnRate = .8f, TurnRemaining = .01f };
		var solved = new PhysicalLocomotionMotionState( Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, new( 4, 2, 7 ), Vector3.Zero );
		var adoption = new PhysicalLocomotionFrameAdoption( Math );
		adoption.Update( state, rig, solved, 0 );
		Assert.False( state.TurnEnabled );
		Assert.Equal( 0f, state.TurnRemaining );
		Assert.Equal( .4f * .9f, state.TurnAngle );
		Assert.True( rig.Preparation.ModelToWorld.Rotation.Z.X > .3f );
		Assert.Equal( solved.Position, rig.Preparation.ModelToWorld.Translation );
		adoption.Update( state, rig, solved, 0 );
		Assert.Equal( Vector3.UnitZ, rig.Preparation.ModelToWorld.Rotation.Z );
	}

	private static PhysicalRigState CreateRig()
	{
		var bones = Enumerable.Repeat( LimbRigidTransform.Identity, 12 ).ToArray();
		var skeleton = new EffectorRigSkeleton( new EffectorBoneBinding[ 4 ], new int[ 12 ], bones.ToArray(), bones.ToArray(), bones );
		return new( new( new EffectorWeightState[ 4 ], new EffectorPoseState[ 4 ], new FootReachState[ 2 ], skeleton, default ),
			new( 12 ), new(), new( 14 ), new PhysicalBodyBinding[ 4 ], new PhysicalBodyBinding[ 14 ], [], [ default, default ] );
	}
}
