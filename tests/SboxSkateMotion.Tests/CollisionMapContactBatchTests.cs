using SkateMotion.Core.Collision;
using Xunit;

namespace SboxSkateMotion.Tests;

public class CollisionMapContactBatchTests
{
	[Fact]
	public void ContactsKeepTriangleThenBodyOrderAndStableIdentities()
	{
		var first = Triangle( 0x01230000u );
		var second = Triangle( 0x01240000u );
		var collector = new Collector( 8 );
		CollisionMapContactBatch.Generate( [ Body( 101 ), Body( 102 ) ], first.Concat( second ).ToArray(), Context(), collector );
		Assert.Equal( 4, collector.Count );
		Assert.Equal( 4, collector.Completed );
		Assert.Equal( new uint[] { 101, 102, 101, 102 }, Enumerable.Range( 0, 4 ).Select( i => collector.Words[ i * 64 + 3 ] ) );
		Assert.Equal( new uint[] { 0x12340123, 0x12340123, 0x12340124, 0x12340124 },
			Enumerable.Range( 0, 4 ).Select( i => collector.Words[ i * 64 + 23 ] ) );
		for ( var i = 0; i < 4; ++i )
		{
			Assert.Equal( 201u, collector.Words[ i * 64 + 7 ] );
			Assert.Equal( Word( .1f ), collector.Words[ i * 64 + 11 ] );
			Assert.Equal( Word( .7f ), collector.Words[ i * 64 + 15 ] );
			Assert.Equal( Word( .5f ), collector.Words[ i * 64 + 19 ] );
		}
	}

	[Theory]
	[InlineData( 0 )]
	[InlineData( 1 )]
	[InlineData( 2 )]
	public void DisabledSpecialAndStaticBodiesDoNotAllocate( int gate )
	{
		var body = Body( 1 );
		if ( gate == 0 ) body = body with { Disabled = true };
		if ( gate == 1 ) body = body with { ObjectType = 6 };
		if ( gate == 2 )
		{
			var state = body.BodyState.ToArray();
			state[ 35 ] = 1;
			body = body with { BodyState = state };
		}
		var collector = new Collector( 4 );
		CollisionMapContactBatch.Generate( [ body ], Triangle(), Context(), collector );
		Assert.Equal( 0, collector.Attempts );
	}

	[Fact]
	public void InactiveBodyParticipatesOnlyWhenItIsTheOnlyBody()
	{
		var inactive = Body( 1 ) with { Active = false };
		var collector = new Collector( 4 );
		CollisionMapContactBatch.Generate( [ inactive ], Triangle(), Context(), collector );
		Assert.Equal( 1, collector.Count );
		var multiple = new Collector( 4 );
		CollisionMapContactBatch.Generate( [ inactive, Body( 2 ) ], Triangle(), Context(), multiple );
		Assert.Equal( 1, multiple.Count );
		Assert.Equal( 2u, multiple.Words[ 3 ] );
	}

	[Fact]
	public void AllocationFailureStopsTheEntireBatch()
	{
		var collector = new Collector( 1 );
		var triangles = Triangle().Concat( Triangle() ).ToArray();
		CollisionMapContactBatch.Generate( [ Body( 1 ), Body( 2 ) ], triangles, Context(), collector );
		Assert.Equal( 1, collector.Count );
		Assert.Equal( 1, collector.Completed );
		Assert.Equal( 2, collector.Attempts );
	}

	[Theory]
	[InlineData( false )]
	[InlineData( true )]
	public void BothCategoryTablesCanRejectBeforeAllocation( bool triangleFilter )
	{
		var table = new uint[ 36 ];
		table[ 1 ] = 32;
		table[ 4 ] = 1u << (triangleFilter ? 5 : 2);
		var context = Context() with { Category = 2 };
		context = triangleFilter ? context with { TriangleFilter = table } : context with { BodyFilter = table };
		var collector = new Collector( 4 );
		CollisionMapContactBatch.Generate( [ Body( 1 ) with { Category = 1 } ], Triangle( 5u << 23 ), context, collector );
		Assert.Equal( 0, collector.Attempts );
	}

