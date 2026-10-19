using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class BoardHandlingControllerTests
{
	private static BoardHandlingInput Retrieve => new( false, false, false, true, false, false, true, 0f, 0f, false );
	private static BoardHandlingPlayback Playing => new( true, 1f, 0f );

	[Theory]
	[InlineData( true, false, false )]
	[InlineData( false, true, false )]
	[InlineData( false, false, true )]
	public void RetrievalWaitsForPhysicalEligibility( bool grabbing, bool holding, bool blocked )
	{
		var controller = new BoardHandlingController();
		var input = Retrieve with { GrabbingObject = grabbing, HoldingBoard = holding, RetrievalBlocked = blocked };
		var result = controller.Update( input, Playing );
		Assert.Equal( BoardHandlingPhase.Idle, controller.Phase );
		Assert.Equal( BoardHandlingCommand.None, result.Command );
		Assert.False( result.Retrieving );
	}

	[Fact]
	public void DropTakesPriorityOverThrowAndRetrieve()
	{
		var controller = new BoardHandlingController();
		var input = Retrieve with { HoldingBoard = true, DropRequested = true, ThrowRequested = true };
		var result = controller.Update( input, Playing );
		Assert.Equal( BoardHandlingMotion.Drop, result.Motion );
		Assert.True( result.Dropping );
		Assert.False( result.RetrieveRequested );
		Assert.Equal( BoardHandlingPhase.DropPlaying, controller.Phase );
	}

	[Theory]
	[InlineData( 0.251f, 0, false )]
	[InlineData( 0.25f, 2, true )]
	[InlineData( 0.001f, 2, true )]
	[InlineData( 0f, 0, false )]
	public void IntoUsesChannelRemainingTime( float remaining, int command, bool request )
	{
		var controller = new BoardHandlingController();
		var first = controller.Update( Retrieve, Playing );
		Assert.Equal( BoardHandlingMotion.FrontInto, first.Motion );
		Assert.Equal( 0.25f, first.BlendSeconds );
		var result = controller.Update( Retrieve, new( true, remaining, 0f ) );
		Assert.Equal( (BoardHandlingCommand)command, result.Command );
		Assert.Equal( request, result.RetrieveRequested );
		Assert.True( result.Retrieving );
		if ( remaining == 0f ) Assert.Equal( BoardHandlingPhase.Idle, controller.Phase );
	}

	[Fact]
	public void CycleWaitsForPossessionThenUsesOutPlayback()
	{
		var controller = new BoardHandlingController();
		controller.Update( Retrieve, Playing );
		controller.Update( Retrieve, new( true, 0.25f, 0f ) );
		Assert.Equal( BoardHandlingPhase.RetrieveCycle, controller.Phase );
		Assert.Equal( BoardHandlingCommand.None, controller.Update( Retrieve, Playing ).Command );
		var held = Retrieve with { HoldingBoard = true };
		var exit = controller.Update( held, Playing );
		Assert.Equal( BoardHandlingMotion.FrontOut, exit.Motion );
		Assert.False( exit.RetrieveRequested );
		Assert.Equal( BoardHandlingCommand.None, controller.Update( held, new( true, 1f, 1f / 6f ) ).Command );
		var end = controller.Update( held, new( true, 1f, 0.166667f ) );
		Assert.Equal( BoardHandlingCommand.Stop, end.Command );
		Assert.Equal( 1f / 6f, end.BlendSeconds );
		Assert.True( end.Retrieving );
		Assert.True( end.PublishOrientation );
		Assert.Equal( BoardHandlingPhase.Idle, controller.Phase );
	}

	[Fact]
	public void RetrievalCancellationPrecedesPossession()
	{
		var controller = new BoardHandlingController();
		controller.Update( Retrieve, Playing );
		controller.Update( Retrieve, new( true, 0.25f, 0f ) );
		var result = controller.Update( Retrieve with { RetrievalActive = false, HoldingBoard = true }, Playing );
		Assert.Equal( BoardHandlingCommand.Stop, result.Command );
		Assert.Equal( BoardHandlingMotion.None, result.Motion );
	}

	[Theory]
	[InlineData( 0.5f, 0 )]
	[InlineData( 0.500001f, 3 )]
	public void DropStopsOnlyAfterItsElapsedThreshold( float elapsed, int command )
	{
		var controller = new BoardHandlingController();
		var input = Retrieve with { HoldingBoard = true, ThrowRequested = true };
		Assert.Equal( BoardHandlingMotion.Throw, controller.Update( input, Playing ).Motion );
		Assert.Equal( (BoardHandlingCommand)command, controller.Update( input, new( true, 1f, elapsed ) ).Command );
	}

	[Theory]
	[InlineData( false, -5f )]
	[InlineData( true, 5f )]
	public void YawChangesFiveDegreesPerUpdate( bool mirrored, float expected )
	{
		var controller = new BoardHandlingController();
		controller.Update( Retrieve with { Mirrored = mirrored }, Playing );
		var result = controller.Update( Retrieve with { Mirrored = mirrored, YawRadians = 1f }, Playing );
		Assert.Equal( expected, result.YawDegrees );
	}

	[Fact]
	public void BackSelectionAndResetUseFreshOrientation()
	{
		var controller = new BoardHandlingController();
		var result = controller.Update( Retrieve with { YawRadians = 3f }, Playing );
		Assert.Equal( BoardHandlingMotion.BackInto, result.Motion );
		Assert.Equal( 180f, result.YawDegrees );
		controller.Reset();
		result = controller.Update( Retrieve, Playing );
		Assert.Equal( BoardHandlingMotion.FrontInto, result.Motion );
		Assert.Equal( 0f, result.YawDegrees );
	}
}
