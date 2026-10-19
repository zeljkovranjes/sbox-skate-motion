using System.Numerics;
using SkateMotion.Core;
using SkateMotion.Core.Vehicle;
using Xunit;

namespace SboxSkateMotion.Tests;

public class ArticulatedTelemetryTests
{
	[Theory]
	[InlineData( true )]
	[InlineData( false )]
	public void TranslationalEnergyIncludesTheWholeAssembly( bool attached )
	{
		var board = CreateBoard();
		board.SetRiderAttached( attached );
		board.Reset( new Vector3( 12f, -4f, 8f ), Quaternion.Identity,
			new Vector3( 3f, 4f, 0f ), Vector3.Zero );
		var mass = attached ? new SkateSettings().Mass : new SkateSettings().BoardMass;
		Assert.InRange( Math.Abs( board.KineticEnergy() - 0.5f * mass * 25f ), 0f, 0.0002f );
	}

	[Fact]
	public void RotatingTheWorldFramePreservesAssemblyEnergy()
	{
		var board = CreateBoard();
		var velocity = new Vector3( 2f, -3f, 1f );
		var angular = new Vector3( 7f, 2f, -4f );
		board.Reset( Vector3.Zero, Quaternion.Identity, velocity, angular );
		var energy = board.KineticEnergy();
		var rotation = Quaternion.CreateFromYawPitchRoll( 1.2f, -0.7f, 0.4f );
		board.Reset( new Vector3( 40f, 20f, -3f ), rotation,
			Vector3.Transform( velocity, rotation ), Vector3.Transform( angular, rotation ) );
		Assert.InRange( Math.Abs( board.KineticEnergy() - energy ), 0f, 0.001f );
	}

	private static ArticulatedBoard CreateBoard()
	{
		var settings = new SkateSettings();
		const float radius = 0.0285f;
		var origins = new Vector3[ 4 ];
		for ( var i = 0; i < origins.Length; i++ )
			origins[ i ] = SkateWheels.Offset( i, settings ) - Vector3.UnitZ * (settings.RideHeight - radius);
		return new( settings, origins, radius );
	}
}
