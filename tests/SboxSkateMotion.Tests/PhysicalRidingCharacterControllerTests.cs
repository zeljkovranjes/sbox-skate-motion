using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class PhysicalRidingCharacterControllerTests
{
	private static readonly LimbRigidTransform Identity = LimbRigidTransform.Identity;

	[Fact]
	public void SharedOutputFeedsTheTargetAndNextPlacementWithoutRewritingBoardVelocities()
	{
		var state = State( true );
		var placement = new PhysicalRiderFrameState();
		state.Rig.Rig.Skeleton.MutableBones[ 0 ] = At( Vector3.UnitX );
		state.Output.UpdateFlags = 1UL << 50;
		for ( var i = 0; i < state.BoardBodies.Length; i++ )
			state.BoardBodies[ i ].Body.Velocity = new( 3 + i, 2, -1 );
		state.BoardBodies[ 6 ].Body.Pose = At( new( 2, 3, 4 ) );
		state.BoardTarget.Body = state.BoardBodies[ 6 ].Body;
		var controller = Create();
		controller.Update( state, placement, Input() );
		Assert.Equal( new Vector3( 4.25f, 3.5f, 3.75f ), placement.Anchor );
		Assert.Equal( new Vector3( 5.25f, 3.5f, 3.75f ), state.RootFrame.Translation );
		Assert.Equal( state.RootFrame, state.Output.Frame );
		Assert.Equal( state.RootFrame, state.AuthoredFrame );
		Assert.Equal( state.RootFrame, state.BoardTarget.Body.Pose );
		Assert.Equal( (1UL << 50) | 0x80000UL, state.Output.UpdateFlags );
		controller.Update( state, placement, Input() );
		Assert.Equal( new Vector3( 8.5f, 4f, 3.5f ), state.RootFrame.Translation );
		for ( var i = 0; i < state.BoardBodies.Length; i++ )
			Assert.Equal( new Vector3( 3 + i, 2, -1 ), state.BoardBodies[ i ].Body.Velocity );
	}

	[Fact]
	public void SeparateBoardSourceDoesNotReplaceTheCharactersSecondaryTarget()
	{
		var state = State( false );
		state.BoardSource.Frame = At( new( -1, -2, -3 ) );
		state.Rig.Reset.WorldReference = new( 5, 6, 7 );
		var placement = new PhysicalRiderFrameState { Anchor = new( 2, 3, 4 ), PreserveAnchor = true };
		Create().Update( state, placement, Input() );
		Assert.Equal( new Vector3( 2, 3, 4 ), state.Output.Frame.Translation );
		Assert.Equal( new Vector3( -1, -2, -3 ), state.AuthoredFrame.Translation );
		Assert.Equal( state.RootFrame, state.Rig.Preparation.SecondaryTarget );
		Assert.Equal( new Vector3( 5, 6, 7 ), state.Rig.Preparation.SecondFeedbackFrame.Translation );
		Assert.Equal( state.AuthoredFrame, state.BoardSource.Frame );
	}

	[Fact]
	public void PreparationUsesProducedPlacementAndDeckFramesWithTheCurrentRootOffset()
	{
		var state = State( true );
		state.BoardBodies[ 6 ].Body.Pose = At( new( 2, 3, 4 ) );
		state.RelativeBodyFrame = At( new( 100, 200, 300 ) );
		var placement = new PhysicalRiderFrameState
		{
			RootFrame = At( Vector3.UnitX ), Anchor = new( 10, 20, 30 ), PreserveAnchor = true
		};
		Create().Update( state, placement, Input() );
		Assert.Equal( placement.RootFrame, state.Rig.Preparation.ModelOffset );
		Assert.Equal( new Vector3( 9, 20, 30 ), state.Rig.Preparation.ModelToWorld.Translation );
		Assert.Equal( new Vector3( 8, 17, 26 ), state.Rig.Preparation.ComposedContact.Translation );
		Assert.Equal( Identity, state.RelativeBodyFrame );
		Assert.False( placement.PreserveAnchor );
	}

	[Theory]
	[InlineData( 0, 0 )]
	[InlineData( 1, 7 )]
	[InlineData( 255, 7 )]
	public void CorrectionModeSelectsTheRequestedCurve( byte mode, byte expectedFlag )
	{
		var state = State( true );
		state.BoardCorrectionFlag = 7;
		state.BoardOrientation = new( 0, 0, 0, 1 );
		Create().Update( state, new PhysicalRiderFrameState(), Input( mode: mode ) );
		Assert.Equal( expectedFlag, state.BoardCorrectionFlag );
		Assert.Equal( state.AuthoredFrame, state.BoardTarget.CachedPose );
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
