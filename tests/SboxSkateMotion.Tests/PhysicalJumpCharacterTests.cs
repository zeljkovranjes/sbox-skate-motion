using System;
using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class PhysicalJumpCharacterTests
{
	private static readonly LimbRigidTransform Identity = LimbRigidTransform.Identity;

	[Fact]
	public void JumpVelocityFeedsPlacementBeforeItIsPublishedAgain()
	{
		var character = State( true );
		character.BoardBodies[ 6 ].Body.Pose = At( new( 2f, 3f, 4f ) );
		character.BoardCorrectionFlag = 7;
		var bodies = character.BoardBodies.Select( binding => binding.Body ).ToArray();
		foreach ( var body in bodies )
			body.Velocity = new( 77f, 88f, 99f );
		var attributes = new PhysicalAnimationAttributeState { MotionFlags = 1UL << 22, JumpStrength = .75f };
		var launch = new PhysicalJumpVelocityState { Cooldown = 1f };
		var state = new PhysicalJumpCharacterState();
		var placement = new PhysicalRiderFrameState();
		var accepted = new PhysicalJumpCharacterController( Create() ).TryUpdate( state, attributes, launch,
			new( new( 2f, 3f, -4f ), default, default, Vector3.UnitZ, Vector3.UnitX, Vector3.UnitZ,
				default, default, 0, false ), new( .5f, 1.5f, 99f ), bodies, character, placement, Input( mode: 7 ) );
		Assert.True( accepted );
		Assert.True( state.Launched );
		Assert.Equal( new Vector3( 2.5f, 3.75f, 4.321875f ), placement.Anchor );
		Assert.Equal( placement.Anchor, character.RootFrame.Translation );
		Assert.Equal( (byte)0, character.BoardCorrectionFlag );
		Assert.Equal( 0x3fa4ccccu, BitConverter.SingleToUInt32Bits( state.Velocity.Z ) );
		foreach ( var body in bodies )
			Assert.Equal( state.Velocity, body.Velocity );
	}

	[Fact]
	public void MissingJumpRequestDeclinesTheBranchWithoutRunningCharacterPlacement()
	{
		var character = State( true );
		var placement = new PhysicalRiderFrameState { Anchor = new( 4f, 5f, 6f ), PreserveAnchor = true };
		var state = new PhysicalJumpCharacterState { Launched = true, Velocity = Vector3.UnitX };
		var launch = new PhysicalJumpVelocityState { Cooldown = .5f, HoldFrames = 2 };
		var body = new PhysicalBodyState { Velocity = Vector3.UnitY };
		var accepted = new PhysicalJumpCharacterController( Create() ).TryUpdate( state, new(), launch,
			default, default, [ body ], character, placement, Input() );
		Assert.False( accepted );
		Assert.False( state.Launched );
		Assert.Equal( Vector3.UnitX, state.Velocity );
		Assert.Equal( Vector3.UnitY, body.Velocity );
		Assert.True( placement.PreserveAnchor );
		Assert.Equal( new Vector3( 4f, 5f, 6f ), placement.Anchor );
		Assert.Equal( .5f, launch.Cooldown );
		Assert.Equal( 2, launch.HoldFrames );
	}

	private static PhysicalCharacterState State( bool sharedOwner )
	{
		var poses = new[] { Identity, Identity, At( -.8f * Vector3.UnitY ) };
		var skeleton = new EffectorRigSkeleton( Enumerable.Repeat( new EffectorBoneBinding( 2, -1, 10f ), 4 ).ToArray(),
			[ 0, 0, 1 ], Enumerable.Repeat( Identity, 3 ).ToArray(), poses, poses.ToArray() );
		var pose = new EffectorPoseState( new EffectorTargetState( 0, Vector3.Zero, Vector3.Zero,
			Vector3.Zero, Vector3.UnitY, 0f, 0f, false ), Identity, Identity, Identity, Identity, Identity, Identity, Vector3.Zero, false );
		var rig = new EffectorRigState( new EffectorWeightState[ 4 ], Enumerable.Repeat( pose, 4 ).ToArray(),
			new FootReachState[ 2 ], skeleton, default );
		var prep = new PhysicalPosePreparationState
		{
			ModelOffset = Identity, SecondaryTarget = Identity, FirstFeedbackFrame = Identity, SecondFeedbackFrame = Identity
		};
		var cache = new EffectorPoseCache( 3 );
		Array.Copy( poses, cache.Bones, 3 );
		var physical = new PhysicalRigState( rig, cache, prep, new( 5 ), Bindings( 4 ), Bindings( 5 ), [], [ default, default ] );
		var owner = new PhysicalFrameOwner { Frame = Identity };
		return new( physical, owner, sharedOwner ? owner : new() { Frame = Identity }, Bindings( 1 )[ 0 ], Bindings( 7 ) );
	}

	private static PhysicalBodyBinding[] Bindings( int count ) => Enumerable.Range( 0, count )
		.Select( _ => new PhysicalBodyBinding { CachedPose = Identity, Body = new() { Pose = Identity, InverseMass = 1f } } ).ToArray();

	private static PhysicalRidingCharacterInput Input( float placementDelta = .25f, byte mode = 0 ) => new( Identity,
		new PhysicalRigInput( new( Identity, Identity, Identity, Identity, Identity, Identity, Identity, Vector3.Zero, Vector3.Zero ),
			new( Enumerable.Repeat( new EffectorPoseInput( Identity, null, Identity, 0f, 0f ), 4 ).ToArray(),
				new bool[ 4 ], new bool[ 4 ], new FootReachInput[ 2 ], false, false, false, false, 0, false, false, 0f, 180f ),
			new( 1f, true, 1f, 1f ), new byte[ 3 ], 1f / 60f, 9.81f, false, false, false ), placementDelta, mode );

	private static LimbRigidTransform At( Vector3 position ) => Identity with { Translation = position };

	private static PhysicalRidingCharacterController Create()
	{
		var math = new LimbSolverMath( [ -1f / 720f, 1f / 24f, -.5f ],
			[ -1f / 5040f, 1f / 120f, -1f / 6f ], [ 0f, 0f, 0f, 0f, 0f, 0f, 0f, MathF.PI / 2f ],
			2f / MathF.PI, MathF.PI / 2f, 0f, MathF.PI );
		var targets = new EffectorTargetSolver( math, new LimbArcSine( 0f, 0f, 0f, 1f, MathF.PI / 2f ),
			new EffectorTargetSettings( .0001f, .5f, .125f, .01f, .25f, -.6f, .6f ) );
		var arc = new LimbArcTangent( [ 1f / 17f, -1f / 15f, 1f / 13f, -1f / 11f, 1f / 9f, -1f / 7f, 1f / 5f, -1f / 3f ], MathF.PI / 2f, MathF.PI );
		var positions = new LimbPositionSolver( math, .0005f, MathF.PI / 180f, 180f / MathF.PI );
		var rig = new PhysicalRigController( new EffectorRigController(
			new EffectorWeightController( .25f, .25f, Vector3.One, Vector3.One ), new EffectorPosePreparer( targets ),
			new EffectorPoseBlender( new LimbPoseBlender( math, arc, .00001f, .1f ) ),
			new FootReachController( new FootReachSettings( 0f, 0f, 0f, 0f, 0f, 0f, 1f, 0f, 1f, 1f, 0f ) ),
			new EffectorRigSolver( new LimbChainSolver( positions, new LimbRotationSolver( .0001f, .001f ), .0001f ) ), Vector3.One ) );
		var curve = new BoardCorrectionCurve( [ 0f, .5f, 1f, 1.5f, 2f, 2.5f, 3f, 3.5f ],
			[ 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f ] );
		var alternate = new BoardCorrectionCurve( [ 0f, .5f, 1f, 1.5f, 2f, 2.5f, 3f, 3.5f ], new float[ 8 ] );
		var board = new BoardFrameCorrection( math, curve, alternate,
			new( 2f * MathF.PI, 1f / (2f * MathF.PI), .95f, .996f, MathF.Sqrt( 2f ) ) );
		return new( new PhysicalRiderFrameController( math,
			new LimbSignedAngle( math, 1e-8f, MathF.PI * 2f, 1f / (MathF.PI * 2f) ) ), rig, board );
	}
}
