using SkateMotion.Core.Collision;
using Xunit;

namespace SboxSkateMotion.Tests;

public sealed class CollisionCircleSegmentAxesTests
{
	[Theory]
	[InlineData( 1, 0, 0, 0, 0, 0, 0, 0x3f800000u, 0u, 0u )]
	[InlineData( 1, 0, 0, -2, 0, 0, 2, 0xbf7fffffu, 0u, 0u )]
	[InlineData( 1, -2, 0, 0, 2, 0, 0, 0xbf800000u, 0u, 0u )]
	[InlineData( 0, -2, 0.5f, -1, 2, 0.5f, 1, 0xb47fffffu, 0x3f7fffffu, 0xb3ffffffu )]
	[InlineData( 1, -2, 0.5f, -1, 2, 0.5f, 1, 0xb4800001u, 0xbf7fffffu, 0x33ffffffu )]
	[InlineData( 1, 3, -2, 1, 4, 2, -1, 0x3f79d497u, 0xbe47dd42u, 0x3dc7dd42u )]
	public void CandidateRetainsBoundedSearchAndDegenerateDirections( float radius,
		float ax, float ay, float az, float bx, float by, float bz,
		uint expectedX, uint expectedY, uint expectedZ )
	{
		var r = Word( radius );
		uint[] inputs = [ 0, 0, 0, 0, Word( 1 ), 0, 0, 0, r, r, r, r,
			Word( ax ), Word( ay ), Word( az ), 0, Word( bx ), Word( by ), Word( bz ), 0 ];
		var before = inputs.AsSpan().ToArray();
		var output = new uint[ 8 ];
		output.AsSpan().Fill( 0xa5a5a5a5u );
		CollisionCylinderAxes.CircleSegmentCandidate( inputs, output );
		Assert.Equal( new uint[] { expectedX, expectedY, expectedZ, 0 }, output.AsSpan( 0, 4 ).ToArray() );
		Assert.All( output.AsSpan( 4 ).ToArray(), value => Assert.Equal( 0xa5a5a5a5u, value ) );
		Assert.Equal( before, inputs );
	}

	[Theory]
	[InlineData( 19, 4 )]
	[InlineData( 20, 3 )]
	public void IncompleteBuffersDoNotWrite( int inputLength, int outputLength )
	{
		var output = new uint[ outputLength ];
		output.AsSpan().Fill( 0xabcdef01u );
		var before = output.AsSpan().ToArray();
		Assert.Throws<ArgumentException>( () => CollisionCylinderAxes.CircleSegmentCandidate( new uint[ inputLength ], output ) );
		Assert.Equal( before, output );
	}

	private static uint Word( float value ) => BitConverter.SingleToUInt32Bits( value );
}
