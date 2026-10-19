using System;
using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class PhysicalGroundQueryCandidateWriterTests
{
	[Fact]
	public void AdmittedCandidateStoresProjectedSortKeyAndPreservesPadding()
	{
		var owner = CreateOwner();
		var x = BitConverter.UInt32BitsToSingle( 0x3dccccd0 );
		var y = BitConverter.UInt32BitsToSingle( 0x3ca3d70d );
		Assert.Equal( 0, PhysicalGroundQueryCandidateWriter.Append( owner,
			new( x, y, 0, 1 ), Vector4.UnitY, new( x, y, 0, 0 ), 0, 0, 9 ) );
		Assert.Equal( 1u, owner[ 0x76f0 / 4 ] );
		foreach ( var offset in new[] { 0x62d4, 0x62d8, 0x62dc } )
			Assert.Equal( 0x3dccccd0u, owner[ offset / 4 ] );
		Assert.Equal( 0u, owner[ 0x62e0 / 4 ] );
		Assert.Equal( 9u, owner[ 0x62e4 / 4 ] );
		Assert.Equal( 0x41234567u, owner[ 0x62e8 / 4 ] );
		Assert.Equal( 0x41234567u, owner[ 0x62ec / 4 ] );
	}

	[Fact]
	public void NearPointUsesQueryAxisAndExpandsIntervalToSegmentEndpoint()
	{
		var owner = CreateOwner();
		Assert.Equal( 0, PhysicalGroundQueryCandidateWriter.AppendPoint( owner,
			new( .001f, 0, 0, 1 ), Vector4.UnitY, 1, 64, 2 ) );
		Assert.Equal( 0x3f800000u, owner[ 0x62c0 / 4 ] );
		Assert.Equal( 0u, owner[ 0x62c4 / 4 ] );
		Assert.Equal( BitConverter.SingleToUInt32Bits( .001f ), owner[ 0x62d4 / 4 ] );
		Assert.Equal( 0x3f800000u, owner[ 0x62d8 / 4 ] );
		Assert.Equal( BitConverter.SingleToUInt32Bits( .001f ), owner[ 0x62dc / 4 ] );
		Assert.Equal( 1u, owner[ 0x62e0 / 4 ] );
		Assert.Equal( 64u, owner[ 0x62d0 / 4 ] );
	}

	private static uint[] CreateOwner()
	{
		var owner = new uint[ PhysicalGroundQuerySelection.OwnerWordCount ];
		Array.Fill( owner, 0x41234567u );
		void V( int offset, Vector4 value )
		{
			owner[ offset / 4 ] = BitConverter.SingleToUInt32Bits( value.X );
			owner[ offset / 4 + 1 ] = BitConverter.SingleToUInt32Bits( value.Y );
			owner[ offset / 4 + 2 ] = BitConverter.SingleToUInt32Bits( value.Z );
			owner[ offset / 4 + 3 ] = BitConverter.SingleToUInt32Bits( value.W );
		}
		V( 0, new( 0, 0, 0, 1 ) );
		V( 16, Vector4.UnitX );
		owner[ 0x76f0 / 4 ] = 0;
		owner[ 0x6294 / 4 ] = 2;
		owner[ 0x4a90 / 4 ] = 0;
		owner[ 0x4af0 / 4 ] = 1;
		V( 0x4b10, new( 1, .2f, 0, 1 ) );
		return owner;
	}
}
