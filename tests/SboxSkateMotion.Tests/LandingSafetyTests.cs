using System.Numerics;
using SkateMotion.Core;
using SkateMotion.Core.Constraints;
using SkateMotion.Core.Vehicle;
using Xunit;

namespace SboxSkateMotion.Tests;

public class LandingSafetyTests
{
	[Theory]
	[InlineData( 0f, 0f, false )]
	[InlineData( 4f, 0f, false )]
	[InlineData( 8f, 0f, false )]
	[InlineData( 9f, 0f, true )]
	[InlineData( 9f, 0.02f, true )]
	[InlineData( 9f, 0.2f, false )]
	[InlineData( -9f, 0f, false )]
	public void NormalImpactUsesApproachSpeedAndCurrentStepReach( float speed, float gap, bool bail )
	{
		var board = Board( Quaternion.Identity, -Vector3.UnitZ * speed );
		var contact = Contact( Vector3.UnitZ, gap );
		Assert.Equal( bail, LandingSafety.RequiresBail( board, new[] { contact }, 8f, 1f / 240f ) );
	}

	[Theory]
	[InlineData( 0f, false )]
	[InlineData( 1f, true )]
	[InlineData( -1f, false )]
	public void InvertedContactRequiresAnActualClosingImpact( float speed, bool bail )
	{
		var board = Board( Quaternion.CreateFromAxisAngle( Vector3.UnitX, MathF.PI ), -Vector3.UnitZ * speed );
		Assert.Equal( bail, LandingSafety.RequiresBail( board, new[] { Contact( Vector3.UnitZ ) }, 8f, 1f / 240f ) );
	}

	[Fact]
	public void MovingSurfaceVelocityCancelsAbsoluteFallSpeed()
	{
		var board = Board( Quaternion.Identity, -Vector3.UnitZ * 12f );
		board.Body( 7 ).Velocity = -Vector3.UnitZ * 12f;
		Assert.False( LandingSafety.RequiresBail( board, new[] { Contact( Vector3.UnitZ ) }, 8f, 1f / 240f ) );
	}

	[Fact]
	public void HighTangentialSpeedDoesNotCountAsImpact()
	{
		var board = Board( Quaternion.Identity, Vector3.UnitX * 30f );
		Assert.False( LandingSafety.RequiresBail( board, new[] { Contact( Vector3.UnitZ ) }, 8f, 1f / 240f ) );
		Assert.True( LandingSafety.RequiresBail( board, new[] { Contact( -Vector3.UnitX ) }, 8f, 1f / 240f ) );
	}

	[Fact]
	public void CrashUnloadsBeforeSolvingAndStaysBailedAfterDetachment()
	{
		var board = Board( Quaternion.Identity, Vector3.UnitX * 12f );
		var pilot = new BoardPilot( new() );
		var velocity = board.Deck.Velocity;
		var result = pilot.Apply( board, new[] { Contact( -Vector3.UnitX ) }, new( Push: 1f, Steer: 1f ), 1f / 240f );
		Assert.Equal( MotionMode.Bailed, result.Mode );
		Assert.Equal( Vector3.Zero, result.Torque );
		Assert.Equal( velocity, board.Deck.Velocity );
		Assert.InRange( 1f / board.Body( 0 ).InverseMass, 1.97f, 1.99f );
		pilot.SetRiderAttached( false );
		Assert.Equal( MotionMode.Bailed, pilot.Apply( board, [], default, 1f / 240f ).Mode );
		pilot.SetRiderAttached( true );
		Assert.Equal( MotionMode.Airborne, pilot.Apply( board, [], default, 1f / 240f ).Mode );
	}

	[Fact]
	public void UnattendedBoardDoesNotRequestARiderCrash()
	{
		var board = Board( Quaternion.Identity, Vector3.UnitX * 12f );
		var pilot = new BoardPilot( new() );
		pilot.SetRiderAttached( false );
		Assert.NotEqual( MotionMode.Bailed,
			pilot.Apply( board, new[] { Contact( -Vector3.UnitX ) }, default, 1f / 240f ).Mode );
	}

	private static ArticulatedBoard Board( Quaternion rotation, Vector3 velocity )
	{
		var settings = new SkateSettings();
		var origins = new Vector3[ 4 ];
		for ( var i = 0; i < 4; i++ )
			origins[ i ] = SkateWheels.Offset( i, settings ) - Vector3.UnitZ * (settings.RideHeight - 0.0285f);
		var board = new ArticulatedBoard( settings, origins, 0.0285f );
		board.Reset( Vector3.Zero, rotation, velocity, Vector3.Zero );
		return board;
	}

	private static CollisionContact Contact( Vector3 normal, float gap = 0f ) => new(
		0, 7, Vector3.Zero, -normal * gap, normal,
		Vector3.UnitY, Vector3.Cross( normal, Vector3.UnitY ), 0.6f, 0.5f, 0f );
}
