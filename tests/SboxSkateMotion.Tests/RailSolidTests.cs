using System.Numerics;
using SkateMotion.Core.Animation;
using SkateMotion.Core.Grinding;
using Xunit;

namespace SboxSkateMotion.Tests;

public class RailSolidTests
{
	// A 5 cm square rail 4 m long along X, its top 40 cm up.
	private static readonly RailSolid Rail = new( new Vector3( 0f, 0f, 0.375f ), Quaternion.Identity, new Vector3( 2f, 0.025f, 0.025f ) );
	private const float Top = 0.4f;

	// Two triangles making a flat horizontal rectangle at a height, spanning x and y.
	private static float Lift( RailSolid rail, float height, float x0, float x1, float y0, float y1 ) => MathF.Max(
		rail.Lift( new Vector3( x0, y0, height ), new Vector3( x1, y0, height ), new Vector3( x1, y1, height ) ),
		rail.Lift( new Vector3( x0, y0, height ), new Vector3( x1, y1, height ), new Vector3( x0, y1, height ) ) );

	[Fact]
	public void ShapeAboveTheRailNeedsNoLift()
	{
		Assert.Equal( 0f, Lift( Rail, Top + 0.001f, -0.1f, 0.1f, -0.2f, 0.2f ) );
		Assert.Equal( 0f, Lift( Rail, Top, -0.1f, 0.1f, -0.2f, 0.2f ), 6 );
	}

	[Fact]
	public void PlateAcrossTheRailRisesExactlyOutOfItsTop()
	{
		// An axle's underside crossing the rail 2 mm below its top, its corners well beyond both sides.
		Assert.Equal( 0.002f, Lift( Rail, Top - 0.002f, -0.0125f, 0.0125f, -0.095f, 0.095f ), 5 );
		// A deck underside lying across the rail 1 cm into it rises the full centimetre.
		Assert.Equal( 0.01f, Lift( Rail, Top - 0.01f, -0.3f, 0.3f, -0.11f, 0.11f ), 5 );
	}

	[Fact]
	public void ShapeBesideTheRailIsLeftAlone()
	{
		// A wheel hanging beside the rail below its top, clear of the rail's side.
		Assert.Equal( 0f, Lift( Rail, Top - 0.02f, -0.03f, 0.03f, 0.03f, 0.05f ) );
		Assert.Equal( 0f, Lift( Rail, Top - 0.02f, 2.01f, 2.2f, -0.1f, 0.1f ) );
	}

	[Fact]
	public void ShapeComingInFromTheSideIsLiftedGradually()
	{
		// Deep below the top, a shape in from the side counts only its depth from that side plus the side reach.
		var shallow = Lift( Rail, Top - 0.04f, -0.03f, 0.03f, 0.024f, 0.06f );
		var deeper = Lift( Rail, Top - 0.04f, -0.03f, 0.03f, 0.02f, 0.06f );
		Assert.Equal( RailSolid.SideReach + 0.001f, shallow, 4 );
		Assert.Equal( RailSolid.SideReach + 0.005f, deeper, 4 );
		// Near the top the full depth is taken even at the rail's edge.
		Assert.Equal( 0.003f, Lift( Rail, Top - 0.003f, -0.03f, 0.03f, 0.02f, 0.06f ), 5 );
	}

	[Fact]
	public void ShapeReachingDownThroughTheTopRisesFullyWhateverItsDepth()
	{
		// A steeply tipped deck end plunging through the top and out below the rail's bottom, 8 cm below its top.
		var lift = Rail.Lift( new Vector3( 0f, -0.1f, Top + 0.05f ), new Vector3( 0f, 0.1f, Top + 0.05f ), new Vector3( 0f, 0f, Top - 0.08f ) );
		Assert.Equal( 0.08f, lift, 4 );
	}

	[Fact]
	public void TriangleOfAShapeComingDownThroughTheTopRisesFullyEvenWhollyInside()
	{
		// The underside of a rolled axle sunk wholly inside the rail, its hanger above the top: counted alone it came in
		// from beside the rail, but as part of a shape reaching down through the top it rises by its full depth.
		var roll = Quaternion.CreateFromAxisAngle( Vector3.UnitX, 0.13f );
		Vector3 Corner( float y, float x ) => new Vector3( 0f, 0f, Top - 0.03f ) + Vector3.Transform( new Vector3( x, y, 0f ), roll );
		var (a, b, c) = (Corner( -0.095f, -0.0125f ), Corner( 0.095f, -0.0125f ), Corner( 0.095f, 0.0125f ));
		var alone = Rail.Lift( a, b, c );
		var through = Rail.Lift( a, b, c, true );
		var lowest = 0.03f + 0.025f * MathF.Tan( 0.13f );
		Assert.Equal( lowest, through, 4 );
		Assert.True( alone < through );
		Assert.False( Rail.ReachesThroughTop( a, b, c ) );
		Assert.True( Rail.ReachesThroughTop( new Vector3( 0f, -0.01f, Top + 0.02f ), new Vector3( 0f, 0.01f, Top + 0.02f ),
			new Vector3( 0f, 0f, Top - 0.02f ) ) );
	}

