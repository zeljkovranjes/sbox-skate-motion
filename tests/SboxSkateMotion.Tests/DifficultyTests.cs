using SkateMotion.Core;
using SkateMotion.Core.Vehicle;
using Xunit;

namespace SboxSkateMotion.Tests;

public class DifficultyTests
{
	[Fact]
	public void NormalIsTheMeasuredHandling()
	{
		Assert.Equal( 36f, HandlingCurves.PushRate( 0f, HandlingDifficulty.Normal ), 3 );
		Assert.Equal( 30f, HandlingCurves.PushRate( HandlingCurves.PushLimit, HandlingDifficulty.Normal ), 3 );
		Assert.Equal( HandlingCurves.PopSpeed( 0f, 0f, 3f ), HandlingCurves.PopSpeed( 0f, 0f, 3f, HandlingDifficulty.Normal ) );
		Assert.Equal( 0.9f, HandlingCurves.GrindReach( HandlingDifficulty.Normal ) );
	}

	[Fact]
	public void EasyIsMoreForgivingAndHardLess()
	{
		Assert.True( HandlingCurves.PushRate( 4f, HandlingDifficulty.Easy ) > HandlingCurves.PushRate( 4f ) );
		Assert.True( HandlingCurves.PushRate( 4f, HandlingDifficulty.Hard ) < HandlingCurves.PushRate( 4f ) );
		// A quick pop is full height on easy and lower on hard; a full windup pops the same on all three.
		var quick = HandlingCurves.PopSpeed( 0f, 0f, 3f );
		Assert.True( HandlingCurves.PopSpeed( 0f, 0f, 3f, HandlingDifficulty.Easy ) > quick );
		Assert.True( HandlingCurves.PopSpeed( 0f, 0f, 3f, HandlingDifficulty.Hard ) < quick );
		Assert.Equal( HandlingCurves.PopSpeed( 1f, 0f, 3f ), HandlingCurves.PopSpeed( 1f, 0f, 3f, HandlingDifficulty.Hard ), 4 );
		Assert.True( HandlingCurves.GrindReach( HandlingDifficulty.Easy ) > 0.9f );
		Assert.True( HandlingCurves.GrindReach( HandlingDifficulty.Hard ) < 0.2f );
		Assert.True( HandlingCurves.AlignRate( HandlingDifficulty.Hard ) < HandlingCurves.AlignRate( HandlingDifficulty.Normal ) );
	}
}
