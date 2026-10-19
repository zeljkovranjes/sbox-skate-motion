using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class PushFootFactsTests
{
	[Theory]
	[InlineData( false, true, false )]
	[InlineData( true, true, false )]
	[InlineData( false, false, true )]
	[InlineData( true, false, true )]
	[InlineData( false, true, true )]
	[InlineData( true, true, true )]
	public void ExplicitFeetSurviveStanceAndRepeatedPushFrames( bool mirrored, bool left, bool right )
	{
		var source = new RiderStateFacts();
		var facts = new StateFacts();
		var input = new RiderAnimationInput { Pushing = true, PushLeft = left, PushRight = right, NewPush = true };
		for ( var frame = 0; frame < 3; frame++ )
		{
			source.Fill( facts, input, mirrored, .016f );
			Assert.Equal( left, facts.Intents.ContainsKey( "left-push" ) );
			Assert.Equal( right, facts.Intents.ContainsKey( "right-push" ) );
			Assert.True( facts.Intents.ContainsKey( "new-push" ) );
		}
		source.Fill( facts, input with { NewPush = false }, mirrored, .016f );
		Assert.False( facts.Intents.ContainsKey( "new-push" ) );
		source.Fill( facts, input with { Pushing = false }, mirrored, .016f );
		Assert.False( facts.Intents.ContainsKey( "left-push" ) );
		Assert.False( facts.Intents.ContainsKey( "right-push" ) );
		Assert.False( facts.Intents.ContainsKey( "new-push" ) );
	}

	[Fact]
	public void ChangingFeetWhilePushingDoesNotRequireReleasingBoth()
	{
		var source = new RiderStateFacts();
		var facts = new StateFacts();
		source.Fill( facts, new RiderAnimationInput { Pushing = true, PushLeft = true }, false, .016f );
		Assert.False( facts.Intents.ContainsKey( "new-push" ) );
		source.Fill( facts, new RiderAnimationInput { Pushing = true, PushRight = true, NewPush = true }, false, .016f );
		Assert.False( facts.Intents.ContainsKey( "left-push" ) );
		Assert.True( facts.Intents.ContainsKey( "right-push" ) );
		Assert.True( facts.Intents.ContainsKey( "new-push" ) );
	}

	[Theory]
	[InlineData( false, "left-push" )]
	[InlineData( true, "right-push" )]
	public void LegacyStrengthOnlyInputRetainsStanceAndRisingEdge( bool mirrored, string foot )
	{
		var source = new RiderStateFacts();
		var facts = new StateFacts();
		var input = new RiderAnimationInput { Pushing = true };
		source.Fill( facts, input, mirrored, .016f );
		Assert.True( facts.Intents.ContainsKey( foot ) );
		Assert.True( facts.Intents.ContainsKey( "new-push" ) );
		source.Fill( facts, input, mirrored, .016f );
		Assert.True( facts.Intents.ContainsKey( foot ) );
		Assert.False( facts.Intents.ContainsKey( "new-push" ) );
	}
}
