using System;
using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class PhysicalGroundQuerySelectionTests
{
	[Theory]
	[InlineData( false, 2, 1u, 0x7fu, 0x47fu )]
	[InlineData( true, 0, 8u, 0x207u, 0x206u )]
	public void SelectionPreservesIdentityOverlapHistoryAndEmptyFallback(
		bool overhead, int selected, uint classification, uint flags, uint emptyFlags )
	{
		var owner = CreateOwner( overhead );
		for ( var tick = 0; tick < 4; tick++ )
		{
			Assert.Equal( selected, PhysicalGroundQuerySelection.SelectOrdered( owner ) );
			Assert.Equal( flags, owner[ 0x120 / 4 ] );
			Assert.Equal( tick == 3 ? classification : 0u, owner[ 0x114 / 4 ] );
		}
		Assert.Equal( classification, owner[ 0x76f4 / 4 ] );
		Assert.Equal( owner.AsSpan( (0x62a0 + selected * 80) / 4, 18 ).ToArray(), owner.AsSpan( 0x76a0 / 4, 18 ).ToArray() );
		Assert.Equal( owner.AsSpan( (0x62a0 + selected * 80) / 4, 8 ).ToArray(), owner.AsSpan( 0xd0 / 4, 8 ).ToArray() );
		Assert.Equal( 0x41234567u, owner[ 0x76e8 / 4 ] );
		owner[ 0x76f0 / 4 ] = 0;
		Assert.Equal( -1, PhysicalGroundQuerySelection.SelectOrdered( owner ) );
		Assert.Equal( emptyFlags, owner[ 0x120 / 4 ] );
		Assert.Equal( classification, owner[ 0x114 / 4 ] );
	}

	[Fact]
	public void PublicationRetainsStaleRecordsAndOnlyReplacesFallbackFields()
	{
		var owner = CreateOwner( false );
		PhysicalGroundQuerySelection.SelectOrdered( owner );
		var retained = new uint[ PhysicalGroundQueryPublication.RecordWordCount ];
		Assert.True( PhysicalGroundQueryPublication.Update( owner, 1, retained, 500, true,
			new( 9, 8, 7, 1 ), Vector4.UnitY ) );
		Assert.Equal( owner.AsSpan( 0x70 / 4, retained.Length ).ToArray(), retained );
		var previous = (uint[])retained.Clone();
		Assert.True( PhysicalGroundQueryPublication.Update( owner, 0, retained, 501, true,
			new( 9, 8, 7, 1 ), Vector4.UnitY ) );
		Assert.Equal( previous, retained );
		Assert.True( PhysicalGroundQueryPublication.Update( owner, 0, retained, 500, false,
			new( 9, 8, 7, 1 ), Vector4.UnitY ) );
		Assert.Equal( previous, retained );
		PhysicalGroundQueryPublication.Update( owner, 0, retained, 500, true, new( 9, 8, 7, 1 ), Vector4.UnitY );
		Assert.Equal( BitConverter.SingleToUInt32Bits( 9f ), retained[ 0 ] );
		Assert.Equal( 1u, retained[ 0xb0 / 4 ] );
		Assert.Equal( previous.AsSpan( 8, 36 ).ToArray(), retained.AsSpan( 8, 36 ).ToArray() );
		Assert.Equal( previous[ 45 ], retained[ 45 ] );
	}

	private static uint[] CreateOwner( bool overhead )
	{
		var owner = new uint[ PhysicalGroundQuerySelection.OwnerWordCount ];
		Array.Fill( owner, 0x41234567u );
		void U( int offset, uint value ) => owner[ offset / 4 ] = value;
		void F( int offset, float value ) => U( offset, BitConverter.SingleToUInt32Bits( value ) );
		void V( int offset, Vector4 value )
		{
			F( offset, value.X );
			F( offset + 4, value.Y );
			F( offset + 8, value.Z );
			F( offset + 12, value.W );
		}
		foreach ( var offset in new[] { 0x3a54, 0x4a60, 0x76f4, 0x76f8, 0x76fc, 0x7700, 0x7704 } )
			U( offset, 0 );
		V( 0, new( 0, 0, 0, 1 ) );
		V( 16, overhead ? new( .8f, .6f, 0, 0 ) : Vector4.UnitX );
		V( 32, Vector4.UnitY );
		V( 48, Vector4.UnitZ );
		V( 64, new( 6, 0, 0, 0 ) );
		V( 0x80, new( 1, .2f, 0, 0 ) );
		U( 0x120, overhead ? 1u : 64u );
		F( 0x11c, overhead ? .5f : .2f );
		U( 0x6290, 1 );
		U( 0x6294, 2 );
		for ( var index = 0; index < 2; index++ )
		{
			var offset = 0x4a90 + 96 * index;
			U( offset, (uint)index );
			V( offset + 16, new( index * .5f, index * .1f, 0, 1 ) );
			V( offset + 32, new( (index + 1) * .5f, (index + 1) * .1f, 0, 1 ) );
			V( offset + 48, new( 1, .2f, 0, 0 ) );
			V( offset + 64, Vector4.UnitY );
			F( offset + 80, .5f );
		}
		if ( overhead )
		{
			U( 0x3a54, 1 );
			V( 0x2a50, new( 0, 2, 0, 1 ) );
			V( 0x2a60, Vector4.UnitY );
			U( 0x2a80, 0 );
		}
		var count = overhead ? 1 : 3;
		U( 0x76f0, (uint)count );
		for ( var index = 0; index < count; index++ )
		{
			var offset = 0x62a0 + 80 * index;
			V( offset, new( .3f + .4f * index, .1f + .2f * index, 0, 1 ) );
			V( offset + 16, Vector4.UnitY );
			V( offset + 32, new( 1, overhead ? -.8f : .2f, 0, 0 ) );
			U( offset + 48, 0 );
			F( offset + 52, .5f * index );
			F( offset + 56, 2 );
			F( offset + 60, index );
			U( offset + 64, index == 1 ? 8u : 7u );
			U( offset + 68, index == 0 && !overhead ? 1u : 4u );
		}
		return owner;
	}
}
