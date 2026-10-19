using SkateMotion.Core.Collision;
using Xunit;

namespace SboxSkateMotion.Tests;

public class CollisionContactCollectorTests
{
	[Fact]
	public void AllocationPreservesBackingUntilWriterUsesIt()
	{
		var state = State( 1, 50 );
		state[ 96 ] = 123;
		var collector = new CollisionContactCollector( state, null );
		var record = collector.AllocateContact();
		Assert.Equal( 64, record.Length );
		Assert.Equal( 123u, record[ 0 ] );
		Assert.Equal( 2u, state[ 0 ] );
		record[ 0 ] = 456;
		Assert.Equal( 456u, state[ 96 ] );
	}

	[Fact]
	public void TotalCapacityExhaustionIncrementsRejectedCount()
	{
		var state = State( 1, 2 );
		state[ 1 ] = 1;
		var collector = new CollisionContactCollector( state, null );
		Assert.True( collector.AllocateContact().IsEmpty );
		Assert.Equal( 1u, state[ 3 ] );
		Assert.Equal( 0x100u, state[ 0x328c / 4 ] );
		Assert.True( collector.AllocateContact().IsEmpty );
		Assert.Equal( 2u, state[ 3 ] );
	}

	[Fact]
	public void FullInlineBufferWithoutStreamingDoesNotIncrementRejections()
	{
		var state = State( 50, 200 );
		var collector = new CollisionContactCollector( state, null );
		Assert.True( collector.AllocateContact().IsEmpty );
		Assert.Equal( 0u, state[ 3 ] );
		Assert.Equal( 50u, state[ 0 ] );
		Assert.Equal( 0x100u, state[ 0x328c / 4 ] );
	}

	[Fact]
	public void FullStreamingBufferPublishesBeforeReusingFirstSlot()
	{
		var state = State( 50, 200 );
		state[ 0x328c / 4 ] = 0x1000000;
		state[ 32 ] = 123;
		var output = new uint[ 128 * 64 ];
		var stream = new CollisionContactOutputStream( output );
		var collector = new CollisionContactCollector( state, stream );
		var record = collector.AllocateContact();
		Assert.Equal( 1u, state[ 0 ] );
		Assert.Equal( 50u, state[ 1 ] );
		Assert.Equal( 50u, stream.ReservedCount );
		Assert.Equal( 123u, output[ 0 ] );
		record[ 0 ] = 456;
		Assert.Equal( 456u, state[ 32 ] );
		Assert.Equal( 123u, output[ 0 ] );
	}

	[Fact]
	public void DeferredReductionFreesCapacityBeforeAllocation()
	{
		var state = State( 50, 50 );
		state[ 0x328c / 4 ] = 0x10000;
		for ( var i = 0; i < 50; ++i )
		{
			state[ 32 + i * 64 + 3 ] = (uint)(i % 3);
			state[ 32 + i * 64 + 10 ] = 0x3f800000;
		}
		var collector = new CollisionContactCollector( state, null );
		Assert.Equal( 64, collector.AllocateContact().Length );
		Assert.Equal( 4u, state[ 0 ] );
		Assert.Equal( 0u, state[ 3 ] );
	}

	[Fact]
	public void DuplicateCompletionDropsCountButPreservesWrittenSlot()
	{
		var state = State( 2, 50 );
		state[ 4 ] = 0x3f800000;
		state[ 42 ] = state[ 106 ] = 0x3f800000;
		state[ 159 ] = 123;
		var collector = new CollisionContactCollector( state, null );
		collector.CompleteContact();
		Assert.Equal( 1u, state[ 0 ] );
		Assert.Equal( 1u, state[ 3 ] );
		Assert.Equal( 123u, state[ 159 ] );
	}

	[Fact]
	public void OutputOverflowWritesOnlyAvailablePrefixAndClampsCounter()
	{
		var output = new uint[ 2 * 64 ];
		var input = Enumerable.Range( 1, 3 * 64 ).Select( i => (uint)i ).ToArray();
		var stream = new CollisionContactOutputStream( output, 1 );
		stream.Write( input );
		Assert.Equal( 2u, stream.ReservedCount );
		Assert.All( output.AsSpan( 0, 64 ).ToArray(), value => Assert.Equal( 0u, value ) );
		Assert.Equal( input.AsSpan( 0, 64 ).ToArray(), output.AsSpan( 64 ).ToArray() );
	}

	[Fact]
	public void ReservationBeyondCapacityCopiesNothingAndClampsOnWrite()
	{
		var output = new uint[ 64 ];
		var stream = new CollisionContactOutputStream( output, 3 );
		stream.Write( ReadOnlySpan<uint>.Empty );
		Assert.Equal( 3u, stream.ReservedCount );
		stream.Write( new uint[ 64 ] );
		Assert.Equal( 1u, stream.ReservedCount );
		Assert.All( output, value => Assert.Equal( 0u, value ) );
	}

	[Fact]
	public void FlushCountsAllPublishedCandidatesEvenIfOutputTruncates()
	{
		var state = State( 4, 200 );
		state[ 1 ] = 7;
		var stream = new CollisionContactOutputStream( new uint[ 2 * 64 ], 1 );
		new CollisionContactCollector( state, stream ).Flush();
		Assert.Equal( 0u, state[ 0 ] );
		Assert.Equal( 11u, state[ 1 ] );
		Assert.Equal( 2u, stream.ReservedCount );
	}

	[Fact]
	public void MissingStreamAndIncompleteRecordsFailBeforeMutation()
	{
		var state = State( 1, 20 );
		Assert.Throws<InvalidOperationException>( () => new CollisionContactCollector( state, null ).Flush() );
		Assert.Equal( 1u, state[ 0 ] );
		var stream = new CollisionContactOutputStream( new uint[ 64 ] );
		Assert.Throws<ArgumentException>( () => stream.Write( new uint[ 63 ] ) );
		Assert.Equal( 0u, stream.ReservedCount );
	}

	private static uint[] State( uint current, uint capacity )
	{
		var state = new uint[ 0x3290 / 4 ];
		state[ 0 ] = current;
		state[ 2 ] = capacity;
		state[ 4 ] = 0xbf800000;
		return state;
	}
}
