using System.Numerics;
using SkateMotion.Core.Animation;
using SkateMotion.Core.Vehicle;
using Xunit;

namespace SboxSkateMotion.Tests;

public class ShownEaseTests
{
	// A ramp's vertical wall facing back along -x, as a hand plant's lip has it.
	private static readonly Vector3 Wall = -Vector3.UnitX;

	private static float Degrees( Quaternion a, Quaternion b )
	{
		var dot = MathF.Min( 1f, MathF.Abs( Quaternion.Dot( Quaternion.Normalize( a ), Quaternion.Normalize( b ) ) ) );
		return 2f * MathF.Acos( dot ) * 180f / MathF.PI;
	}

	// The board shown after a hand plant lets go, frame by frame: physics has it at the drop's turn at once, the shown board
	// eases there from its stall (the offset, in the board's own frame, shrinking along ShownEase.Left).
	private static Quaternion[] Shown( Quaternion stall, Quaternion drop, float frame, float seconds )
	{
		var offset = Quaternion.Normalize( Quaternion.Inverse( drop ) * stall );
		var shown = new System.Collections.Generic.List<Quaternion> { stall };
		for ( var t = frame; t <= seconds; t += frame )
		{
			var left = ShownEase.Left( t, ShownEase.PlantLetGo );
			shown.Add( Quaternion.Normalize( drop * Quaternion.Slerp( Quaternion.Identity, offset, left ) ) );
		}
		return shown.ToArray();
	}

	[Theory]
	[InlineData( 60f )]
	[InlineData( 144f )]
	public void TheShownBoardsTurnPerFrameStaysBoundedAcrossThePlantsLetGo( float rate )
	{
		// Stalled over the lip nose up (as it rose up the wall) and dropped nose down the ramp: a half turn and more in one
		// step for physics. Shown, no frame turns more than the ease's steepest share of it (1.5 times the even rate), and
		// the first frame barely moves.
		var plant = new HandPlant();
		plant.Ground( Vector3.Zero, Wall );
		var drop = plant.DropRotation();
		var stall = Quaternion.CreateFromAxisAngle( Vector3.UnitY, -MathF.PI / 2f );
		var total = Degrees( stall, drop );
		Assert.True( total > 100f );
		var frame = 1f / rate;
		var shown = Shown( stall, drop, frame, 0.4f );
		var most = 0f;
		for ( var i = 1; i < shown.Length; i++ )
			most = MathF.Max( most, Degrees( shown[ i - 1 ], shown[ i ] ) );
		Assert.True( most <= total * 1.5f * frame / ShownEase.PlantLetGo + 0.5f, $"{most} degrees in a frame" );
		Assert.True( Degrees( shown[ 0 ], shown[ 1 ] ) < total * 0.05f );
	}

	[Fact]
	public void TheShownBoardMeetsThePhysicalBoardWithinTheEase()
	{
		var plant = new HandPlant();
		plant.Ground( Vector3.Zero, Wall );
		var drop = plant.DropRotation();
		var stall = Quaternion.CreateFromAxisAngle( Vector3.UnitY, -MathF.PI / 2f );
		var shown = Shown( stall, drop, 1f / 60f, 0.3f );
		Assert.True( Degrees( shown[ ^1 ], drop ) < 0.01f );
		Assert.True( ShownEase.PlantLetGo <= 0.3f );
	}

	[Theory]
	[InlineData( 0f, 1f )]
	[InlineData( 0.125f, 0.5f )]
	[InlineData( 0.25f, 0f )]
	[InlineData( 0.3f, 0f )]
	public void TheEaseLeavesItsShareAlongASmoothstep( float elapsed, float left )
	{
		Assert.Equal( left, ShownEase.Left( elapsed, 0.25f ), 4 );
		Assert.Equal( 0f, ShownEase.Left( 0.1f, 0f ) );
	}
}
