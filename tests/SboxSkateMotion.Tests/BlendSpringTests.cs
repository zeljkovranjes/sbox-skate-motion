using System;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class BlendSpringTests
{
	[Theory]
	[InlineData( 0f, 1f )]
	[InlineData( 1f, -1f )]
	public void TheLeansSpeedNeverStepsAsTheStickDoes( float from, float to )
	{
		// The stick snapping over (a turn begun, or swapped to the other side) moves the lean off gently, gathers speed and
		// settles on the new side, rather than setting off at full speed in a frame.
		var director = new RiderAnimationDirector( false );
		var parameters = new AnimationParameters();
		var input = new RiderAnimationInput { Speed = 4f, Steer = from };
		for ( var frame = 0; frame < 120; frame++ )
			director.Update( input, 1f / 60f, parameters );
		var last = parameters.Number( "blend-lean" );
		var lastStep = 0f;
		var worst = 0f;
		for ( var frame = 0; frame < 120; frame++ )
		{
			director.Update( input with { Steer = to }, 1f / 60f, parameters );
			var now = parameters.Number( "blend-lean" );
			worst = MathF.Max( worst, MathF.Abs( (now - last) - lastStep ) );
			lastStep = now - last;
			last = now;
		}
		Assert.True( worst < 0.06f, $"the lean's step changed by {worst:F3} in a frame" );
		Assert.InRange( last, to - 0.01f, to + 0.01f );
	}
}
