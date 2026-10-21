using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class BoardFitTests
{
	private static readonly BoardFit Clips = BoardFit.Default;

	[Fact]
	public void TheClipsBoardFitsOntoItselfUnchanged()
	{
		var point = new Vector3( 0.31f, -0.07f, 0.02f );
		Assert.Equal( point, Clips.Hand( point, Clips ) );
		Assert.Equal( point, Clips.Foot( point, Clips ) );
	}

	[Fact]
	public void TrucksEndsAndEdgesLandOnTheBoardRiddensOwn()
	{
		var board = new BoardFit( 0.6f, 1.05f, 0.26f, 0.009f );
		AssertNear( new Vector3( 0.3f, 0f, 0f ), board.Hand( new Vector3( Clips.Wheelbase * 0.5f, 0f, 0f ), Clips ) );
		AssertNear( new Vector3( -0.525f, 0f, 0f ), board.Hand( new Vector3( -Clips.Length * 0.5f, 0f, 0f ), Clips ) );
		AssertNear( new Vector3( 0f, 0.13f, 0f ), board.Hand( new Vector3( 0f, Clips.Width * 0.5f, 0f ), Clips ) );
		// Halfway from truck to nose stays halfway; a hand 2 cm past the nose stays 2 cm past it.
		var halfway = (Clips.Wheelbase + Clips.Length) * 0.25f;
		AssertNear( new Vector3( (0.6f + 1.05f) * 0.25f, 0f, 0f ), board.Hand( new Vector3( halfway, 0f, 0f ), Clips ) );
		AssertNear( new Vector3( 0.545f, 0f, 0f ), board.Hand( new Vector3( Clips.Length * 0.5f + 0.02f, 0f, 0f ), Clips ) );
	}

	[Fact]
	public void APointKeepsItsHeightOverTheDeckTopAndUnderItsUnderside()
	{
		var board = new BoardFit( 0.5f, 0.8f, 0.2f, 0.025f );
		Assert.Equal( 0.0125f + 0.03f, board.Hand( new Vector3( 0f, 0f, Clips.Thickness * 0.5f + 0.03f ), Clips ).Z, 5 );
		Assert.Equal( -0.0125f - 0.01f, board.Foot( new Vector3( 0f, 0f, -Clips.Thickness * 0.5f - 0.01f ), Clips ).Z, 5 );
	}

	[Fact]
	public void FeetFollowTheTrucksOnlyAsFarAsARiderStands()
	{
		var longboard = new BoardFit( 0.97f, 1.5f, 0.36f, 0.01f );
		var foot = longboard.Foot( new Vector3( Clips.Wheelbase * 0.5f, Clips.Width * 0.5f, 0f ), Clips );
		Assert.Equal( Clips.Wheelbase * 0.5f * BoardFit.MostStance, foot.X, 4 );
		Assert.Equal( Clips.Width * 0.5f * BoardFit.MostStance, foot.Y, 4 );
		var cruiser = new BoardFit( 0.3f, 0.6f, 0.17f, 0.012f );
		Assert.Equal( -Clips.Wheelbase * 0.5f * BoardFit.LeastStance, cruiser.Foot( new Vector3( -Clips.Wheelbase * 0.5f, 0f, 0f ), Clips ).X, 4 );
		// A hand still reaches the longboard's own truck and edge.
		Assert.Equal( 0.485f, longboard.Hand( new Vector3( Clips.Wheelbase * 0.5f, 0f, 0f ), Clips ).X, 4 );
	}

	[Fact]
	public void TheClipsBoardScalesWithTheRider()
	{
		var tall = Clips.Scaled( 1.1f );
		Assert.Equal( Clips.Wheelbase * 1.1f, tall.Wheelbase, 5 );
		var board = Clips;
		// The clips' truck on a taller rider's (larger) clip board still lands on the board's truck.
		Assert.Equal( Clips.Wheelbase * 0.5f, board.Hand( new Vector3( tall.Wheelbase * 0.5f, 0f, 0f ), tall ).X, 4 );
	}

	[Fact]
	public void DistanceOffTheClipsDeck()
	{
		Assert.Equal( 0f, BoardFit.OffDeck( new Vector3( 0.2f, 0.05f, 0f ), Clips ) );
		Assert.Equal( 0.05f, BoardFit.OffDeck( new Vector3( 0f, Clips.Width * 0.5f + 0.05f, 0f ), Clips ), 5 );
	}

	[Fact]
	public void AFootBesideTheDeckKeepsItsDistancePastTheEdge()
	{
		var wide = new BoardFit( 0.97f, 1.5f, 0.36f, 0.01f );
		// Well past the clips' edge (a pushing foot on the ground) it is as far past the wide deck's edge.
		var pushing = wide.Beside( new Vector3( -0.2f, -(Clips.Width * 0.5f + 0.15f), -0.1f ), Clips, 0.08f );
		Assert.Equal( -(0.18f + 0.15f), pushing.Y, 4 );
		// On the deck it stands where Foot puts it, and just past the edge it eases between the two.
		var onDeck = new Vector3( 0.1f, 0.05f, 0.01f );
		Assert.Equal( wide.Foot( onDeck, Clips ), wide.Beside( onDeck, Clips, 0.08f ) );
		var edge = wide.Beside( new Vector3( 0f, Clips.Width * 0.5f + 0.04f, 0f ), Clips, 0.08f );
		Assert.InRange( edge.Y, wide.Foot( new Vector3( 0f, Clips.Width * 0.5f + 0.04f, 0f ), Clips ).Y, 0.18f + 0.04f );
	}

	private static void AssertNear( Vector3 expected, Vector3 actual ) =>
		Assert.True( Vector3.Distance( expected, actual ) < 1e-4f, $"{actual} against {expected}" );
}
