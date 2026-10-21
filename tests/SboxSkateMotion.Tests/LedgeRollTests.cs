using System.Numerics;
using SkateMotion.Core;
using SkateMotion.Core.Constraints;
using SkateMotion.Core.Grinding;
using SkateMotion.Core.Vehicle;
using Xunit;

namespace SboxSkateMotion.Tests;

public class LedgeRollTests
{
	private const float Radius = 0.0285f;

	private static ArticulatedBoard Board( SkateSettings settings )
	{
		var origins = new Vector3[ 4 ];
		for ( var i = 0; i < origins.Length; i++ )
			origins[ i ] = SkateWheels.Offset( i, settings ) - Vector3.UnitZ * (settings.RideHeight - Radius);
		return new ArticulatedBoard( settings, origins, Radius );
	}

	[Fact]
	public void TheRollRestsTheWheelsOverTheTopOnIt()
	{
		// The hanger held on the edge 8 mm and the rail's skin up, a wheel 9.5 cm across it and 2.85 cm round: its bottom
		// is the ledge gap over the top when the board rolls asin((2.85 - 0.8 - skin + gap) / 9.5), that side up.
		var board = Board( SkateSettings.RecoveredDefaults );
		Assert.Equal( MathF.Asin( (Radius - 0.008f - RailContacts.RailSkin + GrindControl.LedgeWheelGap) / 0.095f ),
			GrindControl.LedgeRoll( board ), 4 );
		// The wheel's bottom: the hanger's hold, plus the rise across the track, less the wheel's radius.
		var roll = GrindControl.LedgeRoll( board );
		Assert.Equal( GrindControl.LedgeWheelGap, 0.008f + RailContacts.RailSkin + 0.095f * MathF.Sin( roll ) - Radius, 4 );
	}

	[Fact]
	public void OnALedgesEdgeTheTrucksBalanceRolledWithTheTopSideUp()
	{
		var board = Board( SkateSettings.RecoveredDefaults );
		var roll = GrindControl.LedgeRoll( board );
		// A ledge along X with its top toward -Y.
		var grind = new GrindLock( GrindKind.FiftyFifty, Vector3.UnitX, Vector3.UnitZ ) { Top = -Vector3.UnitY };
		var normal = GrindControl.Balance( grind, Vector3.UnitX, default, roll );
		// The deck's up leans away from the top, so the side over the top is the higher one.
		Assert.Equal( MathF.Cos( roll ), normal.Z, 4 );
		Assert.Equal( MathF.Sin( roll ), normal.Y, 4 );
		// A rail without a ledge, or a slide, keeps its balance.
		Assert.Equal( Vector3.UnitZ, GrindControl.Balance( grind with { Top = Vector3.Zero }, Vector3.UnitX, default, roll ) );
		Assert.Equal( 0f, GrindControl.Balance( grind with { Kind = GrindKind.Boardslide }, Vector3.UnitY, default, roll ).Y, 4 );
	}
}
