using System.Numerics;
using SkateMotion.Core.Animation;
using SkateMotion.Core.Physics;
using Xunit;

namespace SboxSkateMotion.Tests;

public class BoardPlacementMotionTests
{
	[Fact]
	public void RangeBoundaryIsStrictAndRelativeToTheReference()
	{
		var reference = new Vector3( 2f, 3f, 4f );
		Assert.False( BoardPlacementMotion.IsBeyondRange( reference + new Vector3( 3f, 4f, 0f ), reference, 5f ) );
		Assert.True( BoardPlacementMotion.IsBeyondRange( reference + new Vector3( 3f, 4.1f, 0f ), reference, 5f ) );
		Assert.False( BoardPlacementMotion.IsBeyondRange( reference, reference, 0f ) );
	}

	[Fact]
	public void RangeUsesTheSquaredLimitWithoutChangingItsSignContract()
	{
		Assert.True( BoardPlacementMotion.IsBeyondRange( new Vector3( 6f, 0f, 0f ), Vector3.Zero, -5f ) );
		Assert.False( BoardPlacementMotion.IsBeyondRange( new Vector3( 5f, 0f, 0f ), Vector3.Zero, -5f ) );
	}

	[Fact]
	public void BeginCapturesTheBoardAndResetsAllMotionCachesAndClocks()
	{
		var board = LimbRigidTransform.Identity with { Translation = new Vector3( 2f, 3f, 4f ) };
		var state = new PhysicalBoardMotionState
		{
			Source = board, Target = board, Output = board,
			Elapsed = 9f, Duration = 8f, Progress = 7f, Weight = 6f
		};
		BoardPlacementMotion.Begin( state, board );
		Assert.Equal( board, state.Source );
		Assert.Equal( LimbRigidTransform.Identity, state.Target );
		Assert.Equal( LimbRigidTransform.Identity, state.Output );
		Assert.Equal( 0f, state.Elapsed );
		Assert.Equal( 0f, state.Duration );
		Assert.Equal( 0f, state.Progress );
		Assert.Equal( 0f, state.Weight );
	}

	[Fact]
	public void MovingReferencePreservesInitialOrientationAndDoesNotNormalizeDirection()
	{
		var board = new LimbRigidTransform(
			new LimbFrame( Vector3.UnitY, -Vector3.UnitX, Vector3.UnitZ ), new Vector3( 1f, 2f, 3f ) );
		var state = new PhysicalBoardMotionState();
		var motion = new BoardPlacementMotion( 4f, .125f );
		BoardPlacementMotion.Begin( state, board );
		motion.Update( state, new Vector3( 2f, 3f, 4f ), new Vector3( 0f, .5f, 0f ) );
		motion.Update( state, new Vector3( 3f, 4f, 5f ), new Vector3( 0f, -.5f, 0f ) );
		Assert.Equal( new Vector3( 3f, 2f, 5f ), state.Output.Translation );
		Assert.Equal( board.Rotation, state.Output.Rotation );
		Assert.Equal( board, state.Source );
		Assert.Equal( state.Target, state.Output );
		Assert.Equal( .25f, state.Elapsed );
		Assert.Equal( .5f, state.Progress );
		Assert.Equal( .5f, state.Weight );
	}
}
