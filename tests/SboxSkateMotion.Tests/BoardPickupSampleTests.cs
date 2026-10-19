using System;
using System.Numerics;
using SkateMotion.Core.Animation;
using SkateMotion.Core.Physics;
using Xunit;

namespace SboxSkateMotion.Tests;

public class BoardPickupSampleTests
{
	[Fact]
	public void SampleMatchesTheLastFrameForTheSameTarget()
	{
		var playback = Create();
		playback.Begin( At( 4f ), At( 12f ), Vector3.UnitX );
		Assert.Equal( At( 4f ), playback.Sample( At( 12f ) ) );
		playback.Advance( .25f, At( 12f ) );
		playback.Advance( .25f, At( 12f ) );
		Assert.Equal( playback.Frame, playback.Sample( At( 12f ) ) );
	}

	[Fact]
	public void SampleBlendsTowardTheCurrentTargetWithoutAdvancing()
	{
		var playback = Create();
		playback.Begin( At( 4f ), At( 12f ), Vector3.UnitX );
		playback.Advance( .25f, At( 12f ) );
		playback.Advance( .25f, At( 12f ) );
		var progress = playback.Progress;
		var frame = playback.Frame;
		// Progress 0.5 weighs the target by 0.25: a target moved to 20 is reached a quarter of the way from 4.
		Assert.Equal( 8f, playback.Sample( At( 20f ) ).Translation.X, 5 );
		Assert.Equal( progress, playback.Progress );
		Assert.Equal( frame, playback.Frame );
	}

	[Fact]
	public void SampleFollowsTheTargetOnceRetrievalHasReachedIt()
	{
		var playback = Create();
		playback.Begin( At( 0f ), At( 16f ), Vector3.UnitX );
		playback.Advance( 1f, At( 16f ) );
		Assert.Equal( At( 18f ), playback.Sample( At( 18f ) ) );
		playback.Advance( .25f, At( 16f ) );
		Assert.True( playback.Complete );
		Assert.Equal( At( 22f ), playback.Sample( At( 22f ) ) );
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
