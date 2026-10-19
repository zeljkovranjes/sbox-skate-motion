using System;
using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class GrindQueryFanTests
{
	private static readonly Vector4 Sentinel = new( 17, 18, 19, 20 );

	[Theory]
	[InlineData( 0 )]
	[InlineData( 1 )]
	public void FirstDirectionIsCopiedEvenWhenNoQueriesAreRequested( ushort requested )
	{
		var directions = Storage();
		var velocity = new Vector4( 2, 3, 4, 7 );
		var count = Create().Build( velocity, Vector4.Zero, requested, false, 30, 1, directions, out var height );
		Assert.Equal( (int)requested, count );
		Assert.Equal( velocity, directions[ 0 ] );
		Assert.Equal( -100, height );
		for ( var i = 1; i < directions.Length; i++ )
			Assert.Equal( Sentinel, directions[ i ] );
	}

	[Theory]
	[InlineData( 7 )]
	[InlineData( 8 )]
	[InlineData( 65535 )]
	public void UnsignedRequestIsCappedAtSevenAndLeavesExtraStorageUntouched( ushort requested )
	{
		var directions = Storage();
		Assert.Equal( 7, Create().Build( Vector4.UnitX, Vector4.Zero, requested, false, 30, 1, directions, out _ ) );
		Assert.Equal( Sentinel, directions[ 7 ] );
	}

	[Fact]
	public void ZeroVelocityProducesFiniteZeroDirections()
	{
		var directions = Storage();
		Create().Build( Vector4.Zero, new( 100 ), 7, true, 90, 1, directions, out _ );
		for ( var i = 0; i < 7; i++ )
			Assert.Equal( Vector4.Zero, directions[ i ] );
	}

	[Fact]
	public void VerticalVelocityHasNoHorizontalFan()
	{
		var directions = Storage();
		Create().Build( new( 0, 2, 0, 0 ), Vector4.Zero, 7, false, 90, 10, directions, out _ );
		for ( var i = 0; i < 7; i++ )
			Assert.True( Vector4.Distance( new( 0, 2, 0, 0 ), directions[ i ] ) < 1e-6f );
	}

	[Fact]
	public void FanSweepsBothSidesWithoutExceedingOriginalSpeed()
	{
		var directions = Storage();
		Create().Build( new( 2, 0, 0, 0 ), Vector4.Zero, 5, false, 90, 0, directions, out _ );
		Assert.True( directions[ 2 ].Z < -.5f );
		Assert.True( directions[ 4 ].Z > .5f );
		for ( var i = 1; i < 5; i++ )
			Assert.InRange( directions[ i ].Length(), 0f, 2.000001f );
	}

	[Fact]
	public void SecondarySpeedChangesSpreadWhileRetainingTheFirstDirection()
	{
		var first = Storage();
		var second = Storage();
		var fan = Create();
		fan.Build( new( 2, 0, 0, 0 ), new( 10, 0, 0, 0 ), 5, false, 90, 0, first, out _ );
		fan.Build( new( 2, 0, 0, 0 ), new( 10, 0, 0, 0 ), 5, true, 90, 0, second, out _ );
		Assert.Equal( first[ 0 ], second[ 0 ] );
		Assert.True( MathF.Abs( second[ 2 ].Z ) > MathF.Abs( first[ 2 ].Z ) );
	}

	[Fact]
	public void AFollowingSmallerRequestRetainsUnusedDirections()
	{
		var directions = Storage();
		var fan = Create();
		fan.Build( new( 2, 1, 3, 0 ), Vector4.Zero, 7, false, 45, 2, directions, out _ );
		var retained = directions[ 3 ];
		fan.Build( new( -2, 0, 1, 4 ), Vector4.Zero, 2, false, 20, 1, directions, out _ );
		Assert.Equal( retained, directions[ 3 ] );
		Assert.Equal( Sentinel, directions[ 7 ] );
	}

	[Fact]
	public void StorageIsValidatedBeforeAnyDirectionIsWritten()
	{
		var directions = new[] { Sentinel };
		Assert.Throws<ArgumentException>( () => Create().Build( Vector4.UnitX, Vector4.Zero, 2, false, 30, 1, directions, out _ ) );
		Assert.Equal( Sentinel, directions[ 0 ] );
	}

	private static Vector4[] Storage() => [ Sentinel, Sentinel, Sentinel, Sentinel, Sentinel, Sentinel, Sentinel, Sentinel ];

	private static GrindQueryFan Create()
	{
		var math = new LimbSolverMath( [ -1f / 720f, 1f / 24f, -.5f ],
			[ -1f / 5040f, 1f / 120f, -1f / 6f ], [ 0, 0, 0, 0, 0, 0, 0, MathF.PI / 2 ],
			2 / MathF.PI, MathF.PI / 2, 0, MathF.PI );
		var curve = new GrindEdgeSelectionCurve( [ 0, 1, 2, 3, 4, 5, 6, 7 ], [ 1, 1, 1, 1, 1, 1, 1, 1 ] );
		return new( math, curve, new( 1e-6f, .1f, MathF.PI / 180, 2, 4, MathF.PI * 2, -100 ) );
	}
}
