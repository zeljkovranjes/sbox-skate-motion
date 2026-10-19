using System.Numerics;
using SkateMotion.Core;
using SkateMotion.Core.Constraints;
using SkateMotion.Core.Vehicle;
using Xunit;
using Xunit.Abstractions;

namespace SboxSkateMotion.Tests;

public class BoardPilotTests( ITestOutputHelper output )
{
	[Theory]
	[InlineData( 30 )]
	[InlineData( 60 )]
	[InlineData( 120 )]
	public void RiderPushesBrakesAndPopsPhysicalWheelAssembly( int frequency )
	{
		var settings = new SkateSettings();
		const float radius = 0.0285f;
		var origins = new Vector3[ 4 ];
		for ( var i = 0; i < 4; i++ )
			origins[ i ] = SkateWheels.Offset( i, settings ) - Vector3.UnitZ * (settings.RideHeight - radius);
		var board = new ArticulatedBoard( settings, origins, radius );
		var pilot = new BoardPilot( settings );
		board.Reset( Vector3.UnitZ * settings.RideHeight, Quaternion.Identity, Vector3.Zero, Vector3.Zero );
		board.Body( 7 ) = new DynamicBody { Rotation = Quaternion.Identity };
		var contacts = new CollisionContact[ 4 ];
		var peakSpeed = 0f;
		var wheelSpin = 0f;
		var stoppedSpeed = 0f;
		var popHeight = 0f;
		var pops = 0;
		for ( var tick = 0; tick < frequency * 7; tick++ )
		{
			var time = (float)tick / frequency;
			var input = time switch
			{
				< 0.5f => default,
				< 2.5f => new ControlInput( Push: 1f ),
				< 4.5f => new ControlInput( Brake: 1f ),
				< 4.9f => new ControlInput( FlickY: -1f ),
				< 5f => new ControlInput( FlickY: 1f ),
				_ => default
			};
			for ( var substep = 0; substep < 240 / frequency; substep++ )
			{
				const float step = 1f / 240f;
				board.BeginStep();
				for ( var wheel = 0; wheel < 4; wheel++ )
				{
					var point = board.Body( wheel + 3 ).Position - Vector3.UnitZ * radius;
					contacts[ wheel ] = new( wheel + 3, 7, point, new Vector3( point.X, point.Y, 0f ),
						Vector3.UnitZ, Vector3.UnitX, Vector3.UnitY, 0.8f, 0.6f, 0f );
				}
				var result = pilot.Apply( board, contacts, input, step );
				board.Step( contacts, 0f, 0f, step );
				if ( result.Trick != TrickKind.None ) pops++;
				if ( time < 2.5f )
				{
					peakSpeed = Math.Max( peakSpeed, board.Deck.Velocity.Length() );
					wheelSpin = Math.Max( wheelSpin, Math.Abs( board.Body( 3 ).AngularVelocity.Y ) );
				}
				if ( time >= 4.3f && time < 4.5f ) stoppedSpeed = Math.Max( stoppedSpeed, board.Deck.Velocity.Length() );
				if ( time >= 4.9f ) popHeight = Math.Max( popHeight, board.Deck.Position.Z );
			}
		}
		output.WriteLine( $"{frequency} Hz: peak {peakSpeed}, wheel spin {wheelSpin}, stopped {stoppedSpeed}, height {popHeight}, pops {pops}" );
		Assert.InRange( peakSpeed, 2f, 9f );
		Assert.InRange( wheelSpin, 10f, 400f );
		Assert.InRange( stoppedSpeed, 0f, 0.1f );
		Assert.InRange( popHeight, 0.3f, 1.5f );
		Assert.Equal( 1, pops );
		Assert.InRange( board.Deck.Position.Z, settings.RideHeight - 0.01f, settings.RideHeight + 0.01f );
	}
}
