using SkateMotion.Core.Animation;
using SkateMotion.Core.Grinding;
using Xunit;

namespace SboxSkateMotion.Tests;

public class GrindNamesTests
{
	private const float Frame = 1f / 60f;

	[Theory]
	// One truck, frontside: the nose toward the heels lies on the near side.
	[InlineData( "fivezero", true, true, false, false, "fs-5-o" )]
	[InlineData( "fivezero", true, false, true, false, "fs-salad" )]
	[InlineData( "fivezero", true, true, true, true, "fs-smith" )]
	[InlineData( "fivezero", true, false, false, true, "fs-feeble" )]
	[InlineData( "nosegrind", true, true, false, false, "fs-nosegrind" )]
	[InlineData( "nosegrind", true, true, true, false, "fs-overcrook" )]
	[InlineData( "nosegrind", true, false, true, false, "fs-crook" )]
	[InlineData( "nosegrind", true, true, false, true, "fs-overwilly" )]
	[InlineData( "nosegrind", true, false, false, true, "fs-willy" )]
	// Backside the heels face the far side, so the same nose side names the other trick.
	[InlineData( "fivezero", false, true, true, false, "bs-salad" )]
	[InlineData( "fivezero", false, false, true, false, "bs-5-o" )]
	[InlineData( "fivezero", false, true, false, true, "bs-feeble" )]
	[InlineData( "fivezero", false, false, false, true, "bs-smith" )]
	[InlineData( "nosegrind", false, true, true, false, "bs-crook" )]
	[InlineData( "nosegrind", false, false, true, false, "bs-overcrook" )]
	[InlineData( "nosegrind", false, true, false, true, "bs-willy" )]
	[InlineData( "nosegrind", false, false, false, true, "bs-overwilly" )]
	public void OneTruckGrindsAreNamedByTheFreeEnd( string kind, bool frontside, bool noseToHeels, bool twisted,
		bool low, string expected )
	{
		Assert.Equal( expected, GrindNames.Graph( kind, frontside, noseToHeels, false, twisted, low ) );
	}

	[Theory]
	[InlineData( "boardslide", true, true, "fs-lip" )]
	[InlineData( "boardslide", true, false, "fs-board" )]
	[InlineData( "boardslide", false, true, "bs-board" )]
	[InlineData( "boardslide", false, false, "bs-lip" )]
	[InlineData( "noseslide", true, true, "fs-noseblunt" )]
	[InlineData( "noseslide", true, false, "fs-noseslide" )]
	[InlineData( "noseslide", false, true, "bs-noseslide" )]
	[InlineData( "noseslide", false, false, "bs-noseblunt" )]
	[InlineData( "tailslide", true, true, "fs-tailslide" )]
	[InlineData( "tailslide", true, false, "fs-blunt" )]
	[InlineData( "tailslide", false, true, "bs-blunt" )]
	[InlineData( "tailslide", false, false, "bs-tailslide" )]
	public void SlidesAreNamedByWhichWayTheBoardTurnedOverTheRail( string kind, bool frontside, bool noseToHeels,
		string expected )
	{
		Assert.Equal( expected, GrindNames.Graph( kind, frontside, noseToHeels, false, false, false ) );
	}

	[Theory]
	[InlineData( "fiftyfifty", true, "bf-fs-50-50" )]
	[InlineData( "fivezero", false, "bf-bs-5-o" )]
	[InlineData( "nosegrind", true, "bf-fs-nosegrind" )]
	public void TruckGrindsRiddenBackwardsAreTheirBackwardFacingVersions( string kind, bool frontside, string expected )
	{
		Assert.Equal( expected, GrindNames.Graph( kind, frontside, null, true, false, false ) );
	}

	[Fact]
	public void SlidesKeepTheirNameRiddenEitherWay()
	{
		Assert.Equal( GrindNames.Graph( "boardslide", false, true, false, false, false ),
			GrindNames.Graph( "boardslide", false, true, true, false, false ) );
		Assert.Equal( GrindNames.Graph( "tailslide", true, true, false, false, false ),
			GrindNames.Graph( "tailslide", true, true, true, false, false ) );
	}

	[Theory]
	[InlineData( "noseslide", true, "fs-noseblunt" )]
	[InlineData( "noseslide", false, "bs-noseblunt" )]
	[InlineData( "tailslide", true, "bs-blunt" )]
	[InlineData( "tailslide", false, "fs-blunt" )]
	public void AnEndOnALedgeTopIsABluntNamedByTheTurn( string kind, bool noseToHeels, string expected )
	{
		Assert.Equal( expected, GrindNames.Graph( kind, true, noseToHeels, false, false, false, backslash: true ) );
		Assert.Equal( expected, GrindNames.Graph( kind, false, noseToHeels, false, false, false, backslash: true ) );
	}

	[Theory]
	[InlineData( "boardslide", true, "fs-darkslide" )]
	[InlineData( "boardslide", false, "bs-darkslide" )]
	[InlineData( "tailslide", true, "bs-darkslide" )]
	public void ADarkSideUpSlideIsADarkslide( string kind, bool noseToHeels, string expected )
	{
		Assert.Equal( expected, GrindNames.Graph( kind, true, noseToHeels, false, false, false, dark: true ) );
	}

	[Fact]
	public void AnUnknownNoseSideKeepsTheStraightNames()
	{
		Assert.Equal( "fs-board", GrindNames.Graph( "boardslide", true, null, false, false, false ) );
		Assert.Equal( "bs-5-o", GrindNames.Graph( "fivezero", false, null, false, false, false ) );
		Assert.Equal( "", GrindNames.Graph( "", true, null, false, false, false ) );
	}

	[Fact]
	public void TheGraphIsToldTheGrindsShapeAndItsApproach()
	{
		var facts = new StateFacts();
		var source = new RiderStateFacts();
		source.Fill( facts, new RiderAnimationInput
		{
			Grind = "fivezero", GrindFrontside = false, GrindNoseToHeels = false, GrindLow = true
		}, false, Frame );
		Assert.Equal( "bs-smith", facts.Grind );
		Assert.Equal( 1f, facts.Facts[ "is-grind-approach" ] );
		source.Fill( facts, new RiderAnimationInput { Grind = "fiftyfifty", GrindBackward = true }, false, Frame );
		Assert.Equal( "bf-fs-50-50", facts.Grind );
	}

	[Theory]
	[InlineData( "stalegrab", "grind-fs-grab" )]
	[InlineData( "mutegrab", "grind-bs-grab" )]
	[InlineData( "crailgrab", "grind-double-grab" )]
	public void GrabsOnARailAreTheGrindsOwn( string grab, string intent )
	{
		var facts = new StateFacts();
		new RiderStateFacts().Fill( facts, new RiderAnimationInput { Grind = "fiftyfifty", Grab = grab }, false, Frame );
		Assert.True( facts.Intents.ContainsKey( intent ) );
	}
}
