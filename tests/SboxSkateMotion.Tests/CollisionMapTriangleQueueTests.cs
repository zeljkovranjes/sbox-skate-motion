using SkateMotion.Core.Collision;
using Xunit;

namespace SboxSkateMotion.Tests;

public class CollisionMapTriangleQueueTests
{
	[Theory]
	[InlineData( 0, 0 )]
	[InlineData( 1, 0 )]
	[InlineData( 19, 0 )]
	[InlineData( 20, 0 )]
	[InlineData( 21, 20 )]
	[InlineData( 40, 20 )]
	[InlineData( 41, 40 )]
	public void FullBatchesAndFinalRemainderKeepContactOrder( int triangles, int generatedBeforeFinal )
	{
		var state = CollectorState( 50 );
		var queue = Queue( state );
		for ( var i = 0; i < triangles; ++i ) queue.Enqueue( Triangle( (ushort)i ) );
		Assert.Equal( (uint)generatedBeforeFinal, state[ 0 ] );
		queue.GeneratePending();
		Assert.Equal( (uint)triangles, state[ 0 ] );
		Assert.Equal( (uint)triangles, queue.TotalTriangles );
		for ( var i = 0; i < triangles; ++i )
			Assert.Equal( 0x12340000u | (uint)i, state[ 32 + i * 64 + 23 ] );
	}

	[Fact]
	public void ExplicitGenerationRetainsPendingBatch()
	{
		var state = CollectorState( 50 );
		var queue = Queue( state );
		queue.Enqueue( Triangle( 7 ) );
		queue.GeneratePending();
		queue.GeneratePending();
		Assert.Equal( 2u, state[ 0 ] );
		Assert.Equal( 1, queue.Count );
		Assert.Equal( 1u, queue.TotalTriangles );
		Assert.Equal( state.AsSpan( 32, 64 ).ToArray(), state.AsSpan( 96, 64 ).ToArray() );
	}

	[Fact]
	public void ExhaustedCollectorStillAllowsTheNextTriangleToBeQueued()
	{
		var state = CollectorState( 5 );
		var queue = Queue( state );
		for ( var i = 0; i < 21; ++i ) queue.Enqueue( Triangle( (ushort)i ) );
		Assert.Equal( 5u, state[ 0 ] );
		Assert.Equal( 1, queue.Count );
		Assert.Equal( Triangle( 20 ), queue.Pending.ToArray() );
		Assert.Equal( 21u, queue.TotalTriangles );
	}

	[Fact]
	public void QueueOwnsCopiedFieldsAndRetainsDestinationPadding()
	{
		var queue = Queue( CollectorState( 50 ) );
		var triangle = Triangle( 8 );
		triangle[ 35 ] = 0x12345678;
		triangle[ 41 ] = 0xdeadbeef;
		triangle[ 42 ] = 0x7fa12345;
		triangle[ 43 ] = 0x12345678;
		var expected = triangle.ToArray();
		expected[ 35 ] = 0x12340000;
		expected[ 41 ] = expected[ 42 ] = expected[ 43 ] = 0;
		queue.Enqueue( triangle );
		Array.Clear( triangle );
		Assert.Equal( expected, queue.Pending.ToArray() );
	}

	[Fact]
	public void InvalidInputCannotFlushOrChangeAFullQueue()
	{
		var state = CollectorState( 50 );
		var queue = Queue( state );
		for ( var i = 0; i < 20; ++i ) queue.Enqueue( Triangle( (ushort)i ) );
		var before = queue.Pending.ToArray();
		Assert.Throws<ArgumentException>( () => queue.Enqueue( new uint[ 43 ] ) );
		var wrongKind = Triangle( 30 );
		wrongKind[ 36 ] = 1;
		Assert.Throws<ArgumentException>( () => queue.Enqueue( wrongKind ) );
		Assert.Equal( 0u, state[ 0 ] );
		Assert.Equal( 20u, queue.TotalTriangles );
		Assert.Equal( before, queue.Pending.ToArray() );
	}

	private static CollisionMapTriangleQueue Queue( uint[] collector )
	{
		var shape = new uint[ 24 ];
		shape[ 12 ] = shape[ 13 ] = Word( .5f );
		shape[ 14 ] = Word( .1f );
		shape[ 15 ] = Word( 1 );
		shape[ 20 ] = Word( .125f );
		var transform = new uint[ 16 ];
		for ( var i = 0; i < 4; ++i ) transform[ i * 5 ] = Word( 1 );
		var bodyState = new uint[ 44 ];
		shape.AsSpan( 12, 4 ).CopyTo( bodyState.AsSpan( 4 ) );
		var body = new CollisionMapBodyInput
		{
			Shape = shape, Transform = transform, ShapeKind = 1, ObjectType = 1, Active = true,
			BodyState = bodyState, BodyIdentity = 101, Material = 0x1234,
			Bounds = new uint[] { Word( -10 ), Word( -10 ), Word( -10 ), 0, Word( 10 ), Word( 10 ), Word( 10 ), 0 },
			BaseMargin = Word( .05f ), StaticFriction = Word( .4f ), SlidingFriction = Word( .3f ), Restitution = Word( .1f )
		};
		var context = new CollisionMapContactContext
		{
			BodyState = new uint[ 44 ], BodyIdentity = 201, MaximumExtension = Word( .5f ),
			StaticFriction = Word( .7f ), SlidingFriction = Word( .5f ), Restitution = Word( .2f )
		};
		return new CollisionMapTriangleQueue( new[] { body }, context, new CollisionContactCollector( collector, null ) );
	}

	private static uint[] CollectorState( uint capacity )
	{
		var result = new uint[ 0x3290 / 4 ];
		result[ 2 ] = capacity;
		result[ 4 ] = Word( -1 );
		return result;
	}

	private static uint[] Triangle( ushort surface )
	{
		var source = new uint[ 24 ];
		source[ 4 ] = Word( 2 );
		source[ 9 ] = Word( 3 );
		source[ 3 ] = source[ 7 ] = source[ 11 ] = Word( 1 );
		source[ 20 ] = Word( .125f );
		source[ 23 ] = 2;
		var result = new uint[ 44 ];
		CollisionShapeConstruction.TryConstruct( 3, source, result, ReadOnlySpan<uint>.Empty, 0 );
		result[ 34 ] = (uint)surface << 16;
		return result;
	}

	private static uint Word( float value ) => BitConverter.SingleToUInt32Bits( value );
}
