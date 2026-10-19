using System;
using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class BoardAttachmentControllerTests
{
	[Fact]
	public void FreshControllerHasNoPendingCorrection()
	{
		var controller = new BoardAttachmentController();
		var board = At( 2f, 3f, 4f );
		controller.Update( ref board, Targets() );
		Assert.Equal( At( 2f, 3f, 4f ), board );
		Assert.Equal( LimbRigidTransform.Identity, controller.Frame );
		Assert.Equal( 0f, controller.FrameRemaining );
		Assert.Equal( 0f, controller.HeightRemaining );
		Assert.False( controller.FrameRequested );
		Assert.False( controller.HeightRequested );
	}

	[Fact]
	public void HandRequestAlignsTheHelperToTheHand()
	{
		var controller = new BoardAttachmentController();
		var hand = new LimbRigidTransform( new LimbFrame( Vector3.UnitY, -Vector3.UnitX, Vector3.UnitZ ), new Vector3( 4f, 5f, 6f ) );
		var helper = At( 2f, 3f, 1f );
		controller.RequestFromHand( hand, helper );
		Assert.Equal( hand, controller.Frame.Compose( helper ) );
		Assert.Equal( new Vector3( 7f, 3f, 5f ), controller.Frame.Translation );
		Assert.Equal( 15f, controller.FrameRemaining );
		Assert.Equal( 15f, controller.HeightRemaining );
		Assert.True( controller.FrameRequested );
		Assert.True( controller.HeightRequested );
	}

	[Fact]
	public void FrameRequestLeavesTheHeightCountdownAlone()
	{
		var controller = new BoardAttachmentController { HeightRemaining = 4f };
		controller.RequestFrame( At( 4f, 5f, 6f ) );
		Assert.Equal( At( 4f, 5f, 6f ), controller.Frame );
		Assert.Equal( 15f, controller.FrameRemaining );
		Assert.Equal( 4f, controller.HeightRemaining );
		Assert.False( controller.HeightRequested );
	}

	[Fact]
	public void HeightRequestChangesOnlyRigYAndItsOwnState()
	{
		var frame = new LimbRigidTransform( new LimbFrame( Vector3.UnitY, -Vector3.UnitX, Vector3.UnitZ ), new Vector3( 2f, 3f, 4f ) );
		var controller = new BoardAttachmentController { Frame = frame, FrameRemaining = 7f };
		controller.RequestHeight( 8f, 4f );
		Assert.Equal( frame.Rotation, controller.Frame.Rotation );
		Assert.Equal( new Vector3( 2f, 8f, 4f ), controller.Frame.Translation );
		Assert.Equal( 7f, controller.FrameRemaining );
		Assert.Equal( 4f, controller.HeightRemaining );
		Assert.False( controller.FrameRequested );
		Assert.True( controller.HeightRequested );
	}

	[Fact]
	public void FreshRequestAppliesWithoutConsumingTheCountdown()
	{
		var controller = new BoardAttachmentController();
		controller.RequestFromHand( At( 2f, 3f, 4f ), LimbRigidTransform.Identity );
		var board = LimbRigidTransform.Identity;
		controller.Update( ref board, Targets() );
		Assert.Equal( At( 2f, 3f, 4f ), board );
		Assert.Equal( 15f, controller.FrameRemaining );
		Assert.Equal( 15f, controller.HeightRemaining );
		Assert.False( controller.FrameRequested );
		Assert.False( controller.HeightRequested );
	}

	[Fact]
	public void FrameDecayPreservesVerticalOffset()
	{
		var controller = new BoardAttachmentController { Frame = At( 8f, 12f, 16f ), FrameRemaining = 2f };
		var board = LimbRigidTransform.Identity;
		controller.Update( ref board, Targets() );
		Assert.Equal( new Vector3( 2f, 12f, 4f ), board.Translation );
		Assert.Equal( 1f, controller.FrameRemaining );
		Assert.Equal( 0f, controller.HeightRemaining );
	}

	[Fact]
	public void HeightDecayPreservesHorizontalOffsetAndOrientation()
	{
		var frame = new LimbRigidTransform( new LimbFrame( Vector3.UnitY, -Vector3.UnitX, Vector3.UnitZ ), new Vector3( 8f, 12f, 16f ) );
		var controller = new BoardAttachmentController { Frame = frame, HeightRemaining = 2f };
		var board = LimbRigidTransform.Identity;
		controller.Update( ref board, Targets() );
		Assert.Equal( new Vector3( 8f, 3f, 16f ), board.Translation );
		Assert.Equal( frame.Rotation, board.Rotation );
		Assert.Equal( 1f, controller.HeightRemaining );
		Assert.Equal( 0f, controller.FrameRemaining );
	}

	[Fact]
	public void SameCorrectionPrecedesBoardAndAllFourTargets()
	{
		var controller = new BoardAttachmentController();
		controller.RequestFrame( new LimbRigidTransform( new LimbFrame( Vector3.UnitY, -Vector3.UnitX, Vector3.UnitZ ), new Vector3( 2f, 3f, 4f ) ) );
		var board = At( 1f, 0f, 0f );
		var targets = new[] { At( 1f, 0f, 0f ), At( 2f, 0f, 0f ), At( 3f, 0f, 0f ), At( 4f, 0f, 0f ) };
		controller.Update( ref board, targets );
		Assert.Equal( new Vector3( 2f, 4f, 4f ), board.Translation );
		for ( var i = 0; i < 4; i++ )
			Assert.Equal( new Vector3( 2f, 4f + i, 4f ), targets[ i ].Translation );
	}

	[Fact]
	public void BothCountdownsFinishAfterFifteenUnrequestedUpdates()
	{
		var controller = new BoardAttachmentController();
		controller.RequestFromHand( At( 8f, 12f, 16f ), LimbRigidTransform.Identity );
		for ( var i = 0; i <= 15; i++ )
		{
			var board = LimbRigidTransform.Identity;
			controller.Update( ref board, Targets() );
		}
		Assert.Equal( LimbRigidTransform.Identity, controller.Frame );
		Assert.Equal( 0f, controller.FrameRemaining );
		Assert.Equal( 0f, controller.HeightRemaining );
		var untouched = At( 3f, 4f, 5f );
		controller.Update( ref untouched, Targets() );
		Assert.Equal( At( 3f, 4f, 5f ), untouched );
	}

	[Fact]
	public void RefreshingTheRequestRestartsItsDecay()
	{
		var controller = new BoardAttachmentController { Frame = At( 8f, 12f, 16f ), FrameRemaining = 2f, HeightRemaining = 2f };
		var board = LimbRigidTransform.Identity;
		controller.Update( ref board, Targets() );
		controller.RequestFromHand( At( 1f, 2f, 3f ), LimbRigidTransform.Identity );
		board = LimbRigidTransform.Identity;
		controller.Update( ref board, Targets() );
		Assert.Equal( At( 1f, 2f, 3f ), board );
		Assert.Equal( 15f, controller.FrameRemaining );
		Assert.Equal( 15f, controller.HeightRemaining );
	}

	[Fact]
	public void ExpiredCountdownsDoNotApplyRetainedFrame()
	{
		var controller = new BoardAttachmentController { Frame = At( 8f, 12f, 16f ), FrameRemaining = -2f, HeightRemaining = -1f, FrameRequested = true, HeightRequested = true };
		var board = LimbRigidTransform.Identity;
		var targets = Targets();
		controller.Update( ref board, targets );
		Assert.Equal( LimbRigidTransform.Identity, board );
		Assert.All( targets, value => Assert.Equal( LimbRigidTransform.Identity, value ) );
		Assert.Equal( At( 8f, 12f, 16f ), controller.Frame );
		Assert.False( controller.FrameRequested );
		Assert.False( controller.HeightRequested );
	}

	[Fact]
	public void FractionalCountdownIsNotClamped()
	{
		var controller = new BoardAttachmentController { Frame = At( 8f, 12f, 16f ), FrameRemaining = .5f, HeightRemaining = .5f };
		var board = LimbRigidTransform.Identity;
		controller.Update( ref board, Targets() );
		Assert.Equal( At( 8f, 12f, 16f ), board );
		Assert.Equal( -.5f, controller.FrameRemaining );
		Assert.Equal( -.5f, controller.HeightRemaining );
	}

	[Fact]
	public void RejectsWrongTargetCountBeforeChangingState()
	{
		var controller = new BoardAttachmentController { FrameRemaining = 2f };
		var board = LimbRigidTransform.Identity;
		Assert.Throws<ArgumentException>( () => controller.Update( ref board, new LimbRigidTransform[ 3 ] ) );
		Assert.Equal( 2f, controller.FrameRemaining );
	}

	private static LimbRigidTransform At( float x, float y, float z ) => LimbRigidTransform.Identity with { Translation = new Vector3( x, y, z ) };
	private static LimbRigidTransform[] Targets() => new[] { LimbRigidTransform.Identity, LimbRigidTransform.Identity, LimbRigidTransform.Identity, LimbRigidTransform.Identity };
}
