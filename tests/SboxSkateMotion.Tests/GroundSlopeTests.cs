using System;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class GroundSlopeTests
{
	[Theory]
	[InlineData( 0.02f, 1f, false, "flat" )]
	[InlineData( 0.27f, 1f, false, "ramp-up-shallow" )]
	[InlineData( -0.27f, 1f, false, "ramp-dn-shallow" )]
	[InlineData( 1f, 1f, false, "ramp-up-steep" )]
	[InlineData( -1f, 1f, false, "ramp-dn-steep" )]
	[InlineData( 0.3f, 1f, true, "stair-up-shallow" )]
	[InlineData( 0.6f, 1f, true, "stair-up-steep" )]
	[InlineData( -0.6f, 1f, true, "stair-dn-steep" )]
	// Steps too small to count as a slope are level ground.
	[InlineData( 0.05f, 1f, true, "flat" )]
	public void TheGroundIsNamedAsTheWalkingClipsNameIt( float rise, float run, bool stepped, string expected )
	{
		Assert.Equal( expected, GroundSlope.Classify( rise, run, stepped ) );
	}

	[Fact]
	public void NothingToMeasureIsLevel()
	{
		Assert.Equal( GroundSlope.Flat, GroundSlope.Classify( 1f, 0f, false ) );
		Assert.Equal( GroundSlope.Flat, GroundSlope.Classify( float.NaN, 1f, false ) );
	}

	[Fact]
	public void ANewSlopeIsTakenOnlyOnceItHolds()
	{
		var slope = new GroundSlope();
		var step = 1f / 60f;
		// A kerb's single frame of slope does not change the gait.
		Assert.Equal( GroundSlope.Flat, slope.Update( "ramp-up-shallow", step ) );
		Assert.Equal( GroundSlope.Flat, slope.Update( GroundSlope.Flat, step ) );
		var frames = 0;
		while ( slope.Update( "ramp-up-shallow", step ) != "ramp-up-shallow" )
			frames++;
		Assert.InRange( frames * step, GroundSlope.Settle - 2f * step, GroundSlope.Settle + step );
		Assert.Equal( "ramp-up-shallow", slope.Current );
	}

	[Fact]
	public void StairsTakeHoldThoughTheirStepsSwingTheirSteepness()
	{
		// Walking up stairs each tread passing under the samples swings the reading between shallow and steep, faster than
		// a class holds: the stairs are still taken, and then kept while the reading swings on.
		var slope = new GroundSlope();
		var step = 1f / 60f;
		var taken = -1f;
		for ( var frame = 0; frame < 60; frame++ )
		{
			var reading = frame / 6 % 2 == 0 ? "stair-up-shallow" : "stair-up-steep";
			var current = slope.Update( reading, step );
			if ( taken < 0f && current != GroundSlope.Flat )
				taken = frame * step;
			if ( taken >= 0f )
				Assert.StartsWith( "stair-up", current );
		}
		Assert.InRange( taken, GroundSlope.Settle - 2f * step, GroundSlope.Settle + 2f * step );
		// A ramp after the stairs is another kind of ground: it must hold on its own.
		Assert.StartsWith( "stair-up", slope.Update( "ramp-up-steep", step ) );
	}
}
