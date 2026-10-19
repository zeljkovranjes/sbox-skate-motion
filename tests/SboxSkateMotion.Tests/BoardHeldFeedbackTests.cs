using System.Numerics;
using SkateMotion.Core.Physics;
using Xunit;

namespace SboxSkateMotion.Tests;

public class BoardHeldFeedbackTests
{
	[Fact]
	public void CoincidentPositionsYieldFiniteZeroDirections()
	{
		var position = new Vector3( 3f, -2f, 9f );
		var result = BoardHeldFeedback.Create( position, position, position, 0 );
		Assert.Equal( Vector3.Zero, result.FirstDirection );
		Assert.Equal( Vector3.Zero, result.SecondDirection );
		Assert.True( result.Enabled );
		Assert.Equal( .8f, result.AlignmentThreshold );
	}

	[Fact]
	public void ThresholdIsStrictForEachIndependentDirection()
	{
		var result = BoardHeldFeedback.Create( Vector3.Zero,
			new Vector3( .000001f, 0f, 0f ), new Vector3( 0f, -.000002f, 0f ), 0 );
		Assert.Equal( Vector3.Zero, result.FirstDirection );
		Assert.Equal( -Vector3.UnitY, result.SecondDirection );
	}

	[Fact]
	public void DirectionsUseRelativePositionsAndPreserveOtherFlags()
	{
		const ulong flags = (1UL << 63) | 0x42;
		var result = BoardHeldFeedback.Create( new Vector3( 4f, 6f, 8f ),
			new Vector3( 7f, 6f, 8f ), new Vector3( 4f, 6f, 3f ), flags );
		Assert.Equal( Vector3.UnitX, result.FirstDirection );
		Assert.Equal( -Vector3.UnitZ, result.SecondDirection );
		Assert.Equal( flags | (1UL << 62), result.BodyFlags );
	}
}
