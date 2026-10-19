using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class BoardFrameCorrectionTests
{
	[Fact]
	public void ReleasePreservesFrameAndRetainedOrientation()
	{
		var frame = LimbRigidTransform.Identity with { Translation = new( 3, 4, 5 ) };
		var orientation = new Vector4( .02f, 0f, 0f, .9998f );
		var result = Create( .95f, .1f ).Update( frame, orientation, 7, false );
		Assert.Equal( frame, result.Frame );
		Assert.Equal( orientation, result.Orientation );
		Assert.Equal( 0, result.CorrectionFlag );
	}

	[Fact]
	public void SelectedCurveControlsReleaseWithoutChangingTranslation()
	{
		var solver = Create( 1f, .25f );
		var frame = LimbRigidTransform.Identity with { Translation = new( 3, 4, 5 ) };
		var orientation = new Vector4( .02f, 0f, 0f, .9998f );
		var released = solver.Update( frame, orientation, 7, false );
		var retained = solver.Update( frame, orientation, 7, true );
		Assert.Equal( 0, released.CorrectionFlag );
		Assert.Equal( 7, retained.CorrectionFlag );
		Assert.Equal( frame.Translation, retained.Frame.Translation );
		Assert.InRange( retained.Orientation.X, 0f, orientation.X );
		Assert.InRange( retained.Orientation.Length(), .99999f, 1.00001f );
	}

	[Fact]
	public void OppositeQuaternionSignsProduceTheSameCorrection()
	{
		var solver = Create( .5f, .5f );
		var orientation = new Vector4( .02f, 0f, 0f, .9998f );
		var positive = solver.Update( LimbRigidTransform.Identity, orientation, 1, false );
		var negative = solver.Update( LimbRigidTransform.Identity, -orientation, 1, false );
		Assert.Equal( positive, negative );
	}

	[Fact]
	public void CurveUsesLastDuplicateAtAnExactInteriorKnot()
	{
		var curve = new BoardCorrectionCurve( [ 0f, 1f, 1f, 1f, 2f, 3f, 4f, 5f ],
			[ 0f, .1f, .2f, .3f, .4f, .5f, .6f, .7f ] );
		Assert.Equal( .3f, curve.Evaluate( 1f ) );
		Assert.Equal( 0f, curve.Evaluate( -1f ) );
		Assert.Equal( .7f, curve.Evaluate( 6f ) );
	}

	[Fact]
	public void CurveOwnsItsInputsAndRejectsUnorderedKeys()
	{
		float[] keys = [ 0f, 1f, 2f, 3f, 4f, 5f, 6f, 7f ];
		float[] values = [ 0f, .1f, .2f, .3f, .4f, .5f, .6f, .7f ];
		var curve = new BoardCorrectionCurve( keys, values );
		values[ 0 ] = 100f;
		keys[ 1 ] = -1f;
		Assert.Equal( 0f, curve.Evaluate( 0f ) );
		Assert.Throws<ArgumentException>( () => new BoardCorrectionCurve( keys, values ) );
	}

	private static BoardFrameCorrection Create( float weight, float alternateWeight )
	{
		var math = new LimbSolverMath( [ -1f / 720f, 1f / 24f, -.5f ],
			[ -1f / 5040f, 1f / 120f, -1f / 6f ], new float[ 8 ],
			2f / MathF.PI, MathF.PI / 2f, 0f, MathF.PI );
		var values = new float[ 8 ];
		var alternateValues = new float[ 8 ];
		Array.Fill( values, weight );
		Array.Fill( alternateValues, alternateWeight );
		float[] keys = [ 0f, .5f, 1f, 1.5f, 2f, 2.5f, 3f, 3.5f ];
		return new BoardFrameCorrection( math, new( keys, values ), new( keys, alternateValues ),
			new( 2f * MathF.PI, 1f / (2f * MathF.PI), .95f, .996f, MathF.Sqrt( 2f ) ) );
	}
}
