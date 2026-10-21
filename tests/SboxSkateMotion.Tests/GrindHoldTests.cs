using SkateMotion.Core;
using SkateMotion.Core.Grinding;
using Xunit;

namespace SboxSkateMotion.Tests;

public class GrindHoldTests
{
	private const float Step = 1f / 240f;

	private static readonly GrindShape Plain = default;
	private static readonly GrindShape Dropped = default( GrindShape ) with { Low = true };

	[Fact]
	public void APlain5050HoldsItsNameThroughAWobbleOntoOneTruck()
	{
		var hold = new GrindHold();
		Assert.Equal( GrindKind.FiftyFifty, hold.Update( GrindKind.FiftyFifty, Plain, default, Step ).Kind );
		// The nose truck lifts and the nose dips for a moment, nobody asking: still a 50-50.
		for ( var i = 0; i < 20; i++ )
			Assert.Equal( GrindKind.FiftyFifty, hold.Update( GrindKind.FiveO, i < 10 ? Plain : Dropped, default, Step ).Kind );
		var back = hold.Update( GrindKind.FiftyFifty, Plain, default, Step );
		Assert.Equal( GrindKind.FiftyFifty, back.Kind );
		Assert.False( back.Shape.Low );
	}

	[Fact]
	public void AClassificationThatLastsRenamesTheGrindAfterItSettles()
	{
		var hold = new GrindHold();
		hold.Update( GrindKind.FiftyFifty, Plain, default, Step );
		var steps = 0;
		while ( hold.Update( GrindKind.FiveO, Plain, default, Step ).Kind != GrindKind.FiveO )
			steps++;
		Assert.InRange( steps * Step, GrindHold.Settle - 2f * Step, GrindHold.Settle + Step );
	}

	[Fact]
	public void LeaningOntoATruckRenamesItAtOnceAndLettingGoReturnsAt5050()
	{
		var hold = new GrindHold();
		var lean = default( ControlInput ) with { FlickY = -0.45f };
		hold.Update( GrindKind.FiftyFifty, Plain, default, Step );
		Assert.Equal( GrindKind.FiveO, hold.Update( GrindKind.FiveO, Plain, lean, Step ).Kind );
		// Held on the truck, a moment back on both trucks is not a 50-50.
		Assert.Equal( GrindKind.FiveO, hold.Update( GrindKind.FiftyFifty, Plain, lean, Step ).Kind );
		Assert.Equal( GrindKind.FiftyFifty, hold.Update( GrindKind.FiftyFifty, Plain, default, Step ).Kind );
	}

	[Fact]
	public void LockingOnOneTruckIsA5050UnlessTheRiderLeansOntoIt()
	{
		Assert.Equal( GrindKind.FiftyFifty, new GrindHold().Update( GrindKind.Nosegrind, Dropped, default, Step ).Kind );
		var lean = default( ControlInput ) with { FlickY = 0.45f };
		var held = new GrindHold().Update( GrindKind.Nosegrind, Dropped, lean, Step );
		Assert.Equal( GrindKind.Nosegrind, held.Kind );
		Assert.True( held.Shape.Low );
	}

	[Fact]
	public void A5050KeepsItsNameAsTheNoseTruckRollsOffTheRailsEnd()
	{
		var hold = new GrindHold();
		hold.Update( GrindKind.FiftyFifty, Plain, default, Step );
		// The nose truck passes the end and drops for 0.2 s before the tail truck leaves: the grind stays a 50-50.
		for ( var i = 0; i < 48; i++ )
			Assert.Equal( GrindKind.FiftyFifty, hold.Update( GrindKind.FiveO, i < 12 ? Plain : Dropped, default, Step ).Kind );
		Assert.Equal( GrindKind.None, hold.Update( GrindKind.None, Plain, default, Step ).Kind );
	}

	[Fact]
	public void SteeringOnATruckDropsItAtOnceAndASlideKeepsItsKind()
	{
		var hold = new GrindHold();
		var lean = default( ControlInput ) with { FlickY = -0.45f };
		hold.Update( GrindKind.FiveO, Plain, lean, Step );
		var steer = lean with { Steer = 1f };
		Assert.True( hold.Update( GrindKind.FiveO, Dropped, steer, Step ).Shape.Low );

		var slide = new GrindHold();
		slide.Update( GrindKind.Boardslide, Plain, default, Step );
		Assert.Equal( GrindKind.Boardslide, slide.Update( GrindKind.Tailslide, Plain, steer, Step ).Kind );
		Assert.Equal( GrindKind.None, slide.Update( GrindKind.None, Plain, default, Step ).Kind );
	}
}
