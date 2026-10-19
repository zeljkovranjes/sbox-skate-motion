using System.Numerics;
using SkateMotion.Core.Animation;
using SkateMotion.Core.Physics;
using Xunit;

namespace SboxSkateMotion.Tests;

public class BoardHeldFrameTests
{
	[Fact]
	public void CoincidentFramesProduceIdentityAndZeroAnchors()
	{
		var pose = LimbRigidTransform.Identity with { Translation = new Vector3( 4f, -2f, 8f ) };
		var result = BoardHeldFrame.Create( pose, pose );
		Assert.Equal( Vector4.UnitW, result.ChildRotation );
		Assert.Equal( Vector3.Zero, result.ChildPosition );
		Assert.Equal( Vector4.UnitW, result.BodyRotation );
		Assert.Equal( Vector3.Zero, result.BodyPosition );
	}

	[Fact]
	public void AnchorUsesTheBoardAxesInsteadOfWorldSpace()
	{
		var board = new LimbRigidTransform(
			new LimbFrame( Vector3.UnitY, -Vector3.UnitX, Vector3.UnitZ ), new Vector3( 2f, 3f, 4f ) );
		var hand = board with { Translation = new Vector3( 5f, 7f, 9f ) };
		var result = BoardHeldFrame.Create( board, hand );
		Assert.Equal( new Vector3( 4f, -3f, 5f ), result.BodyPosition );
		Assert.Equal( Vector4.UnitW, result.BodyRotation );
		Assert.Equal( Vector3.Zero, result.ChildPosition );
	}

	[Fact]
	public void RelativeHalfTurnPreservesTheMajorQuaternionAxis()
	{
		var hand = new LimbRigidTransform(
			new LimbFrame( Vector3.UnitX, -Vector3.UnitY, -Vector3.UnitZ ), new Vector3( 1f, 2f, 3f ) );
		var result = BoardHeldFrame.Create( LimbRigidTransform.Identity, hand );
		Assert.Equal( Vector4.UnitX, result.BodyRotation );
		Assert.Equal( hand.Translation, result.BodyPosition );
		Assert.Equal( Vector4.UnitW, result.ChildRotation );
	}
}
