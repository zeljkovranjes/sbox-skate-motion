using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class GrindEdgeRecordTests
{
	[Theory]
	[InlineData( 0f, true )]
	[InlineData( 0.0000152587890625f, true )]
	[InlineData( -0.0000152587890625f, true )]
	[InlineData( 0.000016f, false )]
	[InlineData( -0.000016f, false )]
	public void HorizontalSpanUsesStrictThreshold( float horizontal, bool vertical )
	{
		var delta = new Vector3( horizontal, 2f, 0f );
		var record = Build( delta, LimbRigidTransform.Identity );
		Assert.Equal( vertical, record.IsVertical );
		Assert.Equal( Vector3.Zero, record.Segment.Start );
		Assert.Equal( delta, record.Segment.End );
	}

	[Fact]
	public void ClassificationUsesTransformedEndpoints()
	{
		var frame = new LimbRigidTransform( new LimbFrame( Vector3.UnitY, Vector3.UnitX, -Vector3.UnitZ ), new Vector3( 4f, 5f, 6f ) );
		var record = Build( Vector3.UnitX, frame );
		Assert.True( record.IsVertical );
		Assert.Equal( frame.Translation, record.Segment.Start );
		Assert.Equal( frame.Translation + Vector3.UnitY, record.Segment.End );
	}

	[Fact]
	public void GroupedCoefficientSumRetainsCancellationResult()
	{
		var record = GrindEdgeRecord.FromCoefficients( new Vector3( -16777216f, 0f, 0f ),
			Vector3.Zero, new Vector3( 16777216f, 0f, 0f ), Vector3.UnitX,
			LimbRigidTransform.Identity, 0u, 0u );
		Assert.Equal( Vector3.UnitX, record.Segment.Start );
		Assert.Equal( Vector3.Zero, record.Segment.End );
	}

	[Fact]
	public void ClassificationPreservesOtherFlagsAndMetadata()
	{
		var horizontal = Build( Vector3.UnitX, LimbRigidTransform.Identity );
		var vertical = Build( Vector3.UnitY, LimbRigidTransform.Identity );
		Assert.Equal( 17u, horizontal.Metadata );
		Assert.Equal( 17u, vertical.Metadata );
		Assert.Equal( 0x12345678u, horizontal.Flags );
		Assert.Equal( 0x92345678u, vertical.Flags );
	}

	[Fact]
	public void NonuniformScaleAndShearArePreserved()
	{
		var frame = new LimbRigidTransform( new LimbFrame( new Vector3( 2f, .5f, 0f ),
			new Vector3( 0f, 3f, 0f ), new Vector3( 0f, 0f, -4f ) ), new Vector3( 3f, 4f, 5f ) );
		var record = Build( Vector3.One, frame );
		Assert.Equal( new Vector3( 5f, 7.5f, 1f ), record.Segment.End );
		Assert.False( record.IsVertical );
	}

	private static GrindEdgeRecord Build( Vector3 delta, LimbRigidTransform frame ) =>
		GrindEdgeRecord.FromCoefficients( Vector3.Zero, Vector3.Zero, delta, Vector3.Zero, frame, 17u, 0x92345678u );
}
