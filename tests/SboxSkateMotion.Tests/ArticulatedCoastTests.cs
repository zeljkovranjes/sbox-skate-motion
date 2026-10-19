using System.Numerics;
using SkateMotion.Core;
using SkateMotion.Core.Constraints;
using SkateMotion.Core.Vehicle;
using Xunit;
using Xunit.Abstractions;

namespace SboxSkateMotion.Tests;

public class ArticulatedCoastTests( ITestOutputHelper output )
{
	[Theory]
	[InlineData( 8f )]
	[InlineData( -8f )]
	public void NeutralTrucksRollStraightInBothDirections( float speed )
	{
		var settings = new SkateSettings();
		const float radius = 0.0285f;
		var origins = new Vector3[ 4 ];
		for ( var i = 0; i < 4; i++ )
			origins[ i ] = SkateWheels.Offset( i, settings ) - Vector3.UnitZ * (settings.RideHeight - radius);
		var board = new ArticulatedBoard( settings, origins, radius );
		var pilot = new BoardPilot( settings );
		board.Reset( Vector3.UnitZ * settings.RideHeight, Quaternion.Identity, Vector3.UnitX * speed, Vector3.Zero );
		board.Body( 7 ) = new DynamicBody { Rotation = Quaternion.Identity };
		var contacts = new CollisionContact[ 4 ];
		var lateral = 0f;
		var yaw = 0f;
		for ( var tick = 0; tick < 240 * 8; tick++ )
		{
			board.BeginStep();
			for ( var wheel = 0; wheel < 4; wheel++ )
			{
				var point = board.Body( wheel + 3 ).Position - Vector3.UnitZ * radius;
				contacts[ wheel ] = new( wheel + 3, 7, point, new Vector3( point.X, point.Y, 0f ),
					Vector3.UnitZ, Vector3.UnitX, Vector3.UnitY, 0.8f, 0.6f, 0f );
			}
			pilot.Apply( board, contacts, default, 1f / 240f );
			board.Step( contacts, 0f, 0f, 1f / 240f );
			lateral = Math.Max( lateral, Math.Abs( board.Deck.Position.Y ) );
			yaw = Math.Max( yaw, Math.Abs( MathF.Atan2( board.Deck.Forward.Y, board.Deck.Forward.X ) ) );
		}
		output.WriteLine( $"speed {speed}: lateral {lateral}, yaw {yaw}, final {board.Deck.Velocity}" );
		Assert.InRange( lateral, 0f, 0.02f );
		Assert.InRange( yaw, 0f, 0.02f );
		Assert.InRange( board.Deck.Velocity.Length(), Math.Abs( speed ) - 1.2f, Math.Abs( speed ) );
	}
}
