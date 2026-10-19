using System;
using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class PhysicalGroundQuerySegmentsTests
{
	[Fact]
	public void OrderedCandidatesPreserveNeutralBreaksAndSkipShortEdges()
	{
		var points = new Vector3[]
		{
			new( 0f, 0f, 0f ),
			new( 0.4f, 0.1f, 0f ),
			new( 0.8f, 0.2f, 0f ),
			new( 0.8f, 0.6f, 0f ),
			new( 0.8f, 1f, 0f ),
			new( 0.800001f, 1f, 0f ),
			new( 1.2f, 0.9f, 0f ),
		};
		var expected = new uint[]
		{
			0x00000000u, 0x41234567u, 0x41234567u, 0x41234567u, 0x00000000u, 0x00000000u, 0x00000000u, 0x3f800000u,
			0x3ecccccdu, 0x3dcccccdu, 0x00000000u, 0x3f800000u, 0x3f785b40u, 0x3e785b40u, 0x00000000u, 0x00000000u,
			0x3e785b42u, 0xbf785b42u, 0x00000000u, 0x00000000u, 0x3ed31a60u, 0x41234567u, 0x41234567u, 0x41234567u,
			0x00000000u, 0x41234567u, 0x41234567u, 0x41234567u, 0x3ecccccdu, 0x3dcccccdu, 0x00000000u, 0x3f800000u,
			0x3f4ccccdu, 0x3e4ccccdu, 0x00000000u, 0x3f800000u, 0x3f785b40u, 0x3e785b40u, 0x00000000u, 0x00000000u,
			0x3e785b42u, 0xbf785b42u, 0x00000000u, 0x00000000u, 0x3ed31a60u, 0x41234567u, 0x41234567u, 0x41234567u,
			0x00000001u, 0x41234567u, 0x41234567u, 0x41234567u, 0x3f4ccccdu, 0x3e4ccccdu, 0x00000000u, 0x3f800000u,
			0x3f4ccccdu, 0x3f800000u, 0x00000000u, 0x3f800000u, 0x00000000u, 0x3f7fffffu, 0x00000000u, 0x00000000u,
			0x3f800000u, 0x00000000u, 0x00000000u, 0x00000000u, 0x3f4cccceu, 0x41234567u, 0x41234567u, 0x41234567u,
			0x00000000u, 0x41234567u, 0x41234567u, 0x41234567u, 0x3f4cccdeu, 0x3f800000u, 0x00000000u, 0x3f800000u,
			0x3f99999au, 0x3f666666u, 0x00000000u, 0x3f800000u, 0x3f785b3fu, 0xbe785b6au, 0x00000000u, 0x00000000u,
			0xbe785b6cu, 0xbf785b40u, 0x00000000u, 0x00000000u, 0x3ed31a40u, 0x41234567u, 0x41234567u, 0x41234567u,
		};
		Check( points, expected, 4 );
	}

	[Fact]
	public void ConsecutiveDirectionalSegmentsMergeAndDegenerateNormalsUseUpAxis()
	{
		var points = new Vector3[]
		{
			new( 0f, 2f, 0f ),
			new( 0.1f, 1.6f, 0f ),
			new( 0.2f, 1.1f, 0f ),
			new( 0.2f, 1.1f, 1f ),
			new( 0.2f, 1.1f, 2f ),
			new( 0.1f, 1.3f, 2f ),
		};
		var expected = new uint[]
		{
			0x00000002u, 0x41234567u, 0x41234567u, 0x41234567u, 0x00000000u, 0x40000000u, 0x00000000u, 0x3f800000u,
			0x3e4ccccdu, 0x3f8ccccdu, 0x00000000u, 0x3f800000u, 0x3e5e2305u, 0xbf79e765u, 0x00000000u, 0x00000000u,
			0xbf79e765u, 0xbe5e2305u, 0x00000000u, 0x00000000u, 0x3f6c0534u, 0x41234567u, 0x41234567u, 0x41234567u,
			0x00000001u, 0x41234567u, 0x41234567u, 0x41234567u, 0x3e4ccccdu, 0x3f8ccccdu, 0x00000000u, 0x3f800000u,
			0x3dcccccdu, 0x3fa66666u, 0x40000000u, 0x3f800000u, 0xbd4b8829u, 0x3dcb8824u, 0x3f7e6a33u, 0x00000000u,
			0x3f64f92cu, 0x3ee4f932u, 0x00000000u, 0x00000000u, 0x4000cc2au, 0x41234567u, 0x41234567u, 0x41234567u,
		};
		Check( points, expected, 0 );
	}

	[Fact]
	public void SingleCandidateClearsCountsAndPreservesOutputStorage()
	{
		var output = new uint[ 24 ];
		Array.Fill( output, 0x41234567u );
		var count = PhysicalGroundQuerySegments.Build( new uint[ 16 ], 1,
			Vector4.UnitX, Vector4.UnitY, Vector4.UnitZ, output, out var lastNeutralEnd );
		Assert.Equal( 0, count );
		Assert.Equal( 0, lastNeutralEnd );
		Assert.All( output, word => Assert.Equal( 0x41234567u, word ) );
	}

	private static void Check( Vector3[] points, uint[] expected, int expectedLastNeutralEnd )
	{
		var candidates = new uint[ points.Length * 16 ];
		for ( var index = 0; index < points.Length; index++ )
		{
			candidates[ index * 16 ] = BitConverter.SingleToUInt32Bits( points[ index ].X );
			candidates[ index * 16 + 1 ] = BitConverter.SingleToUInt32Bits( points[ index ].Y );
			candidates[ index * 16 + 2 ] = BitConverter.SingleToUInt32Bits( points[ index ].Z );
			candidates[ index * 16 + 3 ] = 0x3f800000;
		}
		var output = new uint[ 64 * 24 ];
		Array.Fill( output, 0x41234567u );
		var count = PhysicalGroundQuerySegments.Build( candidates, points.Length,
			Vector4.UnitX, Vector4.UnitY, Vector4.UnitZ, output, out var lastNeutralEnd );
		Assert.Equal( expected.Length / 24, count );
		Assert.Equal( expectedLastNeutralEnd, lastNeutralEnd );
		Assert.Equal( expected, output.AsSpan( 0, expected.Length ).ToArray() );
		Assert.All( output.AsSpan( expected.Length ).ToArray(), word => Assert.Equal( 0x41234567u, word ) );
	}
}
