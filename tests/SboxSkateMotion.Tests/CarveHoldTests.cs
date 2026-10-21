using System.Numerics;
using SkateMotion.Core;
using SkateMotion.Core.Constraints;
using SkateMotion.Core.Vehicle;
using Xunit;
using Xunit.Abstractions;

namespace SboxSkateMotion.Tests;

public class CarveHoldTests( ITestOutputHelper output )
{
	private const float Radius = 0.0285f;

	// A full-steer carve from 6.91 m/s on flat ground (the lab's keyboard steer after a push). At kickTime the deck is
	// given extra spin, as the contacts' grip giving way did in the lab (the turn ran from -40 to -100 deg/s); the
	// steering is let go at 1.2 s. Returns the spin (deg/s) at 0.3 s after the kick, still steering, and 0.4 s after the
	// steering was let go, and the speed at the end.
	private static (float Held, float Released, float Speed) Carve( float kick, ITestOutputHelper output, float frameTime )
	{
		var settings = SkateSettings.RecoveredDefaults;
		var origins = new Vector3[ 4 ];
		for ( var i = 0; i < 4; i++ )
			origins[ i ] = SkateWheels.Offset( i, settings ) - Vector3.UnitZ * (settings.RideHeight - Radius);
		var board = new ArticulatedBoard( settings, origins, Radius );
		var pilot = new BoardPilot( settings );
		var steering = new SteeringResponse();
		board.Reset( Vector3.UnitZ * settings.RideHeight, Quaternion.Identity, Vector3.UnitX * 6.91f, Vector3.Zero );
		board.Body( 7 ) = new DynamicBody { Rotation = Quaternion.Identity };
		var contacts = new CollisionContact[ 4 ];
		var time = 0f;
		var kicked = false;
		float held = 0f, released = 0f;
		var steps = Math.Max( 1, (int)MathF.Ceiling( frameTime * 240f ) );
		var delta = frameTime / steps;
		while ( time < 1.7f )
		{
			for ( var s = 0; s < steps; s++ )
			{
				var input = time >= 0.2f && time < 1.2f ? new ControlInput( Steer: -1f ) : default;
				if ( !kicked && time >= 0.7f )
				{
					// Extra spin on every part, the way the wheels letting go spins the whole board.
					for ( var part = 0; part < ArticulatedBoard.DynamicCount; part++ )
						board.Body( part ).AngularVelocity += Vector3.UnitZ * kick;
					kicked = true;
				}
				board.BeginStep();
				for ( var wheel = 0; wheel < 4; wheel++ )
				{
					ref var body = ref board.Body( wheel + 3 );
					var point = body.Position - Vector3.UnitZ * Radius;
					var axle = Vector3.Transform( Vector3.UnitY, body.Rotation );
					var tangent = Vector3.Normalize( Vector3.Cross( axle, Vector3.UnitZ ) );
					contacts[ wheel ] = new( wheel + 3, 7, point, new Vector3( point.X, point.Y, 0f ),
						Vector3.UnitZ, tangent, Vector3.Cross( Vector3.UnitZ, tangent ), 0.85f, 0.68f, 0f );
				}
				pilot.Apply( board, contacts, input, delta );
				board.Step( contacts, steering.Angle( settings, input.Steer, board.Deck.Velocity.Length(), false, false, delta ), 0f, delta );
				time += delta;
			}
			var yaw = board.Deck.AngularVelocity.Z * 180f / MathF.PI;
			if ( MathF.Abs( time - 1.0f ) < frameTime * 0.5f )
				held = yaw;
			if ( MathF.Abs( time - 1.6f ) < frameTime * 0.5f )
				released = yaw;
		}
		output.WriteLine( $"kick {kick} frame {frameTime:F4}: spin {held:F1} deg/s 0.3 s after the kick, {released:F1} deg/s 0.4 s after letting go, speed {board.Deck.Velocity.Length():F2} m/s" );
		return (held, released, board.Deck.Velocity.Length());
	}

	[Theory]
	[InlineData( 1f / 90f )]
	[InlineData( 1f / 60f )]
	[InlineData( 1f / 30f )]
	public void ACarveThatStartsToSpinOutComesBackToItsLine( float frameTime )
	{
		var plain = Carve( 0f, output, frameTime );
		var kicked = Carve( -1.0f, output, frameTime );
		// Still steering, the turn is back to the carve's (about -40 deg/s), not running away past -60.
		Assert.InRange( kicked.Held, plain.Held - 12f, plain.Held + 12f );
		// Let go, the board stops turning as an unkicked one does, instead of spinning on for a second.
		Assert.InRange( kicked.Released, -3f, 3f );
		// And it keeps its speed, not scrubbed off in a skid.
		Assert.True( kicked.Speed > plain.Speed - 0.3f, $"{kicked.Speed} vs {plain.Speed}" );
	}
}
