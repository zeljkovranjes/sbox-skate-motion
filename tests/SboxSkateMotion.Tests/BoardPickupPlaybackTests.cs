using System;
using System.Numerics;
using SkateMotion.Core.Animation;
using SkateMotion.Core.Physics;
using Xunit;

namespace SboxSkateMotion.Tests;

public class BoardPickupPlaybackTests
{
	[Fact]
	public void OwnershipCompletesOnTheTickAfterMotionReachesTheAttachment()
	{
		var playback = Create();
		playback.Begin( At( 0f ), At( 16f ), Vector3.UnitX );
		playback.Advance( 1f, At( 16f ) );
		Assert.Equal( 1f, playback.Progress );
		Assert.Equal( At( 16f ), playback.Frame );
		Assert.False( playback.Complete );
		playback.Advance( .25f, At( 20f ) );
		Assert.True( playback.Complete );
		Assert.Equal( At( 16f ), playback.Frame );
	}

	[Fact]
	public void FixedTicksFollowTheMovingTargetAndRestartWithANewSource()
	{
		var playback = Create();
		playback.Begin( At( 4f ), At( 12f ), Vector3.UnitX );
		playback.Advance( .125f, At( 12f ) );
		Assert.Equal( At( 4f ), playback.Frame );
		playback.Advance( .125f, At( 12f ) );
		Assert.Equal( .25f, playback.Progress );
		playback.Advance( .5f, At( 20f ) );
		Assert.Equal( 14f, playback.Frame.Translation.X );
		playback.Advance( .5f, At( 20f ) );
		Assert.True( playback.Complete );
		playback.Begin( At( 8f ), At( 24f ), Vector3.UnitX );
		Assert.False( playback.Complete );
		Assert.Equal( 0f, playback.Progress );
		Assert.Equal( At( 8f ), playback.Frame );
	}

	private static LimbRigidTransform At( float x ) => LimbRigidTransform.Identity with
	{
		Translation = new Vector3( x, 0f, 0f )
	};

	private static BoardPickupPlayback Create()
	{
		var inputs = new[] { 0f, .5f, 1f, 2f, 3f, 4f, 5f, 6f };
		var duration = new BoardCorrectionCurve( inputs, new[] { 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f } );
		var weight = new BoardCorrectionCurve( inputs, new[] { 0f, .25f, 1f, 1f, 1f, 1f, 1f, 1f } );
		var math = new LimbSolverMath( new[] { -1f / 720f, 1f / 24f, -.5f },
			new[] { -1f / 5040f, 1f / 120f, -1f / 6f }, new float[ 8 ],
			2f / MathF.PI, MathF.PI / 2f, 0f, MathF.PI );
		var arc = new LimbArcTangent( new float[ 8 ], MathF.PI / 2f, MathF.PI );
		var initializer = new BoardRetrievalInitializer( duration, .001f, 100f, 5f, .1f );
		var motion = new BoardRetrievalMotion( weight, new LimbPoseBlender( math, arc, .00001f, .1f ), .25f );
		return new( initializer, motion, .25f );
	}
}
