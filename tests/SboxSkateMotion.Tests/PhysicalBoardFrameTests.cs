using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class PhysicalBoardFrameTests
{
	[Fact]
	public void DisabledCorrectionStillWritesBodyAndKeepsRequestedScaleInCache()
	{
		var source = new LimbRigidTransform( new( Vector3.UnitX * 2f, Vector3.UnitY * 3f, Vector3.UnitZ * 4f ), new( 4, 5, 6 ) );
		var target = new PhysicalBodyBinding { Body = new() { Velocity = new( 7, 8, 9 ) } };
		var orientation = new Vector4( .02f, 0f, 0f, .9998f );
		var result = PhysicalBoardFrame.Update( Correction(), target, source, orientation, 0, false );
		Assert.Equal( source, result.Frame );
		Assert.Equal( source, target.CachedPose );
		Assert.Equal( source.Translation, target.Body.Pose.Translation );
		Assert.InRange( target.Body.Pose.Rotation.X.Length(), .99999f, 1.00001f );
		Assert.Equal( orientation, result.Orientation );
		Assert.Equal( new Vector3( 7, 8, 9 ), target.Body.Velocity );
	}

	[Fact]
	public void ReleaseStillWritesThePhysicalTargetWithItsLocalOffset()
	{
		var source = LimbRigidTransform.Identity with { Translation = new( 4, 5, 6 ) };
		var target = new PhysicalBodyBinding
		{
			Body = new(),
			LocalOffset = LimbRigidTransform.Identity with { Translation = new( 1, 2, 3 ) }
		};
		var orientation = new Vector4( .02f, 0f, 0f, .9998f );
		var result = PhysicalBoardFrame.Update( Correction(), target, source, orientation, 7, false );
		Assert.Equal( 0, result.CorrectionFlag );
		Assert.Equal( orientation, result.Orientation );
		Assert.Equal( new Vector3( 3, 3, 3 ), target.Body.Pose.Translation );
		Assert.Equal( source, target.CachedPose );
	}

	[Fact]
	public void ReleasedFrameContinuesFollowingSourceWithoutChangingRetainedOrientation()
	{
		var correction = Correction();
		var target = new PhysicalBodyBinding();
		var source = LimbRigidTransform.Identity;
		var first = PhysicalBoardFrame.Update( correction, target, source, new( .02f, 0f, 0f, .9998f ), 1, false );
		source = source with { Translation = new( 10, 20, 30 ) };
		var second = PhysicalBoardFrame.Update( correction, target, source, first.Orientation, first.CorrectionFlag, true );
		Assert.Equal( source, target.CachedPose );
		Assert.Equal( source, second.Frame );
		Assert.Equal( first.Orientation, second.Orientation );
		Assert.Equal( 0, second.CorrectionFlag );
	}

	private static BoardFrameCorrection Correction()
	{
		var math = new LimbSolverMath( [ -1f / 720f, 1f / 24f, -.5f ],
			[ -1f / 5040f, 1f / 120f, -1f / 6f ], new float[ 8 ],
			2f / MathF.PI, MathF.PI / 2f, 0f, MathF.PI );
		var curve = new BoardCorrectionCurve( [ 0f, .5f, 1f, 1.5f, 2f, 2.5f, 3f, 3.5f ],
			[ 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f ] );
		return new( math, curve, curve, new( 2f * MathF.PI, 1f / (2f * MathF.PI), .95f, .996f, MathF.Sqrt( 2f ) ) );
	}
}
