using System.Numerics;
using SkateMotion.Core;
using SkateMotion.Core.Constraints;
using SkateMotion.Core.Vehicle;
using Xunit;

namespace SboxSkateMotion.Tests;

public class BodyFlipLandingTests
{
	[Fact]
	public void TheLandingIsTheGroundBelowAsFarUnderTheDeckAsTheGroundItLeft()
	{
		// Off a 5 m roof, the deck 0.1 m over it, onto the street: the deck comes down 0.1 m over the street.
		Assert.Equal( 0.1f, BodyFlip.LandingHeight( 5.1f, 5f, 0f ), 4 );
		// Coming down on a higher ledge: over the ledge.
		Assert.Equal( 6.1f, BodyFlip.LandingHeight( 5.1f, 5f, 6f ), 4 );
	}

	[Fact]
	public void WithNoGroundSeenTheLandingIsTheTakeoffHeight()
	{
		Assert.Equal( 5.1f, BodyFlip.LandingHeight( 5.1f, null, 0f ) );
		Assert.Equal( 5.1f, BodyFlip.LandingHeight( 5.1f, 5f, null ) );
	}

	[Theory]
	[InlineData( true )]
	[InlineData( false )]
	public void RollingOffADropFlipsWhenTheGroundBelowIsCounted( bool groundSeen )
	{
		// Rolling at 2 m/s on a roof 5 m up, then off its edge with a grab and a lean toward the toes. Down to the street
		// there is about a second of flight; back to the roof's height there is none.
		const float roof = 5f;
		var settings = SkateSettings.RecoveredDefaults;
		const float radius = 0.0285f;
		var origins = new Vector3[ 4 ];
		for ( var i = 0; i < 4; i++ )
			origins[ i ] = SkateWheels.Offset( i, settings ) - Vector3.UnitZ * (settings.RideHeight - radius);
		var board = new ArticulatedBoard( settings, origins, radius );
		var pilot = new BoardPilot( settings );
		board.Reset( Vector3.UnitZ * (roof + settings.RideHeight), Quaternion.Identity, Vector3.UnitX * 2f, Vector3.Zero );
		board.Body( 7 ) = new DynamicBody { Rotation = Quaternion.Identity };
		var contacts = new CollisionContact[ 4 ];
		var flipped = 0;
		const float step = 1f / 240f;
		for ( var tick = 0; tick < 240 * 0.8f; tick++ )
		{
			var onRoof = tick < 240 * 0.2f;
			board.BeginStep();
			var count = 0;
			if ( onRoof )
			{
				for ( var wheel = 0; wheel < 4; wheel++ )
				{
					var point = board.Body( wheel + 3 ).Position - Vector3.UnitZ * radius;
					contacts[ count++ ] = new( wheel + 3, 7, point, new Vector3( point.X, point.Y, roof ),
						Vector3.UnitZ, Vector3.UnitX, Vector3.UnitY, 0.8f, 0.6f, 0f );
				}
			}
			var input = onRoof ? default : new ControlInput( Lean: 1f, Grab: true );
			float? ground = onRoof ? roof : groundSeen ? 0f : null;
			var result = pilot.Apply( board, contacts.AsSpan( 0, count ), input, step, groundBelow: ground );
			board.Step( contacts.AsSpan( 0, count ), 0f, 0f, step );
			if ( result.BodyFlip == 1 )
				flipped++;
		}
		if ( groundSeen )
			Assert.True( flipped > 0 );
		else
			Assert.Equal( 0, flipped );
	}
}
