using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class PlacementStairTests
{
	[Fact]
	public void AStairClimbedOverTwoStepsIsTakenAsOne()
	{
		// The walker climbs a 6 in stair in two physics steps (3.3 in, then 2.7): the body eases up over it as one step,
		// its second half not going through at once (it rose 1.2 in in a frame between frames of 0.3 and 0.6).
		var smoother = new PlacementSmoother();
		var heights = new[] { 0f, 0f, 0f, 0f, 3.33f, 6f, 6f, 6f, 6f, 6f, 6f, 6f };
		var shown = smoother.Update( Vector3.Zero, false, true, false, Vector3.UnitZ, 1f / 90f );
		var largest = 0f;
		for ( var frame = 1; frame < heights.Length; frame++ )
		{
			var before = shown.Z;
			shown = smoother.Update( new Vector3( 0.85f * frame, 0f, heights[ frame ] ), false, true, false, Vector3.UnitZ, 1f / 90f );
			largest = MathF.Max( largest, shown.Z - before );
		}
		Assert.True( largest < 0.8f, $"rose {largest:0.00} in in a frame" );
	}
}
