using System;
using System.Linq;
using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class PhysicalAnimationRigInputBinderTests
{
	[Fact]
	public void CurrentAttributesReplaceStaleActivationAndContactInputs()
	{
		var state = State();
		state.FootActivation = 7;
		state.Pose.Attributes.ActionFlags = 1UL << 39;
		state.Pose.Attributes.RigFlags = 1UL << 59;
		state.Pose.Attributes.MotionFlags = (1UL << 30) | (1UL << 23);
		state.Pose.Attributes.ContactBone = 13;
		var binder = new PhysicalAnimationRigInputBinder();
		var result = binder.Bind( state, Input() ).RigInput;
		Assert.True( result.FeetEnabled );
		Assert.True( result.LeftHandEnabled );
		Assert.False( result.RightHandEnabled );
		Assert.True( result.DisableFeet );
		Assert.True( result.ReactionEnabled );
		Assert.True( result.RequestSuppressed );
		Assert.Equal( 13, result.SelectedContactId );
		state.FootActivation = 0;
		state.Pose.Attributes.ActionFlags = 1UL << 40;
		state.Pose.Attributes.RigFlags = 0;
		state.Pose.Attributes.MotionFlags = 0;
		result = binder.Bind( state, Input() with { RigInput = result } ).RigInput;
		Assert.False( result.FeetEnabled );
		Assert.False( result.LeftHandEnabled );
		Assert.True( result.RightHandEnabled );
		Assert.False( result.DisableFeet );
		Assert.False( result.ReactionEnabled );
		Assert.False( result.RequestSuppressed );
	}

	[Fact]
	public void AuthoredAndSolverFramesComeFromAnimationWhilePhysicalSourcesRemainIndependent()
	{
		var state = State();
		for ( var i = 0; i < 4; i++ )
			state.Pose.Authored[ i ] = At( 10 + i );
		state.Pose.CachedRoot = At( 20 );
		state.Pose.CachedBoard = At( 30 );
		var input = Input();
		var result = new PhysicalAnimationRigInputBinder().Bind( state, input );
		Assert.Equal( At( 20 ), result.Frames.Solver );
		Assert.Equal( At( 30 ), result.Frames.AnimatedRoot );
		Assert.Equal( input.Frames.ModelToWorld, result.Frames.ModelToWorld );
		Assert.Equal( input.Frames.Contact, result.Frames.Contact );
		Assert.Equal( input.Frames.MotionDelta, result.Frames.MotionDelta );
		Assert.Equal( input.DeltaTime, result.DeltaTime );
		Assert.Same( input.RigInput.Contacts, result.RigInput.Contacts );
		Assert.Same( input.RigInput.Pinning, result.RigInput.Pinning );
		for ( var i = 0; i < 4; i++ )
		{
			Assert.Equal( At( 10 + i ), result.RigInput.Poses[ i ].Authored );
			Assert.Equal( input.RigInput.Poses[ i ].Animated, result.RigInput.Poses[ i ].Animated );
			Assert.Equal( input.RigInput.Poses[ i ].TargetWeight, result.RigInput.Poses[ i ].TargetWeight );
		}
	}

	[Fact]
	public void FootActivationDoesNotChangePhysicalResetOrSolvePolicy()
	{
		var state = State();
		state.RigResetFlag = 9;
		Assert.Equal( 9, state.FootActivation );
		var input = Input() with { SolveRig = false, BindAnimatedRoot = true, MarkReset = false };
		var result = new PhysicalAnimationRigInputBinder().Bind( state, input );
		Assert.True( result.RigInput.FeetEnabled );
		Assert.False( result.SolveRig );
		Assert.True( result.BindAnimatedRoot );
		Assert.False( result.MarkReset );
	}

	[Fact]
	public void RebindingReusesItsOwnStorageAndLeavesSuppliedPoseSlotsUntouched()
	{
		var state = State();
		var input = Input();
		var binder = new PhysicalAnimationRigInputBinder();
		var first = binder.Bind( state, input );
		Assert.NotSame( input.RigInput.Poses, first.RigInput.Poses );
		state.Pose.Authored[ 0 ] = At( 9 );
		var second = binder.Bind( state, input );
		Assert.Same( first.RigInput.Poses, second.RigInput.Poses );
		Assert.Equal( At( 9 ), second.RigInput.Poses[ 0 ].Authored );
		Assert.Equal( At( 3 ), input.RigInput.Poses[ 0 ].Authored );
	}

	private static PhysicalAnimationInputState State()
	{
		var skeleton = new EffectorRigSkeleton( new EffectorBoneBinding[ 4 ], new int[ 24 ],
			new LimbRigidTransform[ 24 ], new LimbRigidTransform[ 24 ], new LimbRigidTransform[ 24 ] );
		var pose = new PhysicalAnimationPoseState( skeleton, new EffectorPoseCache( 24 ), new PhysicalAnimationAttributeState(), new LimbRigidTransform[ 4 ] );
		return new PhysicalAnimationInputState( pose, new GrindApproachState(), Array.Empty<PhysicalBodyBinding>(), new PhysicalBodyState() );
	}

	private static PhysicalRigInput Input()
	{
		var poses = Enumerable.Range( 0, 4 ).Select( i => new EffectorPoseInput( At( 1 ), At( 2 ), At( 3 ), .25f, .75f ) ).ToArray();
		var rig = new EffectorRigInput( poses, new bool[ 4 ], new bool[ 4 ], new FootReachInput[ 2 ],
			false, false, true, false, -1, false, false, 10f, 170f );
		var frames = new EffectorRigFrameSources( At( 4 ), At( 5 ), At( 6 ), At( 7 ), At( 8 ), At( 9 ), At( 10 ), Vector3.One, Vector3.UnitX );
		return new PhysicalRigInput( frames, rig, default, new byte[ 24 ], .125f, 7f, true, false, true );
	}

	private static LimbRigidTransform At( float x ) => LimbRigidTransform.Identity with { Translation = new Vector3( x, 0, 0 ) };
}
