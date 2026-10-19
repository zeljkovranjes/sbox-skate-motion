using System;
using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class LimbPoseBlenderTests
{
	private static readonly LimbRigidTransform Identity = LimbRigidTransform.Identity;
	private static readonly LimbRigidTransform QuarterTurn = new(
		new LimbFrame( Vector3.UnitY, -Vector3.UnitX, Vector3.UnitZ ), new Vector3( 4f, 2f, -2f ) );

	[Theory]
	[InlineData( 1f )]
	[InlineData( 2f )]
	public void CompletedBlendCopiesTargetWithoutNormalization( float weight )
	{
		var target = QuarterTurn with { Rotation = new LimbFrame( Vector3.UnitX * 2f, Vector3.UnitY, Vector3.UnitZ ) };
		Assert.Equal( new LimbPoseBlendResult( target, 0f ), Create().Blend( Identity, target, weight ) );
	}

	[Theory]
	[InlineData( 0f )]
	[InlineData( -1f )]
	public void UnstartedBlendKeepsSourceAndReportsFullAngle( float weight )
	{
		var result = Create().Blend( Identity, QuarterTurn, weight );
		Assert.Equal( Identity, result.Transform );
		Assert.Equal( MathF.PI / 2f, result.RemainingAngle );
	}

	[Fact]
	public void HalfWeightSharesRotationTranslationAndRemainingAngle()
	{
		var result = Create().Blend( Identity, QuarterTurn, .5f );
		var angle = Math.PI / 4;
		// The configured Taylor profile reduces this angle into the next quadrant.
		var sine = (float)(angle - Math.Pow( angle, 3 ) / 6 + Math.Pow( angle, 5 ) / 120 - Math.Pow( angle, 7 ) / 5040);
		var cosine = (float)(1 - Math.Pow( angle, 2 ) / 2 + Math.Pow( angle, 4 ) / 24 - Math.Pow( angle, 6 ) / 720);
		Near( new Vector3( sine, cosine, 0f ), result.Transform.Rotation.X );
		Near( new Vector3( -cosine, sine, 0f ), result.Transform.Rotation.Y );
		Assert.Equal( new Vector3( 2f, 1f, -1f ), result.Transform.Translation );
		Assert.Equal( MathF.PI / 4f, result.RemainingAngle );
	}

	[Theory]
	[InlineData( 0 )]
	[InlineData( 1 )]
	[InlineData( 2 )]
	public void ExactHalfTurnRecoversTheAxisWhenSkewIsZero( int axis )
	{
		var frame = new LimbFrame( Vector3.UnitX * (axis == 0 ? 1f : -1f),
			Vector3.UnitY * (axis == 1 ? 1f : -1f), Vector3.UnitZ * (axis == 2 ? 1f : -1f) );
		var result = Create().Blend( Identity, new LimbRigidTransform( frame, Vector3.Zero ), .5f );
		var vector = axis == 0 ? result.Transform.Rotation.Y : axis == 1 ? result.Transform.Rotation.Z : result.Transform.Rotation.X;
		Near( axis == 0 ? Vector3.UnitZ : axis == 1 ? Vector3.UnitX : Vector3.UnitY, vector );
		Assert.Equal( MathF.PI / 2f, result.RemainingAngle );
	}

	[Fact]
	public void SmallAnglePathNormalizesColumnsWithoutMakingThemOrthogonal()
	{
		var target = new LimbRigidTransform( new LimbFrame( new Vector3( 1f, .2f, 0f ),
			new Vector3( .2f, 1f, 0f ), Vector3.UnitZ * 2f ), Vector3.Zero );
		var result = Create().Blend( Identity, target, .5f );
		Near( Vector3.Normalize( new Vector3( 1f, .1f, 0f ) ), result.Transform.Rotation.X );
		Assert.True( Vector3.Dot( result.Transform.Rotation.X, result.Transform.Rotation.Y ) > .19f );
		Assert.Equal( 0f, result.RemainingAngle );
	}

	[Theory]
	[InlineData( -1f )]
	[InlineData( 2f )]
	public void TransformWrapperExtrapolatesPositionBeyondRotationEndpoints( float weight )
	{
		var result = Create().BlendTransform( Identity, QuarterTurn, weight );
		Assert.Equal( QuarterTurn.Translation * weight, result.Translation );
		Assert.Equal( weight < 0f ? Identity.Rotation : QuarterTurn.Rotation, result.Rotation );
	}

	[Fact]
	public void UndefinedHalfTurnAxisRemainsVisibleAsNonfiniteOutput()
	{
		var target = new LimbRigidTransform( new LimbFrame( -Vector3.UnitX, -Vector3.UnitY, -Vector3.UnitZ ), Vector3.Zero );
		var result = Create().Blend( Identity, target, .5f );
		Assert.True( float.IsNaN( result.Transform.Rotation.X.X ) );
		Assert.Equal( MathF.PI / 2f, result.RemainingAngle );
	}

	[Fact]
	public void ArcTangentResolvesVerticalQuadrantsAndCopiesCoefficients()
	{
		var coefficients = Coefficients();
		var arc = new LimbArcTangent( coefficients, MathF.PI / 2f, MathF.PI );
		var before = arc.Evaluate( .2f, 1f );
		Array.Fill( coefficients, 100f );
		Assert.Equal( before, arc.Evaluate( .2f, 1f ) );
		Assert.Equal( MathF.PI / 2f, arc.Evaluate( 1f, 0f ) );
		Assert.Equal( -MathF.PI / 2f, arc.Evaluate( -1f, 0f ) );
		Assert.Equal( MathF.PI, arc.Evaluate( 0f, -1f ) );
	}

	[Fact]
	public void InvalidBlendParametersAreRejected()
	{
		Assert.Throws<ArgumentException>( () => new LimbArcTangent( new float[ 7 ], 1f, 2f ) );
		Assert.Throws<ArgumentException>( () => new LimbArcTangent( Coefficients(), float.NaN, 2f ) );
		Assert.Throws<ArgumentException>( () => Create( -1f ) );
	}

	private static void Near( Vector3 expected, Vector3 actual ) => Assert.True( Vector3.Distance( expected, actual ) < .000002f );

	private static float[] Coefficients() => new[] { 1f / 17f, -1f / 15f, 1f / 13f, -1f / 11f, 1f / 9f, -1f / 7f, 1f / 5f, -1f / 3f };

	private static LimbPoseBlender Create( float threshold = .1f )
	{
		// Independent Taylor profiles and synthetic branch thresholds.
		var angles = new LimbSolverMath( new[] { -1f / 720f, 1f / 24f, -.5f },
			new[] { -1f / 5040f, 1f / 120f, -1f / 6f }, new float[ 8 ],
			2f / MathF.PI, MathF.PI / 2f, 0f, MathF.PI );
		return new LimbPoseBlender( angles, new LimbArcTangent( Coefficients(), MathF.PI / 2f, MathF.PI ), .00001f, threshold );
	}
}
