using System.Numerics;
using SkateMotion.Core;
using SkateMotion.Core.Constraints;
using SkateMotion.Core.Vehicle;
using Xunit;

namespace SboxSkateMotion.Tests;

public class AssemblyRecoveryTests
{
	[Fact]
	public void BuriedWheelsSeparateTogetherWithoutAddingKineticMotion()
	{
		var settings = new SkateSettings();
		const float radius = 0.0285f;
		var origins = new Vector3[ 4 ];
		for ( var i = 0; i < 4; i++ )
			origins[ i ] = SkateWheels.Offset( i, settings ) - Vector3.UnitZ * (settings.RideHeight - radius);
		var board = new ArticulatedBoard( settings, origins, radius );
		board.Reset( Vector3.UnitZ * 0.00635f, Quaternion.Identity, new Vector3( 2f, 1f, -0.5f ), Vector3.UnitZ );
		var original = board.Bodies.ToArray();
		var contacts = new CollisionContact[ 4 ];
		for ( var i = 0; i < 4; i++ )
		{
			var point = board.Body( i + 3 ).Position - Vector3.UnitZ * radius;
			contacts[ i ] = new( i + 3, 7, point, new Vector3( point.X, point.Y, 0f ),
				Vector3.UnitZ, Vector3.UnitX, Vector3.UnitY, 0.8f, 0.6f, 0f );
		}
		Assert.True( board.RecoverInitialOverlap( contacts ) );
		for ( var i = 0; i < 7; i++ )
		{
			Assert.Equal( original[ i ].Velocity, board.Body( i ).Velocity );
			Assert.Equal( original[ i ].AngularVelocity, board.Body( i ).AngularVelocity );
			Assert.Equal( original[ i ].Rotation, board.Body( i ).Rotation );
		}
		Assert.InRange( board.MaximumAnchorError(), 0f, 0.000001f );
		Assert.InRange( Math.Abs( board.Deck.Position.Z - settings.RideHeight ), 0f, 0.000001f );
	}

	[Fact]
	public void OverlapProjectionRespectsMultipleSurfaceNormals()
	{
		CollisionContact[] contacts =
		[
			new( 0, 7, new Vector3( -0.04f, 0f, -0.02f ), Vector3.Zero, Vector3.UnitZ, Vector3.UnitX, Vector3.UnitY, 0f, 0f, 0f ),
			new( 0, 8, new Vector3( -0.04f, 0f, -0.02f ), Vector3.Zero, Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, 0f, 0f, 0f )
		];
		Assert.InRange( Vector3.Distance( AssemblyRecovery.Translation( contacts ), new Vector3( 0.04f, 0f, 0.02f ) ), 0f, 0.000001f );
	}
}
