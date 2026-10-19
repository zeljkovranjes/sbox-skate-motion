using System;
using System.Linq;
using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class PhysicalAnimationInputControllerTests
{
	[Theory]
	[InlineData( true, 6f )]
	[InlineData( false, 7f )]
	public void HandSelectionUsesLiveClipMappingsAndPublishesTheAttachedPose( bool first, float expected )
	{
		var controller = Controller( out _, out _ );
		var state = State();
		state.Pose.Attributes.ActionFlags = (1UL << 8) | (1UL << 16) | (first ? 4UL : 0UL);
		var input = Input();
		input.Pose.Clips[ 2 ] = At( 7 );
		input.Pose.Clips[ 3 ] = At( 11 );
		input.Pose.Clips[ 30 ] = At( 1 );
		input.Pose.Clips[ 31 ] = At( 4 );
		input = input with { Attachment = input.Attachment with { AttachFromHand = true, Hand = At( 100 ), Helper = At( 200 ) } };
		controller.Update( state, input );
		Assert.Equal( At( expected ), state.Pose.CachedBoard );
		Assert.Equal( At( expected ), state.Pose.Cache.Bones[ 0 ] );
		Assert.Equal( LimbRigidTransform.Identity, state.Pose.GatheredBoard );
	}

	[Fact]
	public void NewlyGatheredTiltSuppressesGrindBeforeItCanSubmitAnAttachment()
	{
		var controller = Controller( out var retained, out _ );
		var state = State();
		state.Grind.CorrectionPosition = Vector3.One;
		state.Grind.AngularPosition = Vector3.One;
		var input = Input();
		input.Pose.Clips[ 25 ] = new LimbRigidTransform( new LimbFrame( Vector3.UnitY, -Vector3.UnitX, Vector3.UnitZ ), Vector3.Zero );
		input = input with { Attachment = input.Attachment with { Grind = input.Attachment.Grind with { Enabled = true, Suppressed = false } } };
		controller.Update( state, input );
		Assert.Equal( Vector3.Zero, state.Grind.CorrectionPosition );
		Assert.Equal( Vector3.Zero, state.Grind.AngularPosition );
		Assert.False( state.Grind.RequestValid );
		Assert.Equal( 0f, retained.FrameRemaining );
	}

	[Fact]
	public void AttributeBalanceTriggersVerticalTransitionInTheSameUpdate()
	{
		var controller = Controller( out _, out var vertical );
		vertical.PreviousCategory = 2;
		var state = State();
		var input = Input();
		input = input with
		{
			Pose = input.Pose with { Attributes = input.Pose.Attributes with
			{
				Attributes = new[] { new PhysicalAnimationAttribute( AnimationAttributeKey.Encode( "BALANCE" ), 1f ) }
			} },
			Attachment = input.Attachment with { Vertical = input.Attachment.Vertical with { Category = 1, Trigger = 0f } }
		};
		controller.Update( state, input );
		Assert.True( vertical.Active );
		Assert.Equal( 2, vertical.Mode );
	}

	[Fact]
	public void BoardVelocityIsIndependentOfPreparedRiderBodies()
	{
		var controller = Controller( out _, out _ );
		var state = State();
		state.RiderBodies[ 0 ].Body.Velocity = new Vector3( 100 );
		state.RiderBodies[ 0 ].Body.AccumulatorState = 13;
		state.BoardReferenceBody.Velocity = new Vector3( -3, 4, -5 );
		state.BoardReferenceBody.AccumulatorState = 7;
		controller.Update( state, Input() );
		Assert.Equal( new Vector3( -3, 4, -5 ), state.Pose.BoardVelocity );
		Assert.Equal( 0, state.RiderBodies[ 0 ].Body.AccumulatorState );
		Assert.Equal( 7, state.BoardReferenceBody.AccumulatorState );
	}

	[Fact]
	public void ResetIsConsumedOnceAndReplacesBothDriveRequests()
	{
		var controller = Controller( out _, out _ );
		var state = State();
		state.ResetRequested = true;
		state.MotionFlag = 9;
		controller.Update( state, Input() );
		Assert.False( state.ResetRequested );
		Assert.Equal( 0, state.MotionFlag );
		Assert.Equal( 1, state.RigResetFlag );
		Assert.Equal( new PhysicalJointDrive( 2, 0, 3, 2 ), state.FirstDrive );
		Assert.Equal( state.FirstDrive, state.SecondDrive );
		state.FirstDrive = new PhysicalJointDrive( 8, 7, 6, 5 );
		state.MotionFlag = 4;
		controller.Update( state, Input() );
		Assert.Equal( new PhysicalJointDrive( 8, 7, 6, 5 ), state.FirstDrive );
		Assert.Equal( 4, state.MotionFlag );
	}

	private static PhysicalAnimationInputState State()
	{
		var skeleton = new EffectorRigSkeleton( new EffectorBoneBinding[ 4 ], new int[ 24 ],
			Enumerable.Repeat( LimbRigidTransform.Identity, 24 ).ToArray(), new LimbRigidTransform[ 24 ], new LimbRigidTransform[ 24 ] );
		var pose = new PhysicalAnimationPoseState( skeleton, new EffectorPoseCache( 24 ), new PhysicalAnimationAttributeState(), new LimbRigidTransform[ 4 ] )
		{
			RelativeBodyFrame = LimbRigidTransform.Identity
		};
		return new PhysicalAnimationInputState( pose, new GrindApproachState(),
			new[] { new PhysicalBodyBinding { Body = new PhysicalBodyState { MotionMode = 1 } } }, new PhysicalBodyState() );
	}

	private static PhysicalAnimationInput Input()
	{
		var identity = LimbRigidTransform.Identity;
		var mapping = Enumerable.Range( 1, 24 ).ToArray();
		mapping[ 0 ] = 25;
		var pose = new PhysicalAnimationPoseInput( Enumerable.Repeat( identity, 36 ).ToArray(), 0, mapping,
			new[] { 28, 29, 30, 31 }, new[] { 0, 1, 2, 3 }, identity, 1f, 1f, Vector3.Zero,
			new PhysicalAnimationAttributeInput( Array.Empty<PhysicalAnimationAttribute>(), Array.Empty<AnimationAttributeKey>(),
				Vector3.Zero, 0, 1f, false, Vector3.Zero, false, false, .2f, .8f, true, false, 0 ) );
		var attachment = new BoardAttachmentInput( false, identity, identity, false, false, new GrindAttachmentInput(),
			new DescendingAttachmentInput( 0f, false, identity, identity, identity, Vector3.Zero, 0f ), new VerticalAttachmentInput() );
		return new PhysicalAnimationInput( pose, 1, attachment, 2, 3, Vector3.UnitY, Vector3.Zero,
			new float[ 24 ], identity, false, false, Vector4.Zero );
	}

	private static PhysicalAnimationInputController Controller( out BoardAttachmentController retained, out VerticalAttachmentController vertical )
		=> new( Create( out retained, out vertical ), new PhysicalJointDrive( 2, 0, 3, 2 ) );

	private static LimbRigidTransform At( float x ) => LimbRigidTransform.Identity with { Translation = new Vector3( x, 0, 0 ) };

	private static BoardAttachmentSequence Create( out BoardAttachmentController attachment, out VerticalAttachmentController vertical )
	{
		var math = new LimbSolverMath( new[] { -1f / 720f, 1f / 24f, -.5f },
			new[] { -1f / 5040f, 1f / 120f, -1f / 6f }, new float[ 8 ], 2f / MathF.PI, MathF.PI / 2f, 0f, MathF.PI );
		var definitions = new GrindApproachCandidate[ 7 ];
		Array.Fill( definitions, new GrindApproachCandidate( 0f, 10f, 10f, 1f, 0 ) );
		var approach = new GrindApproachSolver( math,
			new GrindApproachSettings( 8, 0f, Vector3.Zero, 1e-6f, 1e-5f, 1f, 100f, 200f, .1f, .1f ),
			new[] { Vector3.Zero, Vector3.UnitZ, Vector3.Zero, Vector3.Zero, -Vector3.UnitZ }, definitions );
		var angular = new GrindAngularCorrector( math,
			new GrindAngularSettings( 1f, 1f, .5f, .9f, 1e-6f, 1e-4f, 2f * MathF.PI, 1f / (2f * MathF.PI) ), definitions );
		var grind = new GrindAttachmentController( approach, angular, new GrindAttachmentFrameBuilder( math ) );
		var arc = new LimbArcTangent( new float[ 8 ], MathF.PI / 2f, MathF.PI );
		var descending = new DescendingAttachmentController( new LimbPoseBlender( math, arc, .00001f, .1f ),
			new[] { 0f, 1f, 2f, 3f, 4f, 5f, 6f, 7f }, new[] { 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f } );
		var curve = new VerticalAttachmentCurve( new[] { 0f, 1f, 2f, 3f }, new[] { 1f, 1f, 1f, 1f } );
		vertical = new VerticalAttachmentController( new VerticalAttachmentSettings(
			.1f, 10f, 2f, 20f, .2f, 3f, 30f, .2f, 4f, 40f, .1f, .4f, .5f, 10f, 7f, 1f ), curve, curve, curve );
		attachment = new BoardAttachmentController();
		return new BoardAttachmentSequence( grind, descending, vertical, attachment );
	}
}
