using SkateMotion.Core.Collision;
using Xunit;

namespace SboxSkateMotion.Tests;

public class CollisionContactCapacityTests
{
	[Fact]
	public void NearlyCoincidentOctagonsRejectOverflowWithoutChangingBuffers()
	{
		// Rounded edge normals produce repeated boundary crossings on coincident faces.
		uint[] edgeRecords =
		[
			0x3f800000u, 0x00000000u, 0x00000000u, 0x3f800000u,
			0xbec3ef15u, 0x3f6c835eu, 0x00000000u, 0x00000000u,
			0x3f6c835eu, 0x3ec3ef15u, 0x80000000u, 0x00000000u,
			0x3f43ef15u, 0x3f43ef15u, 0x3f43ef15u, 0x3f43ef15u,
			0x3f3504f3u, 0x3f3504f3u, 0x00000000u, 0x3f800000u,
			0xbf6c835eu, 0x3ec3ef15u, 0x00000000u, 0x00000000u,
			0x3ec3ef15u, 0x3f6c835eu, 0x80000000u, 0x00000000u,
			0x3f43ef15u, 0x3f43ef15u, 0x3f43ef15u, 0x3f43ef15u,
			0x248d3132u, 0x3f800000u, 0x00000000u, 0x3f800000u,
			0xbf6c835eu, 0xbec3ef15u, 0x00000000u, 0x00000000u,
			0xbec3ef15u, 0x3f6c835eu, 0x00000000u, 0x00000000u,
			0x3f43ef15u, 0x3f43ef15u, 0x3f43ef15u, 0x3f43ef15u,
			0xbf3504f3u, 0x3f3504f3u, 0x00000000u, 0x3f800000u,
			0xbec3ef15u, 0xbf6c835eu, 0x00000000u, 0x00000000u,
			0xbf6c835eu, 0x3ec3ef15u, 0x00000000u, 0x00000000u,
			0x3f43ef15u, 0x3f43ef15u, 0x3f43ef15u, 0x3f43ef15u,
			0xbf800000u, 0x250d3132u, 0x00000000u, 0x3f800000u,
			0x3ec3ef15u, 0xbf6c835eu, 0x00000000u, 0x00000000u,
			0xbf6c835eu, 0xbec3ef15u, 0x00000000u, 0x00000000u,
			0x3f43ef15u, 0x3f43ef15u, 0x3f43ef15u, 0x3f43ef15u,
			0xbf3504f3u, 0xbf3504f3u, 0x00000000u, 0x3f800000u,
			0x3f6c835eu, 0xbec3ef15u, 0x00000000u, 0x00000000u,
			0xbec3ef15u, 0xbf6c835eu, 0x00000000u, 0x00000000u,
			0x3f43ef15u, 0x3f43ef15u, 0x3f43ef15u, 0x3f43ef15u,
			0xa553c9cau, 0xbf800000u, 0x00000000u, 0x3f800000u,
			0x3f6c835eu, 0x3ec3ef15u, 0x00000000u, 0x00000000u,
			0x3ec3ef15u, 0xbf6c835eu, 0x00000000u, 0x00000000u,
			0x3f43ef15u, 0x3f43ef15u, 0x3f43ef15u, 0x3f43ef15u,
			0x3f3504f3u, 0xbf3504f3u, 0x00000000u, 0x3f800000u,
			0x3ec3ef15u, 0x3f6c835eu, 0x00000000u, 0x00000000u,
			0x3f6c835eu, 0xbec3ef15u, 0x00000000u, 0x00000000u,
			0x3f43ef15u, 0x3f43ef15u, 0x3f43ef15u, 0x3f43ef15u,
		];
		var first = new uint[ 144 ];
		edgeRecords.CopyTo( first, 4 );
		first[ 0 ] = 10;
		first[ 134 ] = 0x3f800000u;
		first[ 140 ] = 8;
		var second = first.AsSpan().ToArray();
		var firstBefore = first.AsSpan().ToArray();
		var secondBefore = second.AsSpan().ToArray();
		var output = new uint[ 140 ];
		Array.Fill( output, 0x12345678u );
		var before = output.AsSpan().ToArray();
		var exception = Assert.Throws<ArgumentException>( () => CollisionFeatureContacts.Assemble(
			first, second, new uint[] { 0, 0, 0x3f800000u, 0 }, output ) );
		Assert.Equal( "Polygon contacts exceed the fixed capacity of sixteen pairs.", exception.Message );
		Assert.Equal( before, output );
		Assert.Equal( firstBefore, first );
		Assert.Equal( secondBefore, second );
	}
}
