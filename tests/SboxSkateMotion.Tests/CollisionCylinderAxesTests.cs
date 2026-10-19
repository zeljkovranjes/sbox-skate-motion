using SkateMotion.Core.Collision;
using Xunit;

namespace SboxSkateMotion.Tests;

public sealed class CollisionCylinderAxesTests
{
	[Theory]
	[InlineData( 0, 0, 0, 1, 0, 0, 0, 0, 0, 0u, 0u, 0u )]
	[InlineData( 0, 0, 0, 0, 1, 0, 2, 0.25f, 0, 0u, 0x3f800000u, 0u )]
	[InlineData( 1, 0, 0, 0, 1, 0, -2, 0.25f, 1, 0u, 0xbf800000u, 0u )]
	[InlineData( 0, 1, 0, 0, 1, 0, 0, 1, 0, 0u, 0u, 0x3f7fffffu )]
	[InlineData( 3, -2, 1, 0, 1, 0, -0.5f, 1, 1, 0u, 0xbf55013fu, 0x3f0e00d5u )]
	[InlineData( -2, 4, -3, 0, 1, 0, 2, 1, 0, 0u, 0x3f5b84a7u, 0xbf03b5feu )]
	public void BoundedCandidatesRetainDirectionRoundingAndOtherRows( float x, float y, float z,
		float axisX, float axisY, float axisZ, float offset, float halfLength, int count,
		uint expectedX, uint expectedY, uint expectedZ )
	{
		uint[] inputs = [ 0, 0, 0, 0, Word( 1 ), 0, 0, 0,
			Word( x ), Word( y ), Word( z ), 0, Word( axisX ), Word( axisY ), Word( axisZ ), 0 ];
		var before = inputs.AsSpan().ToArray();
		var output = new uint[ 12 ];
		output.AsSpan().Fill( 0xa5a5a5a5u );
		Assert.Equal( count + 1, CollisionCylinderAxes.AppendSegmentCandidate(
			inputs, Word( offset ), Word( halfLength ), output, count ) );
		Assert.Equal( new uint[] { expectedX, expectedY, expectedZ, 0 }, output.AsSpan( count * 4, 4 ).ToArray() );
		for ( var i = 0; i < output.Length; ++i )
		{
			if ( i < count * 4 || i >= (count + 1) * 4 )
				Assert.Equal( 0xa5a5a5a5u, output[ i ] );
		}
		Assert.Equal( before, inputs );
	}

	[Theory]
	[InlineData( 16, -1 )]
	[InlineData( 16, 2 )]
	[InlineData( 15, 0 )]
	public void InvalidBuffersAndCountsLeaveOutputUnchanged( int inputLength, int count )
	{
		var output = new uint[ 8 ];
		output.AsSpan().Fill( 0xabcdef01u );
		var before = output.AsSpan().ToArray();
		Assert.Throws<ArgumentException>( () => CollisionCylinderAxes.AppendSegmentCandidate(
			new uint[ inputLength ], 0, Word( 1 ), output, count ) );
		Assert.Equal( before, output );
	}

	private static uint Word( float value ) => BitConverter.SingleToUInt32Bits( value );
}
