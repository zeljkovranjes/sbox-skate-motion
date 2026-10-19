using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class TiltParameterControllerTests
{
	[Fact]
	public void AccelerationLimitsStartAndReversalWithoutDiscardingVelocity()
	{
		var controller = ConstantGain( 1f, .25f );
		Assert.Equal( .25f, controller.Step( true, true, 1f, 0f, 0 ) );
		Assert.Equal( .75f, controller.Step( true, true, 1f, 0f, 0 ) );
		// Reversing the target first decelerates the existing positive velocity.
		Assert.Equal( 1f, controller.Step( true, true, -1f, 0f, 0 ) );
		Assert.Equal( 1f, controller.Step( true, true, -1f, 0f, 0 ) );
		Assert.Equal( .75f, controller.Step( true, true, -1f, 0f, 0 ) );
	}

	[Fact]
	public void ReenablingResetsBothValueAndVelocityAndDisabledStepsPublishNothing()
	{
		var controller = ConstantGain( 1f, .25f );
		controller.Step( true, true, 1f, 0f, 0 );
		controller.Step( true, true, 1f, 0f, 0 );
		Assert.Null( controller.Step( false, true, 1f, 0f, 0 ) );
		Assert.Null( controller.Step( false, true, 1f, 0f, 0 ) );
		Assert.Equal( -.25f, controller.Step( true, true, -1f, 0f, 0 ) );
	}

	[Theory]
	[InlineData( 0, .125f )]
	[InlineData( 1, .125f )]
	[InlineData( 2, .5f )]
	[InlineData( 3, .125f )]
	public void OnlyCategoryTwoUsesTheAlternateLimits( int category, float expected )
	{
		var controller = new TiltParameterController( new[] { 0f, 1f, 2f, 3f },
			new[] { 1f, 1f, 1f, 1f }, .125f, .25f, .5f, 1f );
		Assert.Equal( expected, controller.Step( true, true, 1f, 0f, category ) );
	}

	[Theory]
	[InlineData( 0f, 1f )]
	[InlineData( 1.5f, .875f )]
	[InlineData( -1.5f, .875f )]
	[InlineData( 2f, .75f )]
	[InlineData( 3.5f, .5f )]
	[InlineData( 10f, .25f )]
	public void SpinMagnitudeSelectsAuthoredCurveAndMirroringControlsTiltSign( float spin, float gain )
	{
		foreach ( var mirrored in new[] { false, true } )
		{
			var controller = new TiltParameterController( new[] { 1f, 2f, 3f, 4f },
				new[] { 1f, .75f, .75f, .25f }, 4f, 4f, 4f, 4f );
			Assert.Equal( (mirrored ? 1f : -1f) * gain, controller.Step( true, mirrored, 1f, spin, 0 ) );
		}
	}

	[Fact]
	public void AuthoredCurveIsIndependentOfTheCallersMutableArrays()
	{
		var spin = new[] { 0f, 1f, 2f, 3f };
		var gain = new[] { 1f, .75f, .5f, 0f };
		var controller = new TiltParameterController( spin, gain, 1f, 1f, 1f, 1f );
		spin[ 1 ] = 100f;
		gain[ 1 ] = 100f;
		Assert.Equal( .75f, controller.Step( true, true, 1f, 1f, 0 ) );
	}

	private static TiltParameterController ConstantGain( float speed, float acceleration ) =>
		new( new[] { 0f, 1f, 2f, 3f }, new[] { 1f, 1f, 1f, 1f }, speed, acceleration, speed, acceleration );
}
