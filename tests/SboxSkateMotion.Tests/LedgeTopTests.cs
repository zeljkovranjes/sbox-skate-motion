using System;
using System.Numerics;
using SkateMotion.Core;
using SkateMotion.Core.Animation;
using SkateMotion.Core.Constraints;
using SkateMotion.Core.Grinding;
using SkateMotion.Core.Vehicle;
using Xunit;

namespace SboxSkateMotion.Tests;

public class LedgeTopTests
{
	private const float Radius = 0.0285f;
	private const float Height = 0.4f;
	private const float Step = 1f / 240f;
	private const int Surface = 7;

	[Theory]
	// The ledge's top lies toward -y of its edge along x. A tail slide with the deck's middle over the top is on the
	// ledge's top; over the street side, or on an edge with no top, it is not.
	[InlineData( 0.36f, -1f, true )]
	[InlineData( -0.36f, -1f, false )]
	[InlineData( 0.36f, 0f, false )]
	public void AnEndSlideWithTheDeckOverTheLedgesTopIsOnIt( float offset, float top, bool onTop )
	{
		var settings = SkateSettings.RecoveredDefaults;
		// Turned a quarter round, the deck's forward is +y: the edge crosses it at -offset from its middle, the tail.
		var across = Quaternion.CreateFromAxisAngle( Vector3.UnitZ, MathF.PI * 0.5f );
		var board = Create( settings, across, new Vector3( 0f, -offset, Height + 0.0125f + 0.002f ) );
		var edge = new RailSegment( new Vector3( -1f, 0f, Height ), new Vector3( 4f, 0f, Height ) ) { Top = new Vector3( 0f, top, 0f ) };
		var contacts = new CollisionContact[ ArticulatedBoard.ContactCapacity ];
		var count = 0;
		var grind = RailContacts.Build( board, new[] { edge }, Surface, settings.GrindFriction, contacts, ref count );
		Assert.True( grind.Kind is GrindKind.Noseslide or GrindKind.Tailslide );
		Assert.Equal( new Vector3( 0f, top, 0f ), grind.Top );
		Assert.Equal( onTop, GrindShape.Measure( board, grind ).OnLedgeTop );
	}

	[Fact]
	public void TheEdgeLookupKeepsTheLedgesTop()
	{
		var edge = new RailSegment( new Vector3( 1f, 2f, 0.4f ), new Vector3( 3f, 2f, 0.4f ) ) { Top = new Vector3( 0f, -1f, 0f ), HalfWidth = 0.02f };
		Assert.Equal( edge, RailEdgeSpace.ToWorld( RailEdgeSpace.ToLookup( edge ) ) );
	}

	[Theory]
	[InlineData( "tailslide", true, false, "fs-blunt" )]
	[InlineData( "noseslide", true, false, "bs-noseblunt" )]
	[InlineData( "tailslide", false, true, "fs-tailslide" )]
	public void AnEndSlideOnALedgesTopTakesTheBacksideBluntRoutes( string grind, bool onTop, bool noseToHeels, string name )
	{
		var facts = new StateFacts();
		new RiderStateFacts().Fill( facts, new RiderAnimationInput
		{
			Grind = grind, GrindFrontside = true, GrindNoseToHeels = noseToHeels, GrindOnLedgeTop = onTop, DeckUp = 1f
		}, false, 1f / 60f );
		Assert.Equal( name, facts.Grind );
		Assert.Equal( onTop ? 1f : 0f, facts.Facts[ "is-grind-blunting-backslash" ] );
	}

	private static ArticulatedBoard Create( SkateSettings settings, Quaternion rotation, Vector3 at )
	{
		var origins = new Vector3[ 4 ];
		for ( var i = 0; i < origins.Length; i++ )
			origins[ i ] = SkateWheels.Offset( i, settings ) - Vector3.UnitZ * (settings.RideHeight - Radius);
		var board = new ArticulatedBoard( settings, origins, Radius );
		board.Reset( at, rotation, Vector3.UnitX * 3f, Vector3.Zero );
		board.Body( Surface ) = new DynamicBody { Rotation = Quaternion.Identity };
		return board;
	}
}
