using System;
using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class BoardAttachmentSequenceTests
{
	[Theory]
	[InlineData( false, false, false )]
	[InlineData( false, true, false )]
	[InlineData( true, false, true )]
	[InlineData( true, true, true )]
	public void DispatchBypassPreservesPublishedGrindState( bool descending, bool suppress, bool retained )
	{
		var sequence = Create( out _, out _ );
		var state = new GrindApproachState { RequestValid = true, HasCandidate = true };
		var board = LimbRigidTransform.Identity;
		sequence.Update( Input() with { DescendingMode = descending, SuppressDescending = suppress }, state, ref board, Targets() );
		Assert.Equal( retained, state.RequestValid );
		Assert.True( state.HasCandidate );
	}

	[Fact]
	public void DescendingRequestReplacesHandFrameBeforeApplyingItOnce()
	{
		var sequence = Create( out var attachment, out _ );
		var input = Input() with { AttachFromHand = true, Hand = At( 9f, 8f, 7f ), DescendingMode = true };
		input = input with { Descending = input.Descending with { VerticalMotion = -1f, WorldDisplacement = Vector3.UnitX } };
		var board = LimbRigidTransform.Identity;
		var targets = Targets();
		sequence.Update( input, new GrindApproachState(), ref board, targets );
		Assert.Equal( Vector3.UnitX, board.Translation );
		Assert.Equal( board, targets[ 0 ] );
		Assert.Equal( 15f, attachment.FrameRemaining );
		Assert.False( attachment.FrameRequested );
		Assert.False( attachment.HeightRequested );
	}

	[Fact]
	public void VerticalRequestReplacesOnlyHandHeightAndKeepsItsOwnRetention()
	{
		var sequence = Create( out var attachment, out var vertical );
		vertical.Active = true;
		vertical.Mode = 3;
		var input = Input() with { AttachFromHand = true, Hand = At( 2f, 3f, 4f ), DescendingMode = true, SuppressDescending = true };
		input = input with { Vertical = input.Vertical with { Category = 1, CurrentHeight = 5f, SecondaryActive = true, ForceRequest = true } };
		var board = LimbRigidTransform.Identity;
		sequence.Update( input, new GrindApproachState(), ref board, Targets() );
		Assert.Equal( new Vector3( 2f, 4f, 4f ), board.Translation );
		Assert.Equal( 15f, attachment.FrameRemaining );
		Assert.Equal( 6f, attachment.HeightRemaining );
		Assert.False( vertical.Active );
	}

	[Fact]
	public void ReleaseDecaysPriorRequestAgainstFreshlyGatheredPose()
	{
		var sequence = Create( out var attachment, out _ );
		var state = new GrindApproachState { RequestValid = true };
		var input = Input() with { AttachFromHand = true, Hand = At( 15f, 15f, 15f ), DescendingMode = true, SuppressDescending = true };
		var board = LimbRigidTransform.Identity;
		sequence.Update( input, state, ref board, Targets() );
		Assert.Equal( new Vector3( 15f ), board.Translation );
		board = At( 1f, 0f, 0f );
		sequence.Update( input with { AttachFromHand = false }, state, ref board, Targets() );
		var weight = (14f / 15f) * (14f / 15f);
		Assert.Equal( new Vector3( 15f * weight ) + Vector3.UnitX, board.Translation );
		Assert.Equal( 14f, attachment.FrameRemaining );
		Assert.Equal( 14f, attachment.HeightRemaining );
		Assert.True( state.RequestValid );
	}

	[Fact]
	public void DescendingUsesTheCurrentBoardInsteadOfAStaleInputCopy()
	{
		var sequence = Create( out _, out _ );
		var input = Input() with { DescendingMode = true };
		input = input with { Descending = input.Descending with { VerticalMotion = -1f,
			Board = At( 100f, 100f, 100f ), TargetWorld = At( 0f, 5f, 0f ) } };
		var board = At( 2f, 3f, 4f );
		sequence.Update( input, new GrindApproachState(), ref board, Targets() );
		Assert.Equal( new Vector3( 2f, 1f, 4f ), board.Translation );
	}

	[Fact]
	public void InvalidTargetCountDoesNotPartiallySubmitRequests()
	{
		var sequence = Create( out var attachment, out _ );
		var board = LimbRigidTransform.Identity;
		Assert.Throws<ArgumentException>( () => sequence.Update( Input() with { AttachFromHand = true },
			new GrindApproachState(), ref board, new LimbRigidTransform[ 3 ] ) );
		Assert.Equal( 0f, attachment.FrameRemaining );
	}

	private static BoardAttachmentInput Input() => new( false, LimbRigidTransform.Identity, LimbRigidTransform.Identity,
		false, false, new GrindAttachmentInput(),
		new DescendingAttachmentInput( 0f, false, LimbRigidTransform.Identity, LimbRigidTransform.Identity,
			LimbRigidTransform.Identity, Vector3.Zero, 0f ), new VerticalAttachmentInput() );

	private static LimbRigidTransform At( float x, float y, float z ) => LimbRigidTransform.Identity with { Translation = new Vector3( x, y, z ) };
	private static LimbRigidTransform[] Targets() => new[] { LimbRigidTransform.Identity, LimbRigidTransform.Identity, LimbRigidTransform.Identity, LimbRigidTransform.Identity };

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
