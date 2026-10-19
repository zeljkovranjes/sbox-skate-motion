using System;
using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class GrindAttachmentControllerTests
{
	[Theory]
	[InlineData( false )]
	[InlineData( true )]
	public void DisabledRequestsPreserveStateEvenWhenSuppressed( bool suppressed )
	{
		var state = RetainedState();
		var attachment = Pending();
		Create().Update( Input() with { Enabled = false, Suppressed = suppressed }, state, attachment );
		Assert.False( state.RequestValid );
		Assert.True( state.HasCandidate );
		Assert.Equal( Vector3.UnitX, state.CorrectionPosition );
		Assert.Equal( Vector3.UnitY, state.AngularPosition );
		Assert.Equal( 6, state.SelectedCandidate );
		Assert.Equal( 4, state.PreviousCandidate );
		AssertPending( attachment );
	}

	[Fact]
	public void SuppressionClearsPositionsButRetainsRatesAndSelectionHistory()
	{
		var state = RetainedState();
		var attachment = Pending();
		Create().Update( Input() with { Suppressed = true }, state, attachment );
		Assert.False( state.RequestValid );
		Assert.True( state.HasCandidate );
		Assert.Equal( Vector3.Zero, state.CorrectionPosition );
		Assert.Equal( Vector3.Zero, state.AngularPosition );
		Assert.Equal( Vector3.UnitZ, state.CorrectionVelocity );
		Assert.Equal( new Vector3( 2f ), state.AngularVelocity );
		Assert.Equal( 6, state.SelectedCandidate );
		Assert.Equal( 4, state.PreviousCandidate );
		AssertPending( attachment );
	}

	[Fact]
	public void ValidSelectionRequestsBothFrameAndHeightWithoutDecayingThem()
	{
		var state = new GrindApproachState();
		var attachment = Pending();
		Create().Update( Input(), state, attachment );
		Assert.True( state.RequestValid );
		Assert.True( state.HasCandidate );
		Assert.True( attachment.FrameRequested );
		Assert.True( attachment.HeightRequested );
		Assert.Equal( 15f, attachment.FrameRemaining );
		Assert.Equal( 15f, attachment.HeightRemaining );
		Assert.InRange( attachment.Frame.Translation.Z, -.10001f, -.09999f );
	}

	[Fact]
	public void NoCandidateLeavesAnEarlierAttachmentRequestIntact()
	{
		var state = RetainedState();
		var attachment = Pending();
		var input = Input();
		Create().Update( input with { Approach = input.Approach with { Velocity = Vector3.UnitY } }, state, attachment );
		Assert.False( state.RequestValid );
		Assert.False( state.HasCandidate );
		Assert.Equal( 6, state.PreviousCandidate );
		Assert.Equal( Vector3.UnitX, state.CorrectionPosition );
		AssertPending( attachment );
	}

	[Fact]
	public void DisabledUpdateAfterSuccessPublishesInvalidWithoutRemovingPendingFrame()
	{
		var controller = Create();
		var state = new GrindApproachState();
		var attachment = new BoardAttachmentController();
		controller.Update( Input(), state, attachment );
		var frame = attachment.Frame;
		controller.Update( Input() with { Enabled = false }, state, attachment );
		Assert.False( state.RequestValid );
		Assert.True( state.HasCandidate );
		Assert.Equal( frame, attachment.Frame );
		Assert.True( attachment.FrameRequested );
	}

	[Fact]
	public void SuppressionAfterSuccessClearsHistoryButLeavesThePendingAttachment()
	{
		var controller = Create();
		var state = new GrindApproachState();
		var attachment = new BoardAttachmentController();
		controller.Update( Input(), state, attachment );
		var frame = attachment.Frame;
		var velocity = state.CorrectionVelocity;
		controller.Update( Input() with { Suppressed = true }, state, attachment );
		Assert.False( state.RequestValid );
		Assert.True( state.HasCandidate );
		Assert.Equal( Vector3.Zero, state.CorrectionPosition );
		Assert.Equal( velocity, state.CorrectionVelocity );
		Assert.Equal( frame, attachment.Frame );
	}

	[Fact]
	public void FrameBuilderDiscardsModelAndWorldTranslations()
	{
		var builder = new GrindAttachmentFrameBuilder( MathProfile() );
		var state = FrameState();
		state.AngularPosition = new Vector3( .2f, .3f, .4f );
		var input = Input();
		var expected = builder.Build( input, state );
		var displaced = input with {
			ModelToWorld = input.ModelToWorld with { Translation = new Vector3( 100f, 200f, 300f ) },
			WorldToModel = input.WorldToModel with { Translation = new Vector3( -5f, 8f, 17f ) } };
		Assert.Equal( expected, builder.Build( displaced, state ) );
	}

	[Fact]
	public void FinalRotationPivotsAroundTheMutableBoard()
	{
		var state = FrameState();
		state.AngularPosition = new Vector3( 0f, MathF.PI / 2f, 0f );
		var input = Input() with { MutableBoard = LimbRigidTransform.Identity with { Translation = Vector3.UnitX } };
		var frame = new GrindAttachmentFrameBuilder( MathProfile() ).Build( input, state );
		Near( new Vector3( 1f, 0f, 1f ), frame.Translation );
		Near( Vector3.UnitX, frame.Transform( Vector3.UnitX ) );
	}

	[Fact]
	public void PositionalCorrectionUsesWorldToModelRotation()
	{
		var n = new LimbFrame( Vector3.UnitY, -Vector3.UnitX, Vector3.UnitZ );
		var m = new LimbFrame( -Vector3.UnitY, Vector3.UnitX, Vector3.UnitZ );
		var input = Input() with { ModelToWorld = new LimbRigidTransform( m, new Vector3( 40f ) ),
			WorldToModel = new LimbRigidTransform( n, new Vector3( -90f ) ) };
		var state = FrameState();
		state.CorrectionPosition = Vector3.UnitX;
		var frame = new GrindAttachmentFrameBuilder( MathProfile() ).Build( input, state );
		Assert.Equal( LimbRigidTransform.Identity.Rotation, frame.Rotation );
		Assert.Equal( Vector3.UnitY, frame.Translation );
	}

	[Fact]
	public void RotationOrderAppliesAuthoredAxisBeforeEdgeAxis()
	{
		var state = FrameState();
		state.AngularPosition = new Vector3( MathF.PI / 2f, 0f, MathF.PI / 2f );
		var frame = new GrindAttachmentFrameBuilder( MathProfile() ).Build( Input(), state );
		Near( Vector3.UnitZ, frame.Rotation.X );
	}

	[Fact]
	public void RollRotationUsesTheSuppliedAuthoredAxis()
	{
		var state = FrameState();
		state.AngularPosition = new Vector3( 0f, 0f, MathF.PI / 2f );
		var input = Input() with { AuthoredBoard = new LimbRigidTransform(
			new LimbFrame( Vector3.UnitY, Vector3.UnitZ, Vector3.UnitX ), Vector3.Zero ) };
		var frame = new GrindAttachmentFrameBuilder( MathProfile() ).Build( input, state );
		Near( Vector3.UnitZ, frame.Rotation.Y );
	}

	[Fact]
	public void DegenerateEdgeIsNotReplacedByAnInventedFrame()
	{
		var state = FrameState();
		state.EdgeFrame = new LimbFrame( Vector3.Zero, Vector3.Zero, Vector3.UnitY );
		var frame = new GrindAttachmentFrameBuilder( MathProfile() ).Build( Input(), state );
		Assert.False( float.IsFinite( frame.Rotation.X.X ) );
	}

	private static void Near( Vector3 expected, Vector3 actual ) => Assert.InRange( Vector3.Distance( expected, actual ), 0f, 1e-5f );

	private static GrindApproachState FrameState() => new() { EdgeFrame = new LimbFrame( -Vector3.UnitZ, Vector3.UnitY, Vector3.UnitX ) };

	private static GrindApproachState RetainedState() => new() { RequestValid = true, HasCandidate = true,
		SelectedCandidate = 6, PreviousCandidate = 4, CorrectionPosition = Vector3.UnitX,
		AngularPosition = Vector3.UnitY, CorrectionVelocity = Vector3.UnitZ, AngularVelocity = new Vector3( 2f ) };

	private static BoardAttachmentController Pending() => new() {
		Frame = LimbRigidTransform.Identity with { Translation = new Vector3( 3f, 4f, 5f ) },
		FrameRemaining = 7f, HeightRemaining = 9f, FrameRequested = false, HeightRequested = true };

	private static void AssertPending( BoardAttachmentController attachment )
	{
		Assert.Equal( new Vector3( 3f, 4f, 5f ), attachment.Frame.Translation );
		Assert.Equal( 7f, attachment.FrameRemaining );
		Assert.Equal( 9f, attachment.HeightRemaining );
		Assert.False( attachment.FrameRequested );
		Assert.True( attachment.HeightRequested );
	}

	private static GrindAttachmentInput Input() => new( true, false,
		new GrindApproachInput( LimbRigidTransform.Identity with { Translation = new Vector3( 0f, .5f, .3f ) },
			-Vector3.UnitY, Vector3.Zero, Vector3.UnitY, -Vector3.UnitX, Vector3.UnitX, 1f, true ),
		new LimbFrame(), LimbRigidTransform.Identity, LimbRigidTransform.Identity,
		LimbRigidTransform.Identity, LimbRigidTransform.Identity );

	private static GrindAttachmentController Create()
	{
		var math = MathProfile();
		var definitions = new GrindApproachCandidate[ 7 ];
		Array.Fill( definitions, new GrindApproachCandidate( 0f, 10f, 10f, 1f, 0 ) );
		var approach = new GrindApproachSolver( math,
			new GrindApproachSettings( 8, 0f, Vector3.Zero, 1e-6f, 1e-5f, 1f, 100f, 200f, .1f, .1f ),
			new[] { Vector3.Zero, Vector3.UnitZ, Vector3.Zero, Vector3.Zero, -Vector3.UnitZ }, definitions );
		var angular = new GrindAngularCorrector( math,
			new GrindAngularSettings( 1f, 1f, .5f, .9f, 1e-6f, 1e-4f, 2f * MathF.PI, 1f / (2f * MathF.PI) ), definitions );
		return new GrindAttachmentController( approach, angular, new GrindAttachmentFrameBuilder( math ) );
	}

	private static LimbSolverMath MathProfile() => new( new[] { -1f / 720f, 1f / 24f, -.5f },
		new[] { -1f / 5040f, 1f / 120f, -1f / 6f }, new float[ 8 ],
		2f / MathF.PI, MathF.PI / 2f, 0f, MathF.PI );
}
