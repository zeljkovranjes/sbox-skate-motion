using System.Linq;
using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class ClipRootTravelTests
{
	// root -> hand, root -> board: the root steps a metre forward over a second.
	private static readonly ClipSkeleton Skeleton = new( [ "root", "hand", "board" ], [ -1, 0, 0 ],
		[ Vector3.Zero, Vector3.Zero, Vector3.Zero ], [ Quaternion.Identity, Quaternion.Identity, Quaternion.Identity ] );

	private static AnimationClip Step( Vector3 hand, Vector3 board ) => new( "step", 60f, 61, [ 0, 0, 0 ], [ 0, 0, 0 ], [],
		[ 0, 61, 62 ], [ 61, 1, 1 ],
		Enumerable.Range( 0, 61 ).Select( f => new Vector3( f / 60f, 0f, 0f ) ).Concat( [ hand, board ] ).ToArray() );

	[Fact]
	public void ATravellingClipReportsHowFarAlongItsWayTheRootIs()
	{
		var travel = new ClipRootTravel( Skeleton );
		var clip = Step( new Vector3( 0.2f, 0.1f, 1f ), new Vector3( 0.3f, 0.1f, 1f ) );

		Assert.True( travel.Update( clip, 0.5f, 0.1f, new AnimationParameters(), 1, 2 ) );

		Assert.Equal( 0.5f, travel.Progress ?? -1f, 2 );
		Assert.Equal( 0f, travel.Start.X, 3 );
		Assert.Equal( 1f, travel.End.X, 3 );
		Assert.False( travel.StandsAtStart, "The board in the hand at the start: the skater stands by it at the end." );
		Assert.Equal( -0.5f, travel.FromStand.X, 2 );
		Assert.Equal( 1f, travel.Velocity.X, 2 );
	}

	[Fact]
	public void ABoardOutOfHandAtTheStartIsStoodByThereAndPastTheEndTheRootHolds()
	{
		var travel = new ClipRootTravel( Skeleton );
		var clip = Step( new Vector3( 0.2f, 0.1f, 1f ), new Vector3( 0.8f, 0.1f, 0.1f ) );

		travel.Update( clip, 3f, 0.1f, new AnimationParameters(), 1, 2 );

		Assert.True( travel.StandsAtStart );
		Assert.Equal( 1f, travel.Progress ?? -1f, 3 );
		Assert.Equal( 1f, travel.FromStand.X, 2 );
		Assert.Equal( Vector3.Zero, travel.Velocity );
	}

	[Fact]
	public void AClipThatDoesNotTravelHasNoProgress()
	{
		var travel = new ClipRootTravel( Skeleton );
		var still = new AnimationClip( "still", 30f, 2, [ 0, 0, 0 ], [ 0, 0, 0 ], [], [ 0, 1, 2 ], [ 1, 1, 1 ],
			[ Vector3.Zero, Vector3.UnitZ, Vector3.UnitX ] );

		travel.Update( still, 0.02f, 0.01f, new AnimationParameters(), 1, 2 );

		Assert.Null( travel.Progress );
		Assert.False( travel.Update( null, 0f, 0.01f, new AnimationParameters(), 1, 2 ) );
	}
}
