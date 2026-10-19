using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class PhysicalBoardProbeResultTests
{
	[Fact]
	public void CompletedMixedBatchPublishesPackedContactsAndRetainsProbeOrigins()
	{
		var probes = State();
		probes.SkeletonFlags = 1UL << 61;
		var results = new Results( true );
		var contacts = new PhysicalBoardProbeContactState();
		PhysicalBoardProbeResultConsumer.Update( probes, contacts, results );
		Assert.Equal( new[] { -1, 0, 1, 2, 3, 4, 5 }, results.Events );
		Assert.Equal( new[] { .15f, 0f, .3f, -.025f }, contacts.Distances );
		Assert.Equal( new uint[] { 4, 0, 31, 0 }, contacts.SurfaceIndices );
		Assert.Equal( new uint[] { 52, 0, 127, 33 }, contacts.MaterialIndices );
		Assert.Equal( new uint[] { 1, 0, 7, 8 }, contacts.Flags );
		Assert.Equal( -.025f, contacts.ClosestDistance );
		Assert.Equal( Vector3.UnitY, contacts.Normals[ 1 ] );
		Assert.True( probes.Ground.Valid );
		Assert.True( probes.Forward.Valid );
		Assert.Equal( new Vector3( 5f, 6f, 7f ), probes.Ground.Position );
		Assert.Equal( new Vector3( 6f, 7f, 8f ), probes.Forward.Position );
		Assert.Equal( new Vector3( 7f, 8f, 9f ), probes.Ground.Origin );
		Assert.Equal( unchecked((int)0xabcdfedc), probes.Ground.Material );
		Assert.Equal( unchecked((int)0xabcd7654), probes.Forward.Material );
	}

	[Fact]
	public void MissesResetWheelObservationsAndValidityWithoutErasingRetainedCacheData()
	{
		var probes = State();
		var results = new Results( false );
		var contacts = new PhysicalBoardProbeContactState();
		Array.Fill( contacts.Distances, 17f );
		Array.Fill( contacts.Flags, 99U );
		PhysicalBoardProbeResultConsumer.Update( probes, contacts, results );
		Assert.Equal( new[] { -1, 0, 1, 2, 3, 4 }, results.Events );
		Assert.Equal( .2f, contacts.ClosestDistance );
		Assert.All( contacts.Normals, normal => Assert.Equal( Vector3.UnitY, normal ) );
		Assert.All( contacts.Distances, distance => Assert.Equal( 0f, distance ) );
		Assert.All( contacts.Flags, value => Assert.Equal( 0U, value ) );
		Assert.False( probes.Ground.Valid );
		Assert.True( probes.Forward.Valid );
		Assert.Equal( new Vector3( 10f, 11f, 12f ), probes.Ground.Position );
		Assert.Equal( new Vector3( 13f, 14f, 15f ), probes.Ground.Normal );
		Assert.Equal( unchecked((int)0x87654321), probes.Ground.Material );
		Assert.Equal( probes.Ground.Position, probes.Forward.Position );
	}

	private static PhysicalBoardProbeState State()
	{
		var state = new PhysicalBoardProbeState();
		foreach ( var cache in new[] { state.Ground, state.Forward } )
		{
			cache.Origin = new( 7f, 8f, 9f );
			cache.Position = new( 10f, 11f, 12f );
			cache.Normal = new( 13f, 14f, 15f );
			cache.Material = unchecked((int)0x87654321);
			cache.Valid = true;
		}
		return state;
	}

	private sealed class Results( bool mixed ) : IPhysicalBoardProbeResults
	{
		internal List<int> Events { get; } = new();
		private bool _completed;
		public void Complete() { Events.Add( -1 ); _completed = true; }
		public bool TryGet( int index, out PhysicalBoardProbeHit hit )
		{
			Assert.True( _completed );
			Events.Add( index );
			var record = new byte[ 0x80 ];
			for ( var axis = 0; axis < 3; axis++ )
			{
				WriteFloat( record, axis * 4, index + axis + 1f );
				WriteFloat( record, 0x10 + axis * 4, axis == 1 ? 1f : 0f );
			}
			WriteFloat( record, 0x60, new[] { .75f, .25f, 1.5f, -.125f, .6f, .9f }[ index ] );
			BinaryPrimitives.WriteUInt32BigEndian( record.AsSpan( 0x64 ),
				0xabcd0000U | new uint[] { 0x1234, 0xfabc, 0x7fff, 0x8021, 0xfedc, 0x7654 }[ index ] );
			BinaryPrimitives.WriteUInt32BigEndian( record.AsSpan( 0x70 ), mixed && index != 1 ? 2U : 0U );
			return PhysicalBoardProbeResultDecoder.TryDecode( record, out hit );
		}

		private static void WriteFloat( byte[] record, int offset, float value ) =>
			BinaryPrimitives.WriteInt32BigEndian( record.AsSpan( offset ), BitConverter.SingleToInt32Bits( value ) );
	}
}