	[Fact]
	public void ApproachingVelocityExtendsTheContactMargin()
	{
		var body = Body( 1, .7f );
		var stopped = new Collector( 4 );
		CollisionMapContactBatch.Generate( [ body ], Triangle(), Context(), stopped );
		Assert.Equal( 0, stopped.Count );
		var state = body.BodyState.ToArray();
		state[ 10 ] = Word( -60 );
		var moving = new Collector( 4 );
		CollisionMapContactBatch.Generate( [ body with { BodyState = state } ], Triangle(), Context(), moving );
		Assert.Equal( 1, moving.Count );
	}

	[Fact]
	public void SuppliedBoundsRejectSeparatedTriangles()
	{
		var body = Body( 1 ) with { Bounds = new uint[] { Word( 20 ), Word( 20 ), Word( 20 ), 0, Word( 30 ), Word( 30 ), Word( 30 ), 0 } };
		var collector = new Collector( 4 );
		CollisionMapContactBatch.Generate( [ body ], Triangle(), Context(), collector );
		Assert.Equal( 0, collector.Attempts );
	}

	private static CollisionMapBodyInput Body( uint identity, float height = .1f )
	{
		var shape = new uint[ 24 ];
		shape[ 12 ] = Word( .5f );
		shape[ 13 ] = Word( .5f );
		shape[ 14 ] = Word( height );
		shape[ 15 ] = Word( 1 );
		shape[ 20 ] = Word( .125f );
		var transform = new uint[ 16 ];
		for ( var i = 0; i < 4; ++i ) transform[ i * 5 ] = Word( 1 );
		var state = new uint[ 44 ];
		shape.AsSpan( 12, 4 ).CopyTo( state.AsSpan( 4 ) );
		return new CollisionMapBodyInput
		{
			Shape = shape, ShapeKind = 1, ObjectType = 1, Active = true,
			Transform = transform, BodyState = state, BodyIdentity = identity,
			Bounds = new uint[] { Word( -10 ), Word( -10 ), Word( -10 ), 0, Word( 10 ), Word( 10 ), Word( 10 ), 0 },
			BaseMargin = Word( .05f ), StaticFriction = Word( .4f ), SlidingFriction = Word( .3f ), Restitution = Word( .1f ), Material = 0x1234
		};
	}

	private static CollisionMapContactContext Context() => new()
	{
		BodyState = new uint[ 44 ], BodyIdentity = 201,
		StaticFriction = Word( .7f ), SlidingFriction = Word( .5f ), Restitution = Word( .2f ), MaximumExtension = Word( .5f )
	};

	private static uint[] Triangle( uint material = 0 )
	{
		var source = new uint[ 24 ];
		source[ 4 ] = Word( 2 );
		source[ 9 ] = Word( 3 );
		source[ 3 ] = source[ 7 ] = source[ 11 ] = Word( 1 );
		source[ 20 ] = Word( .125f );
		source[ 23 ] = 2;
		var result = new uint[ 44 ];
		CollisionShapeConstruction.TryConstruct( 3, source, result, ReadOnlySpan<uint>.Empty, 0 );
		result[ 34 ] = material;
		return result;
	}

	private static uint Word( float value ) => BitConverter.SingleToUInt32Bits( value );

	private sealed class Collector( int capacity ) : ICollisionContactCollector
	{
		internal uint[] Words { get; } = new uint[ capacity * 64 ];
		internal int Count { get; private set; }
		internal int Completed { get; private set; }
		internal int Attempts { get; private set; }

		public Span<uint> AllocateContact()
		{
			++Attempts;
			return Count == capacity ? Span<uint>.Empty : Words.AsSpan( Count++ * 64, 64 );
		}

		public void CompleteContact() => ++Completed;
	}
}
