using System.Numerics;
using SkateMotion.Core;
using SkateMotion.Core.Constraints;
using SkateMotion.Core.Vehicle;
using Xunit;
using Xunit.Abstractions;

namespace SboxSkateMotion.Tests;

public class PumpTests( ITestOutputHelper output )
{
	private const float Radius = 0.0285f;
	private const float BowlRadius = 3f;
	private const float Step = 1f / 240f;

	[Fact]
	public void PumpingOnFlatGroundAddsNothing()
	{
		var stroke = new PumpStroke();
		for ( var i = 0; i < 240; i++ )
			Assert.Equal( 0f, stroke.Acceleration( true, 5f, 0f, Step ) );
	}

	[Fact]
	public void OneStrokeLastsItsDurationAndNeedsRelease()
	{
		var stroke = new PumpStroke();
		var active = 0;
		for ( var i = 0; i < 240; i++ )
			if ( stroke.Acceleration( true, 5f, 0.5f, Step ) > 0f ) active++;
		Assert.InRange( active * Step, PumpStroke.Duration - Step, PumpStroke.Duration + Step );
		Assert.Equal( 0f, stroke.Acceleration( false, 5f, 0.5f, Step ) );
		Assert.True( stroke.Acceleration( true, 5f, 0.5f, Step ) > 0f );
	}

	[Theory]
	[InlineData( 30 )]
	[InlineData( 60 )]
	[InlineData( 120 )]
	public void PumpingThroughTheBowlBottomClimbsHigherThanCoasting( int frequency )
	{
		var coasting = Ride( frequency, pump: false );
		var pumping = Ride( frequency, pump: true );
		output.WriteLine( $"{frequency}Hz: coasting peak {coasting} m, pumping peak {pumping} m" );
		Assert.True( pumping > coasting + 0.1f, "Pumping through transitions must gain height." );
		Assert.True( pumping < 2.5f, "Pumping must stay bounded." );
	}

	private static float Ride( int frequency, bool pump )
	{
		var settings = new SkateSettings();
		var origins = new Vector3[ 4 ];
		for ( var i = 0; i < origins.Length; i++ )
			origins[ i ] = SkateWheels.Offset( i, settings ) - Vector3.UnitZ * (settings.RideHeight - Radius);
		var board = new ArticulatedBoard( settings, origins, Radius );
		var pilot = new BoardPilot( settings );
		board.Reset( Vector3.UnitZ * (settings.RideHeight + 0.046f), Quaternion.Identity, Vector3.UnitX * 3f, Vector3.Zero );
		board.Body( 7 ) = new DynamicBody { Rotation = Quaternion.Identity };
		var contacts = new CollisionContact[ 4 ];
		var peak = 0f;
		for ( var tick = 0; tick < frequency * 8; tick++ )
		{
			for ( var substep = 0; substep < 240 / frequency; substep++ )
			{
				board.BeginStep();
				var count = BowlContacts( board, contacts );
				// Extend through the bottom of the bowl, where the centripetal load is largest.
				var input = new ControlInput( Pump: pump && MathF.Abs( board.Deck.Position.X ) < 0.6f );
				pilot.Apply( board, contacts.AsSpan( 0, count ), input, Step );
				board.Step( contacts.AsSpan( 0, count ), 0f, 0f, Step );
				if ( tick > frequency * 2 )
					peak = Math.Max( peak, board.Deck.Position.Z );
			}
		}
		return peak;
	}

	// Concave cylinder along Y with its lowest line on the floor at x = 0.
	private static int BowlContacts( ArticulatedBoard board, CollisionContact[] contacts )
	{
		var count = 0;
		var axis = new Vector3( 0f, 0f, BowlRadius );
		for ( var wheel = 0; wheel < 4; wheel++ )
		{
			ref var body = ref board.Body( wheel + 3 );
			var radial = new Vector3( body.Position.X, 0f, body.Position.Z - BowlRadius );
			var distance = radial.Length();
			if ( BowlRadius - distance > Radius + 0.02f )
				continue;
			var normal = -radial / distance;
			var surface = new Vector3( 0f, body.Position.Y, 0f ) + axis + radial / distance * BowlRadius;
			var axle = Vector3.Transform( Vector3.UnitY, body.Rotation );
			var tangent = Vector3.Normalize( Vector3.Cross( axle, normal ) );
			contacts[ count++ ] = new CollisionContact( wheel + 3, 7, body.Position - normal * Radius, surface, normal,
				tangent, Vector3.Cross( normal, tangent ), 0.68f, 0.544f, 0f ).WithProjectedSurfacePoint();
		}
		return count;
	}
}
