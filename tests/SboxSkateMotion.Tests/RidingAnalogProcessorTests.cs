using SkateMotion.Core.Animation;
using SkateMotion.Core.Input;
using Xunit;

namespace SboxSkateMotion.Tests;

public class RidingAnalogProcessorTests
{
	private static readonly RidingAnalogTuning Tuning = new( 2f, 3f, 1f, .5f,
		.5f, 1.5f, -1f, .01f, .5f, 2f, 2f, 1f, .25f, 3f );

	[Fact]
	public void StickValuesKeepNeutralAndCardinalInputsSeparate()
	{
		var math = new LimbArcTangent( new float[ 8 ], MathF.PI / 2f, MathF.PI );
		Assert.Equal( default, ControllerStickValues.Evaluate( default, math ) );
		var result = ControllerStickValues.Evaluate( Sample() with { LeftX = 1f, RightY = -1f }, math );
		Assert.True( result.HasLeft );
		Assert.True( result.HasRight );
		Assert.Equal( MathF.PI / 2f, result.LeftAngle );
		Assert.Equal( 0f, result.RightAngle );
		Assert.Equal( 1f, result.LeftMagnitude );
		Assert.Equal( 1f, result.RightMagnitude );
	}

	[Fact]
	public void TurnUsesRawHorizontalInputInTheRearHalf()
	{
		var result = Read( Sample() with { LeftX = -.25f, LeftY = -.75f }, Sticks( .5f, 1f ) );
		Assert.Equal( -.25f, result.Turn );
	}

	[Fact]
	public void ForwardTurnTapersSignedRadialPressure()
	{
		var current = Sample() with { LeftX = -.25f, LeftY = .75f };
		Assert.Equal( -1f, Read( current, Sticks( -1.8f, 1f ) ).Turn );
		Assert.Equal( -.5f, Read( current, Sticks( -2.5f, 1f ) ).Turn );
		Assert.Equal( 0f, Read( current, Sticks( -3.01f, 1f ) ).Turn );
	}

	[Theory]
	[InlineData( .5f, 1f )]
	[InlineData( 1.5f, .01f )]
	public void HardTurnIncludesBothAngleEndpointsAndKeepsMinimumPressure( float angle, float expected )
	{
		var result = Read( Sample(), Sticks( angle, 1f ) );
		Assert.True( result.HasHardTurn );
		Assert.Equal( expected, result.HardTurn );
		Assert.Equal( expected * .5f, result.HardTurnCrouch );
	}

	[Fact]
	public void FullStickThresholdIsStrictForHardTurnsAndSlides()
	{
		var result = Read( Sample(), Sticks( .75f, .5f ) );
		Assert.False( result.HasHardTurn );
		Assert.Equal( 0f, result.LeftSlide );
		Assert.Equal( 0f, result.RightSlide );
	}

	[Fact]
	public void OpposingSlideRequestsCanCoexist()
	{
		var result = Read( Sample(), Sticks( 0f, 1f ) );
		Assert.Equal( -.5f, result.LeftSlide );
		Assert.Equal( .5f, result.RightSlide );
		Assert.False( result.LeftSlideStart );
		Assert.False( result.RightSlideStart );
	}

	[Theory]
	[InlineData( .25f, false, true )]
	[InlineData( -.25f, true, false )]
	[InlineData( .5f, false, false )]
	public void SlideStartWindowsAreSignedAndStrict( float angle, bool left, bool right )
	{
		var result = Read( Sample(), Sticks( angle, 1f ) );
		Assert.Equal( left, result.LeftSlideStart );
		Assert.Equal( right, result.RightSlideStart );
	}

	[Fact]
	public void SuppressingTurnsDoesNotSuppressBodySpinOrSlides()
	{
		var result = Read( Sample() with { LeftX = .25f }, Sticks( .75f, 1f ), new() { SuppressTurn = true } );
		Assert.Equal( 0f, result.Turn );
		Assert.Equal( 0f, result.KickTurn );
		Assert.False( result.HasHardTurn );
		Assert.Equal( .25f, result.BodySpin );
		Assert.NotEqual( 0f, result.LeftSlide );
	}

	[Fact]
	public void SuppressingSpinAndSlidesKeepsTurnAndGrindAxes()
	{
		var result = Read( Sample() with { LeftX = .25f, RightX = .5f }, Sticks( .75f, 1f ),
			new() { SuppressSpin = true, SuppressSlide = true } );
		Assert.Equal( 0f, result.BodySpin );
		Assert.Equal( .25f, result.Turn );
		Assert.Equal( -.25f, result.GrindBalance );
		Assert.Equal( .75f, result.GrindTranslation );
		Assert.Equal( 0f, result.LeftSlide );
		Assert.Equal( 0f, result.RightSlide );
	}

	[Fact]
	public void RightStickPairsKeepTheirZeroCompanionAxis()
	{
		var sticks = new ControllerStickValues( false, 0f, 0f, true, 0f, .5f );
		var result = Read( Sample() with { RightY = -.5f }, sticks );
		Assert.True( result.HasTweak );
		Assert.Equal( 0f, result.TweakX );
		Assert.Equal( -.5f, result.TweakY );
		Assert.True( result.HasBoardAdjust );
		Assert.Equal( 0f, result.BoardAdjustAngle );
		Assert.True( result.HasAnticipation );
	}

	[Fact]
	public void CrouchMayBePresentWithZeroPressure()
	{
		var result = Read( Sample(), Sticks( 2.5f, 1f ) );
		Assert.True( result.HasCrouch );
		Assert.Equal( 0f, result.Crouch );
		result = Read( Sample() with { RightTrigger = -.1f }, default );
		Assert.True( result.HasCrouch );
		Assert.Equal( 0f, result.Crouch );
	}

	[Fact]
	public void CrouchPressureUsesTheLargerTriggerWithoutUpperClamping()
	{
		var result = Read( Sample() with { LeftTrigger = .5f, RightTrigger = 1.25f }, default );
		Assert.True( result.HasCrouch );
		Assert.Equal( 1.25f, result.Crouch );
	}

	[Fact]
	public void AutoPumpHasIndependentSignedDirectionAndMagnitude()
	{
		var result = Read( Sample(), Sticks( -1f, .75f ) );
		Assert.True( result.HasAutoPump );
		Assert.Equal( -2f, result.AutoPumpAngle );
		Assert.Equal( .75f, result.AutoPumpMagnitude );
		Assert.False( Read( Sample(), Sticks( -1f, .75f ), new() { AutoPushActive = true } ).HasAutoPump );
	}

	[Fact]
	public void CombinedGrindTranslationClampsAfterAddingBothSticks()
	{
		var result = Read( Sample() with { LeftX = .8f, RightX = .8f }, default );
		Assert.Equal( 1f, result.GrindTranslation );
		Assert.Equal( .8f, result.GrindStabilityNudge );
	}

	private static RidingAnalogActions Read( ControllerReading current, ControllerStickValues sticks,
		RidingAnalogContext context = default ) => RidingAnalogProcessor.Evaluate( current, sticks, context, Tuning );

	private static ControllerReading Sample() => new( 0f, 0f, 0f, 0f, 0f, 0f, ControllerButtons.None );

	private static ControllerStickValues Sticks( float angle, float magnitude ) => new( true, angle, magnitude, false, 0f, 0f );
}
