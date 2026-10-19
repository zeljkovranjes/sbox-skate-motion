using System.Numerics;
using SkateMotion.Core;
using SkateMotion.Core.Constraints;
using SkateMotion.Core.Vehicle;
using Xunit;

namespace SboxSkateMotion.Tests;

public class ManualInputTests
{
	[Theory]
	[InlineData( 0f, 0f )]
	[InlineData( 0f, 0.19f )]
	[InlineData( 0f, 0.65f )]
	[InlineData( 0f, 1f )]
	[InlineData( 0f, -1f )]
	[InlineData( 0.8f, 0.45f )]
	public void NeutralAndFullFlickGesturesDoNotRequestManualBalance( float x, float y )
	{
		var (board, contacts, settings) = FlatBoard();
		var manual = new ManualBalance();
		Assert.False( manual.TryBalance( board, contacts, new( FlickX: x, FlickY: y ),
			Vector3.UnitZ, settings, 1f / 240f, out var torque ) );
		Assert.Equal( Vector3.Zero, torque );
		Assert.Equal( MotionMode.Rolling, manual.Mode );
	}

	[Theory]
	[InlineData( true )]
	[InlineData( false )]
	public void BailAndDetachmentRemoveManualAssistance( bool bail )
	{
		var (board, contacts, settings) = FlatBoard();
		var pilot = new BoardPilot( settings );
		var input = new ControlInput( FlickY: -0.45f );
		var before = pilot.Apply( board, contacts, input, 1f / 240f );
		Assert.True( before.Torque.Length() > 1f );
		if ( bail ) pilot.Bail();
		else pilot.SetRiderAttached( false );
		board.BeginStep();
		var after = pilot.Apply( board, contacts, input, 1f / 240f );
		Assert.Equal( Vector3.Zero, after.Torque );
		Assert.Equal( bail ? MotionMode.Bailed : MotionMode.Rolling, after.Mode );
	}

	[Fact]
	public void IntentAloneDoesNotReportAnAxleBalance()
	{
		var (board, contacts, settings) = FlatBoard();
		var manual = new ManualBalance();
		Assert.True( manual.TryBalance( board, contacts, new( FlickY: -0.45f ),
			Vector3.UnitZ, settings, 1f / 240f, out _ ) );
		Assert.Equal( MotionMode.Rolling, manual.Mode );
	}

	[Fact]
	public void MissingRearSupportDoesNotCreateARearManual()
	{
		var (board, contacts, settings) = FlatBoard();
		var manual = new ManualBalance();
		Assert.False( manual.TryBalance( board, contacts.AsSpan( 0, 2 ), new( FlickY: -0.45f ),
			Vector3.UnitZ, settings, 1f / 240f, out var torque ) );
		Assert.Equal( Vector3.Zero, torque );
	}

	private static (ArticulatedBoard, CollisionContact[], SkateSettings) FlatBoard()
	{
		var settings = new SkateSettings();
		const float radius = 0.0285f;
		var origins = new Vector3[ 4 ];
		for ( var i = 0; i < origins.Length; i++ )
			origins[ i ] = SkateWheels.Offset( i, settings ) - Vector3.UnitZ * (settings.RideHeight - radius);
		var board = new ArticulatedBoard( settings, origins, radius );
		board.Reset( Vector3.UnitZ * settings.RideHeight, Quaternion.Identity, Vector3.Zero, Vector3.Zero );
		var contacts = new CollisionContact[ 4 ];
		for ( var i = 0; i < 4; i++ )
		{
			var point = board.Body( i + 3 ).Position - Vector3.UnitZ * radius;
			contacts[ i ] = new( i + 3, 7, point, new Vector3( point.X, point.Y, 0f ),
				Vector3.UnitZ, Vector3.UnitX, Vector3.UnitY, 0.68f, 0.544f, 0f );
		}
		return (board, contacts, settings);
	}
}
