using System;
using System.Linq;
using System.Numerics;
using SkateMotion.Core.Animation;
using SkateMotion.Core.Physics;
using Xunit;

namespace SboxSkateMotion.Tests;

public class BoardHandlingPhysicalBindingTests
{
	private static readonly LimbArcTangent Angles = new( new float[ 8 ], MathF.PI / 2f, MathF.PI );

	[Fact]
	public void RetainsInputFrameAndReadsCurrentCanonicalSpineAtOutput()
	{
		var rig = CreateRig();
		rig.Preparation.ModelToWorld = new( new( Vector3.UnitX, new( 1, 1, 0 ), Vector3.UnitZ ), Vector3.Zero );
		var board = new PhysicalBoardInputState { PhysicalFrame = At( new( 3, 2, 1 ) ) };
		var binding = new BoardHandlingPhysicalBinding( rig, board, Angles );
		Assert.Throws<InvalidOperationException>( () => binding.Publish( default, 4, new() ) );
		binding.CaptureInput( 0 );
		rig.Preparation.ModelToWorld = At( new( 0, 1, 0 ) );
		rig.Rig.Skeleton.MutableBones[ 11 ] = At( new( 0, 1, 0 ) );
		rig.Rig.Skeleton.AnimatedBones[ 11 ] = At( new( 99, 99, 99 ) );
		var input = new BoardHandlingInput( false, true, false, true, true, false, false, 99f, 99f, true );
		var result = binding.Publish( input, 4, new() );
		Assert.Equal( input with { YawRadians = 1f, PitchRadians = 0f }, result );
		Assert.Equal( 0u, BitConverter.SingleToUInt32Bits( result.PitchRadians ) );
	}

	[Fact]
	public void PlacementUsesMotionSourceAndInputStanceFlag()
	{
		var rig = CreateRig();
		rig.Preparation.ModelToWorld = LimbRigidTransform.Identity;
		var board = new PhysicalBoardInputState { PhysicalFrame = At( new( 99, 0, 1 ) ) };
		var binding = new BoardHandlingPhysicalBinding( rig, board, Angles );
		binding.CaptureInput( 4 );
		var motion = new PhysicalBoardMotionState
		{
			Source = At( new( 1, 0, 2 ) ), Target = At( new( 30, 0, 1 ) ), Output = At( new( 40, 0, 1 ) )
		};
		Assert.Equal( .5f - MathF.PI, binding.Publish( default, 3, motion ).YawRadians );
		binding.CaptureInput( 0 );
		Assert.Equal( .5f, binding.Publish( default, 3, motion ).YawRadians );
	}

	private static LimbRigidTransform At( Vector3 position ) => LimbRigidTransform.Identity with { Translation = position };

	private static PhysicalRigState CreateRig()
	{
		var bones = Enumerable.Repeat( LimbRigidTransform.Identity, 12 ).ToArray();
		var skeleton = new EffectorRigSkeleton( new EffectorBoneBinding[ 4 ], new int[ 12 ],
			bones.ToArray(), bones.ToArray(), bones );
		var rig = new EffectorRigState( new EffectorWeightState[ 4 ], new EffectorPoseState[ 4 ],
			new FootReachState[ 2 ], skeleton, default );
		return new( rig, new( 12 ), new(), new( 14 ), new PhysicalBodyBinding[ 4 ],
			new PhysicalBodyBinding[ 14 ], [], [ default, default ] );
	}
}
