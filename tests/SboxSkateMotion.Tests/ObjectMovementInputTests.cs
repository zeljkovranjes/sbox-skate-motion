using SkateMotion.Core.Animation;
using SkateMotion.Core.Input;
using Xunit;

namespace SboxSkateMotion.Tests;

public class ObjectMovementInputTests
{
	[Theory]
	[InlineData( 8 )]
	[InlineData( 16 )]
	public void ResponseInterpolatesAndKeepsEndpointValues( int count )
	{
		var inputs = new float[ count ];
		var outputs = new float[ count ];
		for ( var i = 0; i < count; i++ )
		{
			inputs[ i ] = i;
			outputs[ i ] = i * 2f - 1f;
		}
		var curve = new InputResponseCurve( inputs, outputs );
		Assert.Equal( -1f, curve.Evaluate( -2f ) );
		Assert.Equal( 4f, curve.Evaluate( 2.5f ) );
		Assert.Equal( outputs[ ^1 ], curve.Evaluate( count + 1f ) );
		inputs[ 0 ] = 100f;
		outputs[ 0 ] = 100f;
		Assert.Equal( -1f, curve.Evaluate( -2f ) );
	}

	[Fact]
	public void RepeatedCurveInputsAdvanceToTheNextInterval()
	{
		var curve = new InputResponseCurve( [ 0f, 1f, 1f, 2f, 3f, 4f, 5f, 6f ], [ 0f, 2f, 4f, 8f, 8f, 8f, 8f, 8f ] );
		Assert.Equal( 4f, curve.Evaluate( 1f ) );
		Assert.Equal( 6f, curve.Evaluate( 1.5f ) );
	}

	[Fact]
	public void UnorderedCurveQueryUsesFirstOutputFallback()
	{
		Assert.Equal( .25f, Constant( .25f ).Evaluate( float.NaN ) );
	}

	[Theory]
	[InlineData( -2f, -1f )]
	[InlineData( -.25f, -.25f )]
	[InlineData( 0f, 0f )]
	[InlineData( .25f, .25f )]
	[InlineData( 2f, 1f )]
	public void InactiveLeftStickStillPermitsBoundedDirectRotation( float right, float expected )
	{
		var current = new ControllerReading( .5f, -.75f, right, 0f, 0f, 0f, ControllerButtons.None );
		var result = Processor().Evaluate( current, false, 1f );
		Assert.Equal( 0f, result.Sideways );
		Assert.Equal( 0f, result.Forward );
		Assert.Equal( expected, result.Rotation );
	}

	[Fact]
	public void TranslationResponsesRemainIndependentAndUnclamped()
	{
		var current = new ControllerReading( .75f, -.5f, 0f, 0f, 0f, 0f, ControllerButtons.None );
		var result = Processor( sideways: 2f, forward: 3f ).Evaluate( current, true, .5f );
		Assert.Equal( 1.5f, result.Sideways );
		Assert.Equal( -1.5f, result.Forward );
	}

	[Fact]
	public void DirectionChangesRotationSignBeforeDirectInputIsAdded()
	{
		var current = new ControllerReading( 1f, 0f, .125f, 0f, 0f, 0f, ControllerButtons.None );
		var processor = Processor( rotation: .5f );
		Assert.Equal( .375f, processor.Evaluate( current, true, .5f ).Rotation );
		Assert.Equal( -.125f, processor.Evaluate( current with { LeftX = -1f }, true, .5f ).Rotation );
	}

	[Fact]
	public void RotationWrapsBothBackwardSeamsToPositiveHalfTurn()
	{
		var current = new ControllerReading( 0f, 1f, 0f, 0f, 0f, 0f, ControllerButtons.None );
		var processor = Processor( rotation: .5f );
		Assert.Equal( .25f, processor.Evaluate( current, true, .5f ).Rotation );
		Assert.Equal( .25f, processor.Evaluate( current with { LeftX = -0f }, true, .5f ).Rotation );
	}

	[Fact]
	public void RotationClampsAfterBothSticksAreCombined()
	{
		var current = new ControllerReading( 1f, 0f, .75f, 0f, 0f, 0f, ControllerButtons.None );
		Assert.Equal( 1f, Processor( rotation: .75f ).Evaluate( current, true, 1f ).Rotation );
	}

	private static ObjectMovementInputProcessor Processor( float rotation = 0f, float sideways = 1f, float forward = 1f ) => new(
		new LimbArcTangent( new float[ 8 ], MathF.PI / 2f, MathF.PI ),
		Constant( rotation, 16 ), Constant( sideways ), Constant( forward ), 1f / MathF.Tau, MathF.Tau, 1f / MathF.PI );

	private static InputResponseCurve Constant( float value, int count = 8 )
	{
		var inputs = new float[ count ];
		var outputs = new float[ count ];
		for ( var i = 0; i < count; i++ )
		{
			inputs[ i ] = (float)i / (count - 1);
			outputs[ i ] = value;
		}
		return new( inputs, outputs );
	}
}
