using System.Numerics;
using SkateMotion.Core;
using SkateMotion.Core.Constraints;
using SkateMotion.Core.Vehicle;
using Xunit;
using Xunit.Abstractions;

namespace SboxSkateMotion.Tests;

public class ManualDynamicsTests( ITestOutputHelper output )
{
	[Theory]
	[InlineData( -0.45f, 4f, 30 )]
	[InlineData( 0.45f, 4f, 30 )]
	[InlineData( -0.45f, 4f, 60 )]
	[InlineData( 0.45f, 4f, 60 )]
	[InlineData( -0.45f, 4f, 120 )]
	[InlineData( 0.45f, 4f, 120 )]
	[InlineData( -0.45f, -4f, 60 )]
	[InlineData( 0.45f, -4f, 60 )]
	public void RiderBalancesOneAxleThenReturnsToFourWheels( float flick, float speed, int frequency )
	{
		var settings = new SkateSettings();
		const float radius = 0.0285f;
		var origins = new Vector3[ 4 ];
		for ( var i = 0; i < origins.Length; i++ )
			origins[ i ] = SkateWheels.Offset( i, settings ) - Vector3.UnitZ * (settings.RideHeight - radius);
		var board = new ArticulatedBoard( settings, origins, radius );
		var pilot = new BoardPilot( settings );
		board.Reset( Vector3.UnitZ * settings.RideHeight, Quaternion.Identity, Vector3.UnitX * speed, Vector3.Zero );
		board.Body( 7 ) = new DynamicBody { Rotation = Quaternion.Identity };
		var contacts = new CollisionContact[ 4 ];
		var expected = flick < 0f ? MotionMode.Manual : MotionMode.NoseManual;
		var balanced = 0;
		var holding = 0;
		var greatestPitch = 0f;
		var greatestLateral = 0f;
		var maximumGap = 0f;
		var result = default( PhysicsStep );
		for ( var tick = 0; tick < frequency * 6; tick++ )
		{
			var time = (float)tick / frequency;
			var input = time >= 0.5f && time < 4f ? new ControlInput( FlickY: flick ) : default;
			for ( var substep = 0; substep < 240 / frequency; substep++ )
			{
				const float step = 1f / 240f;
				board.BeginStep();
				for ( var wheel = 0; wheel < 4; wheel++ )
				{
					ref var body = ref board.Body( wheel + 3 );
					var point = body.Position - Vector3.UnitZ * radius;
					var axle = Vector3.Transform( Vector3.UnitY, body.Rotation );
					var tangent = Vector3.Normalize( Vector3.Cross( axle, Vector3.UnitZ ) );
					contacts[ wheel ] = new( wheel + 3, 7, point, new Vector3( point.X, point.Y, 0f ),
						Vector3.UnitZ, tangent, Vector3.Cross( Vector3.UnitZ, tangent ), 0.68f, 0.544f, 0f );
				}
				result = pilot.Apply( board, contacts, input, step );
				board.Step( contacts, 0f, 0f, step );
				Assert.Equal( TrickKind.None, result.Trick );
				Assert.Equal( Vector3.Zero, result.Impulse );
				Assert.Equal( Vector3.Zero, result.AngularImpulse );
				greatestLateral = Math.Max( greatestLateral, Math.Abs( board.Deck.Position.Y ) );
				maximumGap = Math.Max( maximumGap, board.MaximumAnchorError() );
				if ( time >= 1f && time < 4f )
				{
					holding++;
					var pitch = -Math.Sign( flick ) * MathF.Asin( board.Deck.Forward.Z );
					greatestPitch = Math.Max( greatestPitch, pitch );
					if ( result.Mode == expected && result.WheelCount == 2 && pitch > 0.08f ) balanced++;
				}
			}
		}
		output.WriteLine( $"{flick}, {speed}m/s, {frequency}Hz: held {(float)balanced / holding}, pitch {greatestPitch}, lateral {greatestLateral}, gap {maximumGap}, final speed {board.Deck.Velocity.Length()}" );
		Assert.InRange( (float)balanced / holding, 0.95f, 1f );
		Assert.InRange( greatestPitch, 0.1f, 0.22f );
		Assert.InRange( greatestLateral, 0f, 0.1f );
		Assert.InRange( maximumGap, 0f, 0.01f );
		Assert.Equal( MotionMode.Rolling, result.Mode );
		Assert.Equal( 4, result.WheelCount );
		Assert.InRange( Math.Abs( board.Deck.Forward.Z ), 0f, 0.01f );
		Assert.InRange( board.Deck.Position.Z, settings.RideHeight - 0.005f, settings.RideHeight + 0.005f );
		Assert.InRange( board.Deck.Velocity.Length(), 2.5f, 4f );
	}
}
