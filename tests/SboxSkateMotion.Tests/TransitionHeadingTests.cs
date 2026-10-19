using System.Numerics;
using SkateMotion.Core;
using SkateMotion.Core.Constraints;
using SkateMotion.Core.Vehicle;
using Xunit;
using Xunit.Abstractions;

namespace SboxSkateMotion.Tests;

public class TransitionHeadingTests( ITestOutputHelper output )
{
	private const float Radius = 0.0285f;
	private const float RampRadius = 6.096f;
	private const float RampStart = 2.54f;
	private const float Step = 1f / 240f;

	[Theory]
	[InlineData( 0 )]
	[InlineData( 16 )]
	public void NeutralQuarterpipeReturnKeepsHeading( int facets )
	{
		var (heading, lateral, apex) = Ride( facets );
		output.WriteLine( $"facets {facets}: heading change {heading} deg, lateral {lateral} m, apex {apex} m" );
		Assert.True( apex > 1f, "The board must climb the transition." );
		Assert.True( MathF.Abs( heading ) < 0.25f, $"Heading changed {heading} degrees without steering." );
	}

	private static (float Heading, float Lateral, float Apex) Ride( int facets )
	{
		var settings = new SkateSettings();
		var origins = new Vector3[ 4 ];
		for ( var i = 0; i < origins.Length; i++ )
			origins[ i ] = SkateWheels.Offset( i, settings ) - Vector3.UnitZ * (settings.RideHeight - Radius);
		var board = new ArticulatedBoard( settings, origins, Radius );
		var pilot = new BoardPilot( settings );
		board.Reset( Vector3.UnitZ * (settings.RideHeight + 0.046f), Quaternion.Identity, Vector3.UnitX * 8f, Vector3.Zero );
		board.Body( 7 ) = new DynamicBody { Rotation = Quaternion.Identity };
		var contacts = new CollisionContact[ 4 ];
		var apex = 0f;
		var lateral = 0f;
		for ( var substep = 0; substep < 240 * 5; substep++ )
		{
			board.BeginStep();
			var count = RampContacts( board, contacts, facets );
			pilot.Apply( board, contacts.AsSpan( 0, count ), new ControlInput(), Step );
			board.Step( contacts.AsSpan( 0, count ), 0f, 0f, Step );
			apex = MathF.Max( apex, board.Deck.Position.Z );
			lateral = MathF.Max( lateral, MathF.Abs( board.Deck.Position.Y ) );
			if ( apex > 1f && board.Deck.Position.X < 0f )
				break;
		}
		var forward = Vector3.Transform( Vector3.UnitX, board.Deck.Rotation );
		var heading = MathF.Atan2( forward.Y, forward.X ) * 180f / MathF.PI;
		return (heading, lateral, apex);
	}

	// Flat floor up to RampStart, then a concave quarterpipe along +X, smooth or split into flat facets
	// over its first quarter turn like the lab mesh.
	private static int RampContacts( ArticulatedBoard board, CollisionContact[] contacts, int facets )
	{
		var count = 0;
		var centre = new Vector3( RampStart, 0f, RampRadius );
		for ( var wheel = 0; wheel < 4; wheel++ )
		{
			ref var body = ref board.Body( wheel + 3 );
			var normal = Vector3.UnitZ;
			var distance = body.Position.Z;
			if ( body.Position.X > RampStart )
			{
				var radial = new Vector3( body.Position.X - centre.X, 0f, body.Position.Z - centre.Z );
				var angle = MathF.Atan2( radial.X, -radial.Z );
				if ( facets > 0 )
				{
					var width = MathF.PI / 2f / facets;
					var middle = (MathF.Floor( Math.Clamp( angle, 0f, MathF.PI / 2f - 1e-4f ) / width ) + 0.5f) * width;
					normal = new Vector3( -MathF.Sin( middle ), 0f, MathF.Cos( middle ) );
					distance = Vector3.Dot( new Vector3( body.Position.X, 0f, body.Position.Z ) - centre, normal )
						+ RampRadius * MathF.Cos( width / 2f );
				}
				else
				{
					normal = -radial / radial.Length();
					distance = RampRadius - radial.Length();
				}
			}
			if ( distance > Radius + 0.02f )
				continue;
			var surface = body.Position - normal * distance;
			var axle = Vector3.Transform( Vector3.UnitY, body.Rotation );
			var tangent = Vector3.Normalize( Vector3.Cross( axle, normal ) );
			contacts[ count++ ] = new CollisionContact( wheel + 3, 7, body.Position - normal * Radius, surface, normal,
				tangent, Vector3.Cross( normal, tangent ), 0.68f, 0.544f, 0f ).WithProjectedSurfacePoint();
		}
		return count;
	}
}
