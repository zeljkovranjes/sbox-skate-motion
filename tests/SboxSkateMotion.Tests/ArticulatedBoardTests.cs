using System.Numerics;
using SkateMotion.Core;
using SkateMotion.Core.Constraints;
using SkateMotion.Core.Vehicle;
using Xunit;
using Xunit.Abstractions;

namespace SboxSkateMotion.Tests;

public class ArticulatedBoardTests( ITestOutputHelper output )
{
	[Theory]
	[InlineData( 30, true )]
	[InlineData( 60, true )]
	[InlineData( 120, true )]
	[InlineData( 30, false )]
	[InlineData( 60, false )]
	[InlineData( 120, false )]
	public void SevenBodyBoardSettlesWithConnectedTrucksAndWheels( int frequency, bool rider )
	{
		var settings = new SkateSettings();
		const float radius = 0.0285f;
		Vector3[] origins =
		[
			new( 0.26f, 0.095f, -settings.RideHeight + radius ),
			new( 0.26f, -0.095f, -settings.RideHeight + radius ),
			new( -0.26f, 0.095f, -settings.RideHeight + radius ),
			new( -0.26f, -0.095f, -settings.RideHeight + radius )
		];
		var board = new ArticulatedBoard( settings, origins, radius );
		board.SetRiderAttached( rider );
		board.Reset( new Vector3( 0f, 0f, settings.RideHeight + 0.2f ), Quaternion.Identity, Vector3.Zero, Vector3.Zero );
		board.Body( 7 ) = new DynamicBody { Rotation = Quaternion.Identity };
		var contacts = new CollisionContact[ 4 ];
		var maximumGap = 0f;
		var step = 1f / 240f;
		for ( var tick = 0; tick < frequency * 4; tick++ )
		{
			for ( var substep = 0; substep < 240 / frequency; substep++ )
			{
				board.BeginStep();
				for ( var wheel = 0; wheel < 4; wheel++ )
				{
					var point = board.Body( wheel + 3 ).Position - Vector3.UnitZ * radius;
					contacts[ wheel ] = new CollisionContact( wheel + 3, 7, point,
						new Vector3( point.X, point.Y, 0f ), Vector3.UnitZ, Vector3.UnitX, Vector3.UnitY, 0.8f, 0.6f, 0f );
				}
				board.Step( contacts, 0f, 0f, step );
				maximumGap = Math.Max( maximumGap, board.MaximumAnchorError() );
				foreach ( var body in board.Bodies )
					Assert.True( float.IsFinite( body.Position.LengthSquared() ) && float.IsFinite( body.Rotation.LengthSquared() ) );
			}
		}
		var deck = board.Body( 0 );
		output.WriteLine( $"{frequency} Hz rider {rider}: maximum gap {maximumGap:R}, deck height {deck.Position.Z:R}, velocity {deck.Velocity}" );
		Assert.InRange( maximumGap, 0f, 0.02f );
		Assert.InRange( Math.Abs( deck.Position.Z - settings.RideHeight ), 0f, 0.01f );
		Assert.InRange( deck.Velocity.Length(), 0f, 0.05f );
		Assert.InRange( Vector3.Dot( Vector3.Transform( Vector3.UnitZ, deck.Rotation ), Vector3.UnitZ ), 0.99f, 1.00001f );
	}
}
