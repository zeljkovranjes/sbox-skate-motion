using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class HippyPopHeightTests
{
	[Theory]
	[InlineData( true, false, 0.3f, 0.3f )]
	[InlineData( false, true, 0.8f, 0.8f )]
	[InlineData( false, false, 0.3f, 1f )]
	public void AHippyJumpsClipsFollowItsOwnCrouch( bool crouching, bool flying, float charge, float expected )
	{
		// Crouching for a hippy jump or flying it, its clips go by its own crouch; otherwise by the last trick's pop (high).
		var director = new RiderAnimationDirector( false );
		var parameters = new AnimationParameters();
		director.Update( new RiderAnimationInput
		{
			HighPop = true, HippyJump = crouching || flying, HippyCrouch = crouching, OffBoard = flying, HippyCharge = charge
		}, 1f / 60f, parameters );
		Assert.Equal( expected, parameters.Number( "blend-pop-height" ), 3 );
	}
}
