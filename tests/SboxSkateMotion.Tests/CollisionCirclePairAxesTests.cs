using SkateMotion.Core.Collision;
using Xunit;

namespace SboxSkateMotion.Tests;

public sealed class CollisionCirclePairAxesTests
{
	[Theory]
	[InlineData( 0, 0, 0, 0, 0, 1, 0x3f800000u, 0u, 0u )]
	[InlineData( 1, 1, 0, 0, 0, 1, 0u, 0u, 0x3f7fffffu )]
	[InlineData( 1, 1, 1, 0, 0, 1, 0x3f7fffffu, 0u, 0u )]
	[InlineData( 1, 1, 0, 1, 0, 1, 0u, 0xbee4f92eu, 0xbf64f92eu )]
	[InlineData( 1, 1, 3, -2, 1, 1, 0x3f7f3609u, 0xbd8fb2a5u, 0x3d0fb2a5u )]
	[InlineData( 1, 1, 0, 0, 0, -1, 0u, 0u, 0u )]
	public void AcceptedCandidatesPreserveSearchAndFallbackDirections( float firstRadius, float secondRadius,
		float x, float y, float z, float secondAxis, uint expectedX, uint expectedY, uint expectedZ )
	{
		var inputs = Pair( firstRadius, secondRadius, x, y, z, secondAxis );
		var before = inputs.AsSpan().ToArray();
		var output = new uint[ 8 ];
		output.AsSpan().Fill( 0xabcdef01u );
		Assert.True( CollisionCylinderAxes.TryCircleCircleCandidate( inputs, output ) );
		Assert.Equal( new uint[] { expectedX, expectedY, expectedZ, 0 }, output.AsSpan( 0, 4 ).ToArray() );
		Assert.All( output.AsSpan( 4 ).ToArray(), value => Assert.Equal( 0xabcdef01u, value ) );
		Assert.Equal( before, inputs );
	}

	[Theory]
	[InlineData( 0, 1, 0 )]
	[InlineData( 1, 1, 1 )]
	public void RejectedCandidatesLeaveEveryOutputWordUnchanged( float firstRadius, float secondRadius, float height )
	{
		var inputs = Pair( firstRadius, secondRadius, 0, 0, height, 1 );
		var output = new uint[ 8 ];
		output.AsSpan().Fill( 0xabcdef01u );
		var before = output.AsSpan().ToArray();
		Assert.False( CollisionCylinderAxes.TryCircleCircleCandidate( inputs, output ) );
		Assert.Equal( before, output );
	}

	[Theory]
	[InlineData( 27, 4 )]
	[InlineData( 28, 3 )]
	public void InvalidBuffersDoNotWrite( int inputLength, int outputLength )
	{
		var output = new uint[ outputLength ];
		output.AsSpan().Fill( 0xabcdef01u );
		var before = output.AsSpan().ToArray();
		Assert.Throws<ArgumentException>( () => CollisionCylinderAxes.TryCircleCircleCandidate( new uint[ inputLength ], output ) );
		Assert.Equal( before, output );
	}

	private static uint[] Pair( float firstRadius, float secondRadius, float x, float y, float z, float secondAxis )
	{
		var a = Word( firstRadius );
		var b = Word( secondRadius );
		return [ 0, 0, 0, 0, Word( 1 ), 0, 0, 0, a, a, a, a,
			Word( x ), Word( y ), Word( z ), 0, Word( secondAxis ), 0, 0, 0, b, b, b, b, 0, Word( 1 ), 0, 0 ];
	}

	private static uint Word( float value ) => BitConverter.SingleToUInt32Bits( value );
}
