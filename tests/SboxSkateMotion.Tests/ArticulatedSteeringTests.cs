using System.Numerics;
using SkateMotion.Core;
using SkateMotion.Core.Constraints;
using SkateMotion.Core.Vehicle;
using Xunit;
using Xunit.Abstractions;

namespace SboxSkateMotion.Tests;

public class ArticulatedSteeringTests( ITestOutputHelper output )
{
	[Theory]
	[InlineData( -0.25f )]
	[InlineData( 0.25f )]
	public void GentleCarveRetainsSpeedAndTruckTargets( float steer )
	{
		var settings = new SkateSettings();
		const float radius = 0.0285f;
		var origins = new Vector3[ 4 ];
		for ( var i = 0; i < 4; i++ )
			origins[ i ] = SkateWheels.Offset( i, settings ) - Vector3.UnitZ * (settings.RideHeight - radius);
		var board = new ArticulatedBoard( settings, origins, radius );
		var pilot = new BoardPilot( settings );
		board.Reset( Vector3.UnitZ * settings.RideHeight, Quaternion.Identity, Vector3.UnitX * 4f, Vector3.Zero );
		board.Body( 7 ) = new DynamicBody { Rotation = Quaternion.Identity };
		var contacts = new CollisionContact[ 4 ];
		var gap = 0f;
		var steeringError = 0f;
		var turnAngle = 0f;
		for ( var tick = 0; tick < 240 * 5; tick++ )
		{
			var input = tick >= 120 && tick < 600 ? new ControlInput( Steer: steer ) : default;
			var target = input.Steer * settings.TruckAngle / (1f + board.Deck.Velocity.Length() * settings.SteeringSpeedScale);
			board.BeginStep();
			for ( var wheel = 0; wheel < 4; wheel++ )
			{
				ref var body = ref board.Body( wheel + 3 );
				var point = body.Position - Vector3.UnitZ * radius;
				var axle = Vector3.Transform( Vector3.UnitY, body.Rotation );
				var tangent = Vector3.Normalize( Vector3.Cross( axle, Vector3.UnitZ ) );
				contacts[ wheel ] = new( wheel + 3, 7, point, new Vector3( point.X, point.Y, 0f ),
					Vector3.UnitZ, tangent, Vector3.Cross( Vector3.UnitZ, tangent ), 0.85f, 0.68f, 0f );
			}
			pilot.Apply( board, contacts, input, 1f / 240f );
			board.Step( contacts, target, 0f, 1f / 240f );
			gap = Math.Max( gap, board.MaximumAnchorError() );
			var front = RelativeYaw( board, 1 );
			var rear = RelativeYaw( board, 2 );
			if ( tick >= 240 && tick < 600 )
				steeringError = Math.Max( steeringError, Math.Max( Math.Abs( front - target ), Math.Abs( rear + target ) ) );
			if ( tick == 599 )
				turnAngle = MathF.Atan2( board.Deck.Forward.Y, board.Deck.Forward.X );
			if ( tick % 120 == 0 )
				output.WriteLine( $"t {tick / 240f:F1} speed {board.Deck.Velocity.Length():F4} target {target:F5} front {front:F5} rear {rear:F5} yaw {board.Deck.AngularVelocity.Z:F4} gap {board.MaximumAnchorError():F6}" );
		}
		output.WriteLine( $"final speed {board.Deck.Velocity.Length():R}, turn {turnAngle:R}, steering error {steeringError:R}, max gap {gap:R}" );
		Assert.InRange( board.Deck.Velocity.Length(), 2.8f, 4f );
		Assert.True( turnAngle * steer > 0.1f );
		Assert.InRange( steeringError, 0f, 0.03f );
		Assert.InRange( gap, 0f, 0.01f );
	}

	private static float RelativeYaw( ArticulatedBoard board, int body )
	{
		var rotation = Quaternion.Conjugate( board.Body( 0 ).Rotation ) * board.Body( body ).Rotation;
		var forward = Vector3.Transform( Vector3.UnitX, rotation );
		return MathF.Atan2( forward.Y, forward.X );
	}
}
