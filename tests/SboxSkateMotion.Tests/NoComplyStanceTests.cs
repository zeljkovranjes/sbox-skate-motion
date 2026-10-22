using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class NoComplyStanceTests
{
	private const float Frame = 1f / 60f;

	[Fact]
	public void AWindupBegunJustAfterAPushStartsPushesOnInsteadOfCrouching()
	{
		// The push first, then the pop's wind-up: a no comply's. The rider pushes on, so the graph stays in the push's start.
		var facts = new StateFacts();
		var source = new RiderStateFacts();
		source.Fill( facts, new RiderAnimationInput { Pushing = true }, false, Frame );
		source.Fill( facts, new RiderAnimationInput { Pushing = true }, false, Frame );
		source.Fill( facts, new RiderAnimationInput { Pushing = true, Windup = 1f }, false, Frame );
		Assert.True( facts.Intents.ContainsKey( "left-push" ) );
		Assert.False( facts.Intents.ContainsKey( "ollie-anticipation" ) );
		// Wound up once the push is well under way, it crouches for an ollie as ever.
		var late = new RiderStateFacts();
		for ( var frame = 0; frame < 40; frame++ )
			late.Fill( facts, new RiderAnimationInput { Pushing = true }, false, Frame );
		late.Fill( facts, new RiderAnimationInput { Pushing = true, Windup = 1f }, false, Frame );
		Assert.True( facts.Intents.ContainsKey( "ollie-anticipation" ) );
		Assert.False( facts.Intents.ContainsKey( "left-push" ) );
	}

	[Fact]
	public void AWindupHeldBeforeThePushStillCrouches()
	{
		var facts = new StateFacts();
		var source = new RiderStateFacts();
		source.Fill( facts, new RiderAnimationInput { Windup = 1f }, false, Frame );
		source.Fill( facts, new RiderAnimationInput { Windup = 1f, Pushing = true }, false, Frame );
		Assert.True( facts.Intents.ContainsKey( "ollie-anticipation" ) );
		Assert.False( facts.Intents.ContainsKey( "left-push" ) || facts.Intents.ContainsKey( "right-push" ) );
	}

	[Theory]
	[InlineData( false, true )]
	[InlineData( true, false )]
	[InlineData( false, false )]
	[InlineData( true, true )]
	public void ANoComplysPopMakesTheStockPushTheFrontFootOne( bool goofy, bool mirrored )
	{
		// The graph goes into the no comply only from a front-foot push. The stock push (the back foot, as the clips play it:
		// the right when mirrored) is told on the pop the stance that makes it the front foot; everywhere else, the rider's own.
		var facts = new StateFacts();
		var source = new RiderStateFacts();
		source.Fill( facts, new RiderAnimationInput { Goofy = goofy, Pushing = true }, mirrored, Frame );
		Assert.Equal( goofy ? 1f : 0f, facts.Facts[ "is-riding-goofy" ] );
		source.Fill( facts, new RiderAnimationInput { Goofy = goofy, Pushing = true, Trick = "ollie", TimeSincePop = 0f }, mirrored, Frame );
		Assert.Equal( mirrored ? 1f : 0f, facts.Facts[ "is-riding-goofy" ] );
		// In the air afterwards, and in a plain ollie, the rider's own.
		source.Fill( facts, new RiderAnimationInput { Goofy = goofy, Airborne = true, Trick = "ollie", TimeSincePop = 0.3f }, mirrored, Frame );
		Assert.Equal( goofy ? 1f : 0f, facts.Facts[ "is-riding-goofy" ] );
		var plain = new RiderStateFacts();
		plain.Fill( facts, new RiderAnimationInput { Goofy = goofy, Trick = "ollie", TimeSincePop = 0f }, mirrored, Frame );
		Assert.Equal( goofy ? 1f : 0f, facts.Facts[ "is-riding-goofy" ] );
	}

	[Theory]
	[InlineData( false, true, false )]
	[InlineData( false, true, true )]
	[InlineData( true, false, false )]
	[InlineData( true, false, true )]
	public void ANoComplysPopTakesTheFootFromAPushAskedForByItsOwnButton( bool goofy, bool mirrored, bool right )
	{
		// A push asked for with one foot's own button: the pop is told the stance that makes that foot the front one, so a
		// front-foot push keeps the rider's own stance and a back-foot push turns it, whichever way the clips are mirrored.
		var facts = new StateFacts();
		var source = new RiderStateFacts();
		var push = new RiderAnimationInput { Goofy = goofy, Pushing = true, PushLeft = !right, PushRight = right, NewPush = true };
		source.Fill( facts, push, mirrored, Frame );
		source.Fill( facts, push with { NewPush = false, Trick = "ollie", TimeSincePop = 0f }, mirrored, Frame );
		Assert.Equal( right ? 1f : 0f, facts.Facts[ "is-riding-goofy" ] );
	}
}
