using System;
using SkateMotion.Core.Collision;
using Xunit;

namespace SboxSkateMotion.Tests;

public class CollisionShapeProjectionTests
{
	[Theory]
	[InlineData( 1, 0f, 0f )]
	[InlineData( 2, -1.25f, 1.25f )]
	[InlineData( 3, 0f, 1f )]
	[InlineData( 4, -.5f, .5f )]
	[InlineData( 5, -1.25f, 1.25f )]
	public void ConstructedShapesProjectTheirCoreGeometry( int kind, float minimum, float maximum )
	{
		var shape = Shape( kind );
		var interval = new uint[ 8 ];
		CollisionShapeProjection.Project( shape, [0, 0, Word( 1f ), 0], interval );
		for ( var lane = 0; lane < 4; ++lane )
		{
			Assert.Equal( Word( minimum ), interval[ lane ] );
			Assert.Equal( Word( maximum ), interval[ lane + 4 ] );
		}
	}

	[Fact]
	public void BoxBatchPreservesScalingBeforeProjection()
	{
		var shape = Shape( 4 );
		uint[] geometry =
		[
			0xbf800000, 0x40a00000, 0x40000000, 0x3f800000,
			0x40000000, 0x3dcccccd, 0, 0,
			0, 0x3f000000, 0x3e4ccccd, 0,
			0x3e99999a, 0, 0x3fc00000, 0
		];
		geometry.CopyTo( shape, 0 );
		uint[] direction = [Word( 1f ), Word( 1f ), Word( 1f ), 0x12345678];
		var scalar = new uint[ 8 ];
		var batch = new uint[ 12 ];
		CollisionShapeProjection.Project( shape, direction, scalar );
		CollisionShapeProjection.ProjectBatch( shape, direction, batch );
		Assert.Equal( 0x3ff999a0u, scalar[ 0 ] );
		Assert.Equal( 0x3ff9999cu, batch[ 0 ] );
		Assert.Equal( 0x4120ccccu, scalar[ 4 ] );
		Assert.Equal( scalar[ 4 ], batch[ 4 ] );
	}

	[Fact]
	public void BatchLeavesThirdRowsAndTrailingStorageUntouched()
	{
		for ( var kind = 1; kind <= 5; ++kind )
		{
			var output = new uint[ 28 ];
			Array.Fill( output, 0x10203040u );
			CollisionShapeProjection.ProjectBatch( Shape( kind ), [Word( 1f ), 0, 0, 0, 0, 0, Word( 1f ), 0], output );
			for ( var index = 0; index < output.Length; ++index )
				if ( index is >= 8 and < 12 or >= 20 )
					Assert.Equal( 0x10203040u, output[ index ] );
		}
	}

	[Fact]
	public void EmptyBatchPreservesOutputForEveryPrimitive()
	{
		for ( var kind = 1; kind <= 5; ++kind )
		{
			uint[] output = [0x12345678u, 0x87654321u];
			CollisionShapeProjection.ProjectBatch( Shape( kind ), [], output );
			Assert.Equal( new uint[] { 0x12345678u, 0x87654321u }, output );
		}
	}

	[Fact]
	public void RoundRadiusIsNotAddedToCoreIntervals()
	{
		var shape = Shape( 1 );
		shape[ 32 ] = Word( 100f );
		var output = new uint[ 8 ];
		CollisionShapeProjection.Project( shape, [Word( 1f ), 0, 0, 0], output );
		Assert.Equal( new uint[ 8 ], output );
	}

	[Fact]
	public void CylinderDoesNotRenormalizeTheSuppliedDirection()
	{
		var output = new uint[ 8 ];
		CollisionShapeProjection.Project( Shape( 5 ), [Word( 1f ), Word( 1f ), Word( 1f ), 0], output );
		Assert.Equal( Word( -1.25f ), output[ 0 ] );
		Assert.Equal( Word( 1.25f ), output[ 4 ] );
	}

	private static uint Word( float value ) => BitConverter.SingleToUInt32Bits( value );

	private static uint[] Shape( int kind )
	{
		uint[] source =
		[
			Word( 1f ), 0, 0, 0, 0, Word( 1f ), 0, 0,
			0, 0, Word( 1f ), 0, 0, 0, 0, Word( 1f ),
			0, Word( 1.25f ), Word( .75f ), Word( .5f ), Word( .125f ), 0, 0, 0
		];
		var shape = new uint[ 44 ];
		Assert.True( CollisionShapeConstruction.TryConstruct( kind, source, shape, [], 0u ) );
		return shape;
	}
}
