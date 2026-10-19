using System;
using System.Numerics;
using SkateMotion.Core.Animation;
using SkateMotion.Core.Physics;
using Xunit;

namespace SboxSkateMotion.Tests;

public class BoardRetrievalMotionTests
{
	[Fact]
	public void AuthoredCurveControlsPositionSeparatelyFromCompletionProgress()
	{
		var result = Create().Update( At( 0f ), At( 16f ), 1f, .25f );
		Assert.Equal( .5f, result.Elapsed );
		Assert.Equal( .5f, result.Progress );
		Assert.Equal( .25f, result.Weight );
		Assert.Equal( 4f, result.Frame.Translation.X );
	}

	[Fact]
	public void EachTickUsesTheMovingAttachmentAndPreservesTheCapturedSource()
	{
		var motion = Create();
		var first = motion.Update( At( 4f ), At( 12f ), 1f, .25f );
		var second = motion.Update( At( 4f ), At( 20f ), 1f, first.Elapsed );
		Assert.Equal( 6f, first.Frame.Translation.X );
		Assert.Equal( .75f, second.Progress );
		Assert.Equal( .625f, second.Weight );
		Assert.Equal( 14f, second.Frame.Translation.X );
	}

	[Theory]
	[InlineData( 0f )]
	[InlineData( .1f )]
	[InlineData( float.NaN )]
	public void CompleteOrUnorderedProgressCopiesTheCurrentAttachment( float duration )
	{
		var result = Create().Update( At( 4f ), At( 20f ), duration, 0f );
		Assert.Equal( 1f, result.Progress );
		Assert.Equal( 1f, result.Weight );
		Assert.Equal( At( 20f ), result.Frame );
	}

	[Fact]
	public void NegativeProgressRemainsVisibleWhileTheCurveSuppliesItsFirstValue()
	{
		var result = Create().Update( At( 4f ), At( 20f ), 1f, -.75f );
		Assert.Equal( -.5f, result.Progress );
		Assert.Equal( 0f, result.Weight );
		Assert.Equal( At( 4f ), result.Frame );
	}

	private static LimbRigidTransform At( float x ) => LimbRigidTransform.Identity with
	{
		Translation = new Vector3( x, 0f, 0f )
	};

	private static BoardRetrievalMotion Create()
	{
		var curve = new BoardCorrectionCurve( new[] { 0f, .5f, 1f, 2f, 3f, 4f, 5f, 6f },
			new[] { 0f, .25f, 1f, 1f, 1f, 1f, 1f, 1f } );
		var math = new LimbSolverMath( new[] { -1f / 720f, 1f / 24f, -.5f },
			new[] { -1f / 5040f, 1f / 120f, -1f / 6f }, new float[ 8 ],
			2f / MathF.PI, MathF.PI / 2f, 0f, MathF.PI );
		var arc = new LimbArcTangent( new float[ 8 ], MathF.PI / 2f, MathF.PI );
		return new( curve, new LimbPoseBlender( math, arc, .00001f, .1f ), .25f );
	}
}
