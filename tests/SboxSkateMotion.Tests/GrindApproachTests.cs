using System.Numerics;
using SkateMotion.Core.Grinding;
using Xunit;

namespace SboxSkateMotion.Tests;

public class GrindApproachTests
{
	// The rail runs along x; the board rides along it toward -x (yaw 180), the deck's right +y.
	private static readonly Vector3 Rail = Vector3.UnitX;

	private static Vector3 Forward( float yaw ) =>
		new( MathF.Cos( yaw * MathF.PI / 180f ), MathF.Sin( yaw * MathF.PI / 180f ), 0f );

	// The deck's right: a quarter turn clockwise from its forward, seen from above.
	private static Vector3 Right( float yaw ) => new( Forward( yaw ).Y, -Forward( yaw ).X, 0f );

	// Rides along the rail, then turns in the air by the given degrees (positive toward yaw 270, negative toward 90) in
	// steps of 3, as a board turned onto the rail.
	private static GrindApproach TurnOnto( float turn, float startYaw = 180f )
	{
		var approach = new GrindApproach();
		for ( var i = 0; i < 10; i++ )
			approach.Track( Forward( startYaw ), Right( startYaw ), Rail );
		var steps = (int)MathF.Ceiling( MathF.Abs( turn ) / 3f );
		for ( var i = 1; i <= steps; i++ )
		{
			var yaw = startYaw + turn * i / steps;
			approach.Track( Forward( yaw ), Right( yaw ), Rail );
		}
		return approach;
	}

	[Fact]
	public void AlongTheRailTheRightIsSquareAcrossItTheWayTheDeckFaces()
	{
		var approach = new GrindApproach();
		approach.Track( Forward( 170f ), Right( 170f ), Rail );
		Assert.Equal( Vector3.UnitY, approach.Right );
		// Rolling the other way along it (fakie, or the deck turned round), the other side.
		approach.Track( Forward( 5f ), Right( 5f ), Rail );
		Assert.Equal( -Vector3.UnitY, approach.Right );
	}

	[Theory]
	[InlineData( 84f )]
	[InlineData( 90f )]
	[InlineData( 96f )]
	[InlineData( 120f )]
	[InlineData( -84f )]
	[InlineData( -90f )]
	[InlineData( -96f )]
	[InlineData( -120f )]
	public void TheNoseSideFollowsTheTurnShortOfSquareOrPastIt( float turn )
	{
		// Centred on the rail at the end of the turn, the nose lies across it where the turn swung it: toward -y turning to
		// yaw 270, toward +y turning to yaw 90. Measured against the deck's right at the end of the turn (along the rail),
		// it flipped between 84 and 96 degrees.
		var approach = TurnOnto( turn );
		var nose = Forward( 180f + turn ) * GrindShape.NoseReach;
		var noseOffset = new Vector3( 0f, nose.Y, 0f );
		Assert.Equal( turn < 0f, approach.Across( noseOffset ) > 0f );
		Assert.Equal( Vector3.UnitY, approach.Right );
	}

	[Theory]
	// Regular (the toes on the deck's right). The rail on the rider's left is a backside slide: turning the nose over it
	// (toward yaw 270, -y) a boardslide, turning it away a lipslide; on the right, frontside, the other way round.
	[InlineData( false, 84f, "bs-board" )]
	[InlineData( false, 96f, "bs-board" )]
	[InlineData( false, -84f, "bs-lip" )]
	[InlineData( false, -96f, "bs-lip" )]
	[InlineData( true, 84f, "fs-lip" )]
	[InlineData( true, 96f, "fs-lip" )]
	[InlineData( true, -84f, "fs-board" )]
	[InlineData( true, -96f, "fs-board" )]
	public void ABoardslidesNameFollowsTheTurnNotWhereItLocksOn( bool railOnRight, float turn, string name )
	{
		var approach = new GrindApproach();
		approach.Track( Forward( 180f ), Right( 180f ), Rail );
		// From the deck to the rail across it, before the turn: +y is the rider's right riding toward -x.
		var railOffset = new Vector3( 0f, railOnRight ? 0.15f : -0.15f, 0f );
		var railRight = approach.Across( railOffset ) > 0f;
		approach = TurnOnto( turn );
		var nose = Forward( 180f + turn ) * GrindShape.NoseReach;
		var noseRight = approach.Across( new Vector3( 0f, nose.Y, 0f ) ) > 0f;
		// As the rider's animator puts them together.
		const bool toesRight = true;
		var frontside = railRight == toesRight;
		var noseToHeels = noseRight != toesRight;
		Assert.Equal( name, GrindNames.Graph( "boardslide", frontside, noseToHeels, false, false, false ) );
	}

	[Theory]
	// Regular, the rail on the left (backside), the board coming from the right of it (+y). The nose turned toward the
	// rail (yaw 270) and resting on it with the deck on the near side is a noseslide; the tail turned onto it (the nose
	// swung away, yaw 90), a tailslide.
	[InlineData( "noseslide", 90f, "bs-noseslide" )]
	[InlineData( "noseslide", 84f, "bs-noseslide" )]
	[InlineData( "noseslide", 96f, "bs-noseslide" )]
	[InlineData( "tailslide", -90f, "bs-tailslide" )]
	[InlineData( "tailslide", -96f, "bs-tailslide" )]
	public void AnEndSlidesNoseSideIsWhereItPointsNotWhereItsTipSits( string kind, float turn, string name )
	{
		var approach = TurnOnto( turn );
		// The deck's middle 0.42 m on the near side of the rail: on a noseslide the nose's tip reaches about to the rail.
		var forward = Forward( 180f + turn );
		var tip = new Vector3( 0f, 0.42f, 0f ) + forward * GrindShape.NoseReach;
		Assert.True( MathF.Abs( tip.Y ) < 0.05f || kind == "tailslide" );
		var noseRight = approach.NoseSide( forward );
		Assert.NotNull( noseRight );
		const bool toesRight = true;
		const bool railRight = false;
		var frontside = railRight == toesRight;
		var noseToHeels = noseRight.Value != toesRight;
		Assert.Equal( name, GrindNames.Graph( kind, frontside, noseToHeels, false, false, false ) );
	}

	[Fact]
	public void ADeckAlongTheRailHasNoNoseSideYet()
	{
		var approach = TurnOnto( 0f );
		Assert.Null( approach.NoseSide( Forward( 181f ) ) );
		// Ten degrees off the line toward +y, the approach's right.
		Assert.True( approach.NoseSide( Forward( 170f ) ) );
	}

	[Fact]
	public void AHalfTurnTakesTheRightItHasAlongTheLineAgain()
	{
		// Round by 180 and on by a quarter more (a 270 onto the rail): the right is the one it had after the half turn.
		var approach = TurnOnto( 180f );
		Assert.Equal( -Vector3.UnitY, approach.Right );
		for ( var yaw = 360f; yaw <= 450f; yaw += 3f )
			approach.Track( Forward( yaw ), Right( yaw ), Rail );
		Assert.Equal( -Vector3.UnitY, approach.Right );
	}

	[Fact]
	public void ComingStraightAtARailOrWithNoneNearItIsTheDecksOwnRight()
	{
		// Never along the rail's line, there is no approach to keep: the deck's own right, as it is.
		var approach = new GrindApproach();
		approach.Track( Forward( 90f ), Right( 90f ), Rail );
		Assert.True( Vector3.Distance( Right( 90f ), approach.Right ) < 1e-5f );
		approach.Track( Forward( 30f ), Right( 30f ), null );
		Assert.True( Vector3.Distance( Right( 30f ), approach.Right ) < 1e-5f );
	}
}
