using System;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class PhysicalGroundQueryAdmissionTests
{
	[Fact]
	public void StationarySupportRetainsPointAndDoesNotRequireSegments()
	{
		var owner = new uint[ PhysicalGroundQuerySelection.OwnerWordCount ];
		owner[ 0x120 / 4 ] = 1;
		owner[ 0x20 / 4 + 1 ] = 0x3f800000;
		owner[ 0x30 / 4 + 2 ] = 0x3f800000;
		owner[ 0x70 / 4 + 3 ] = 0x3f800000;
		owner[ 0x80 / 4 + 1 ] = 0x3f800000;
		owner[ 0x62e8 / 4 ] = 0x12345678;
		Assert.Equal( 1, PhysicalGroundQueryAdmission.Build( owner ) );
		Assert.Equal( 0xbf800000u, owner[ 0x62c0 / 4 ] );
		Assert.Equal( uint.MaxValue, owner[ 0x62e0 / 4 ] );
		Assert.Equal( 0x12345678u, owner[ 0x62e8 / 4 ] );
	}

	[Fact]
	public void PartitionSortRetainsPaddingAtItsDestination()
	{
		var owner = new uint[ PhysicalGroundQuerySelection.OwnerWordCount ];
		owner[ 0x76f0 / 4 ] = 33;
		for ( var index = 0; index < 33; index++ )
		{
			var offset = 0x62a0 / 4 + index * 20;
			owner[ offset ] = (uint)index;
			owner[ offset + 15 ] = BitConverter.SingleToUInt32Bits( (index * 17) % 7 );
			owner[ offset + 18 ] = (uint)(100 + index);
		}
		PhysicalGroundQueryCandidateSort.Sort( owner );
		var previous = float.NegativeInfinity;
		for ( var index = 0; index < 33; index++ )
		{
			var offset = 0x62a0 / 4 + index * 20;
			var key = BitConverter.UInt32BitsToSingle( owner[ offset + 15 ] );
			Assert.True( key >= previous );
			Assert.Equal( (uint)(100 + index), owner[ offset + 18 ] );
			previous = key;
		}
	}

	[Fact]
	public void OrderedSamplesRunThroughAdmissionAndSupportPublication()
	{
		var owner = new uint[ PhysicalGroundQuerySelection.OwnerWordCount ];
		owner[ 3 ] = 0x3f800000;
		owner[ 4 ] = 0x3f800000;
		owner[ 9 ] = 0x3f800000;
		owner[ 14 ] = 0x3f800000;
		owner[ 16 ] = BitConverter.SingleToUInt32Bits( 6f );
		owner[ 0x3a50 / 4 ] = 2;
		owner[ 0x2a50 / 4 + 3 ] = 0x3f800000;
		owner[ 0x2a90 / 4 ] = 0x3f800000;
		owner[ 0x2a90 / 4 + 3 ] = 0x3f800000;
		owner[ 0x11c / 4 ] = BitConverter.SingleToUInt32Bits( 0.2f );
		Assert.Equal( 0, PhysicalGroundQueryPipeline.Select( owner ) );
		Assert.Equal( 1u, owner[ 0x6294 / 4 ] );
		Assert.Equal( 1u, owner[ 0x76f0 / 4 ] );
		var retained = new uint[ PhysicalGroundQueryPublication.RecordWordCount ];
		PhysicalGroundQueryPublication.Update( owner, 1, retained, 500, false, default, default );
		Assert.Equal( owner[ 0xd0 / 4 ], retained[ 0x60 / 4 ] );
		Assert.Equal( owner[ 0x120 / 4 ], retained[ 0xb0 / 4 ] );
	}
}
