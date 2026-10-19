using System.Numerics;
using SkateMotion.Core;
using SkateMotion.Core.Constraints;
using SkateMotion.Core.Vehicle;
using Xunit;
using Xunit.Abstractions;

namespace SboxSkateMotion.Tests;

public class TruckSteeringLoadTests( ITestOutputHelper output )
{
	[Theory]
	[InlineData( 4f, -0.5f, 240 )]
	[InlineData( 4f, 0.5f, 240 )]
	[InlineData( 6f, -0.5f, 240 )]
	[InlineData( 6f, 0.5f, 240 )]
	[InlineData( 4f, -0.5f, 480 )]
	[InlineData( 4f, 0.5f, 480 )]
	[InlineData( 6f, -0.5f, 480 )]
	[InlineData( 6f, 0.5f, 480 )]
	public void LoadedTrucksHoldTheirTargetThroughAHighGripCarve( float speed, float steer, int frequency )
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
		var step = 1f / frequency;
		var maximumError = 0f;
		var maximumGap = 0f;
		var maximumEnergy = board.KineticEnergy();
		var initialEnergy = maximumEnergy;
		for ( var tick = 0; tick < frequency * 4; tick++ )
		{
			var time = tick * step;
			var input = time >= 0.5f && time < 1.5f ? new ControlInput( Steer: steer ) : default;
			var target = input.Steer * settings.TruckAngle / (1f + board.Deck.Velocity.Length() * settings.SteeringSpeedScale);
			board.BeginStep();
			for ( var wheel = 0; wheel < 4; wheel++ )
			{
				ref var body = ref board.Body( wheel + 3 );
				var point = body.Position - Vector3.UnitZ * radius;
				var axle = Vector3.Transform( Vector3.UnitY, body.Rotation );
				var tangent = Vector3.Normalize( Vector3.Cross( axle, Vector3.UnitZ ) );
				contacts[ wheel ] = new( wheel + 3, 7, point, new Vector3( point.X, point.Y, 0f ),
					Vector3.UnitZ, tangent, Vector3.Cross( Vector3.UnitZ, tangent ), 2f, 1.6f, 0f );
			}
			pilot.Apply( board, contacts, input, step );
			board.Step( contacts, target, 0f, step );
			maximumGap = Math.Max( maximumGap, board.MaximumAnchorError() );
			maximumEnergy = Math.Max( maximumEnergy, board.KineticEnergy() );
			if ( time >= 0.75f && time < 1.5f )
			{
				for ( var truck = 1; truck <= 2; truck++ )
				{
					var relative = Quaternion.Conjugate( board.Body( 0 ).Rotation ) * board.Body( truck ).Rotation;
					var forward = Vector3.Transform( Vector3.UnitX, relative );
					var angle = MathF.Atan2( forward.Y, forward.X );
					maximumError = Math.Max( maximumError, Math.Abs( angle - (truck == 1 ? target : -target) ) );
				}
			}
		}
		output.WriteLine( $"speed {speed}, steer {steer}, Hz {frequency}: final {board.Deck.Velocity.Length()}, target error {maximumError}, anchor gap {maximumGap}, peak energy {maximumEnergy / initialEnergy}" );
		Assert.InRange( maximumError, 0f, 0.06f );
		Assert.InRange( maximumGap, 0f, 0.01f );
		Assert.InRange( maximumEnergy / initialEnergy, 0f, 1.02f );
		Assert.InRange( board.Deck.Velocity.Length(), speed * 0.7f, speed );
	}
}