	[Fact]
	public void LiftReachesRightUpToTheRailsEndsAndFadesPastThem()
	{
		// A truck coming down at the rail's very start is lifted fully out of its top.
		Assert.Equal( 0.01f, Lift( Rail, Top - 0.01f, 1.996f, 2.1f, -0.1f, 0.1f ), 5 );
		// Deeper than the reach, a shape at the end counts only as deep as it is in from the end plus the reach, so passing
		// the end takes the lift away a little at a time; past the end there is none.
		Assert.Equal( RailSolid.SideReach + 0.004f, Lift( Rail, Top - 0.04f, 1.996f, 2.1f, -0.1f, 0.1f ), 5 );
		Assert.Equal( 0f, Lift( Rail, Top - 0.04f, 2.001f, 2.1f, -0.1f, 0.1f ) );
	}

	[Fact]
	public void LiftIsAlongATiltedRailsUp()
	{
		// A rail sloping at 20 degrees: a plate parallel to its top, 3 mm into it.
		var turn = Quaternion.CreateFromAxisAngle( Vector3.UnitY, 0.35f );
		var rail = Rail with { Rotation = turn };
		var up = rail.Up;
		var centre = rail.Centre + up * (rail.Half.Z - 0.003f);
		var along = Vector3.Transform( Vector3.UnitX, turn ) * 0.2f;
		var across = Vector3.UnitY * 0.1f;
		var lift = MathF.Max( rail.Lift( centre - along - across, centre + along - across, centre + along + across ),
			rail.Lift( centre - along - across, centre + along + across, centre - along + across ) );
		Assert.Equal( 0.003f, lift, 5 );
		Assert.Equal( MathF.Sin( 0.35f ), up.X, 5 );
	}

	[Fact]
	public void LiftChangesContinuouslyAsAShapeSinks()
	{
		var previous = 0f;
		for ( var depth = 0f; depth <= 0.03f; depth += 0.0005f )
		{
			var lift = Lift( Rail, Top - depth, -0.0125f, 0.0125f, -0.095f, 0.095f );
			Assert.InRange( lift - previous, -1e-4f, 0.0006f );
			previous = lift;
		}
	}

	[Fact]
	public void TruckJustAboveTheTopIsAtTheRail()
	{
		// An axle resting on the rail, one coming down 4 cm above it and one beside it within the groove are at the rail;
		// one under it, far above it or past its end is not.
		Assert.True( Rail.AtTop( new Vector3( 0f, 0f, Top + 0.008f ), 0.06f, 0.06f ) );
		Assert.True( Rail.AtTop( new Vector3( 1f, 0.05f, Top + 0.04f ), 0.06f, 0.06f ) );
		Assert.False( Rail.AtTop( new Vector3( 0f, 0f, Top - 0.06f ), 0.06f, 0.06f ) );
		Assert.False( Rail.AtTop( new Vector3( 0f, 0f, Top + 0.1f ), 0.06f, 0.06f ) );
		Assert.False( Rail.AtTop( new Vector3( 0f, 0.1f, Top + 0.01f ), 0.06f, 0.06f ) );
		Assert.False( Rail.AtTop( new Vector3( 2.1f, 0f, Top + 0.01f ), 0.06f, 0.06f ) );
	}

	[Fact]
	public void BallInsideTheRailLeavesUpFromAboveOrSidewaysNeverDown()
	{
		// A shin of a leg from above, 2 cm into the rail's top, rises out, even near a side; one of a leg hanging beside
		// the rail, 1 cm in from a side, goes out that side; one beside, below or past the rail stays.
		var up = Rail.Exit( new Vector3( 0f, 0.02f, Top - 0.02f + 0.03f ), 0.03f, true );
		Assert.Equal( 0.02f, up.Z, 4 );
		Assert.Equal( 0f, up.Y, 4 );
		var side = Rail.Exit( new Vector3( 0f, -0.045f, Top - 0.01f ), 0.03f, false );
		Assert.Equal( -0.01f, side.Y, 4 );
		Assert.Equal( 0f, side.Z, 4 );
		Assert.Equal( Vector3.Zero, Rail.Exit( new Vector3( 0f, 0.06f, Top - 0.02f ), 0.03f, true ) );
		Assert.Equal( Vector3.Zero, Rail.Exit( new Vector3( 0f, 0f, Top - 0.09f ), 0.03f, true ) );
		Assert.Equal( Vector3.Zero, Rail.Exit( new Vector3( 2.05f, 0f, Top ), 0.03f, true ) );
		Assert.True( Rail.AboveTop( new Vector3( 0f, 0.5f, Top + 0.3f ) ) );
		Assert.False( Rail.AboveTop( new Vector3( 0f, 0.5f, Top - 0.01f ) ) );
	}

	[Fact]
	public void DistanceIsNegativeInsideAndPositiveOutside()
	{
		Assert.Equal( -0.025f, Rail.Distance( Rail.Centre ), 5 );
		Assert.Equal( 0.1f, Rail.Distance( new Vector3( 0f, 0f, Top + 0.1f ) ), 5 );
		Assert.Equal( 0.05f, Rail.Distance( new Vector3( 0f, 0.075f, Top - 0.01f ) ), 5 );
		Assert.True( Rail.IsValid );
		Assert.False( (Rail with { Half = Vector3.Zero }).IsValid );
	}

	[Fact]
	public void GrindingBoardIsNotFlying()
	{
		Assert.True( new RiderAnimationInput { Airborne = true }.Flying );
		Assert.False( new RiderAnimationInput { Airborne = true, Grind = "fiftyfifty" }.Flying );
		Assert.False( new RiderAnimationInput { Airborne = false }.Flying );
		Assert.True( new RiderAnimationInput { Airborne = true, Grind = "" }.Flying );
	}
}
