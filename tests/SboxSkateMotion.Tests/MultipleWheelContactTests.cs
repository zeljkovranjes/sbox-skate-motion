using System.Numerics;
using SkateMotion.Core;
using SkateMotion.Core.Constraints;
using SkateMotion.Core.Vehicle;
using Xunit;

namespace SboxSkateMotion.Tests;

public class MultipleWheelContactTests
{
	[Theory]
	[InlineData( 1f )]
	[InlineData( -1f )]
	public void MultipleSurfacesCountOneWheelAndKeepOpposedNormalsFinite( float direction )
	{
		var settings = new SkateSettings();
		var origins = new Vector3[ 4 ];
		for ( var i = 0; i < 4; i++ )
			origins[ i ] = SkateWheels.Offset( i, settings ) - Vector3.UnitZ * (settings.RideHeight - .0285f);
		var board = new ArticulatedBoard( settings, origins, .0285f );
		board.Reset( Vector3.UnitZ, Quaternion.Identity, Vector3.Zero, Vector3.Zero );
		var pilot = new BoardPilot( settings );
		pilot.SetRiderAttached( false );
		board.SetRiderAttached( false );
		var point = board.Body( 3 ).Position;
		var first = new CollisionContact( 3, 7, point, point, Vector3.UnitZ, Vector3.UnitX, Vector3.UnitY, .6f, .4f, 0f );
		var second = first with { BodyB = 8, Normal = Vector3.UnitZ * direction };
		var result = pilot.Apply( board, new[] { first, second }, default, 1f / 240f );
		Assert.Equal( 1, result.WheelCount );
		Assert.True( float.IsFinite( result.Force.LengthSquared() ) );
		Assert.True( float.IsFinite( result.Torque.LengthSquared() ) );
	}
}
