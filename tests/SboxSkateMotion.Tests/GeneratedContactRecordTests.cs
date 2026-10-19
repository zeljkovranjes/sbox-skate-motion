using System.Numerics;
using SkateMotion.Core.Animation;
using SkateMotion.Core.Constraints;
using SkateMotion.Core.Physics;
using Xunit;

namespace SboxSkateMotion.Tests;

public class GeneratedContactRecordTests
{
	[Fact]
	public void DecoderPreservesCachedFrameAndPackedBodyFields()
	{
		var words = Record();
		words[ 12 ] = 0x80000000;
		words[ 13 ] = 0x3eaaaaab;
		words[ 23 ] = 0xfedc1234;
		var result = GeneratedContactRecord.Decode( words, 9, 3 );
		Assert.Equal( 9, result.Geometry.BodyA );
		Assert.Equal( 3, result.Geometry.BodyB );
		Assert.Equal( words[ 12 ], BitConverter.SingleToUInt32Bits( result.Geometry.TangentA.X ) );
		Assert.Equal( words[ 13 ], BitConverter.SingleToUInt32Bits( result.Geometry.TangentA.Y ) );
		Assert.Equal( words[ 23 ], result.Geometry.Materials );
		Assert.Equal( new Vector3( 2, 3, 4 ), result.A.Center );
		Assert.Equal( new Matrix4x4( 1, .1f, .2f, 0, .1f, 2, .3f, 0, .2f, .3f, 3, 0, 0, 0, 0, 1 ), result.A.InverseInertia );
		Assert.Equal( new Vector3( 5, 6, 7 ), result.A.LinearMotion );
		Assert.Equal( new Vector3( 8, 9, 10 ), result.A.AngularMotion );
		Assert.Equal( .7f, result.Geometry.StaticFriction );
		Assert.Equal( .4f, result.Geometry.SlidingFriction );
		Assert.Equal( .2f, result.Geometry.Restitution );
	}

	[Fact]
	public void CachedResponseFlagControlsMassAndAcceleration()
	{
		var words = Record();
		words[ 43 ] = 8;
		var result = GeneratedContactRecord.Decode( words, 0, 1 );
		Assert.False( result.A.Responds );
		Assert.Equal( 8, result.A.MotionFlags );
		Assert.Equal( 2f, result.A.InverseMass );
		Assert.Equal( 0f, result.A.EffectiveInverseMass );
		Assert.Equal( Vector3.Zero, result.A.PointMotion( Vector3.One ) );
	}

	[Fact]
	public void ExplicitIdentityBindingsResolveWorldOrderAndUseCachedSnapshot()
	{
		var a = new PhysicalBodyState();
		var b = new PhysicalBodyState();
		var world = World( b, a );
		world.Prepare();
		var words = Record();
		var result = world.CompileGeneratedRecord( words,
			[new( words[ 3 ], a ), new( words[ 7 ], b )], .01f );
		Assert.Equal( 1, result.BodyA );
		Assert.Equal( 0, result.BodyB );
		Assert.Equal( new Vector3( -2, -3, -4 ), result.OffsetA );
		Assert.Equal( 2f, result.InverseMassA );
		Assert.True( result.ReportContact );
		Assert.Equal( Vector3.Zero, result.TangentA );
		Assert.Equal( Vector3.Zero, result.TangentB );
	}

	[Fact]
	public void MissingIdentityDoesNotUsePointerOrWorldIndexAsFallback()
	{
		var a = new PhysicalBodyState();
		var b = new PhysicalBodyState();
		var world = World( a, b );
		world.Prepare();
		Assert.Throws<ArgumentException>( () => world.CompileGeneratedRecord( Record(), [new( 0, a ), new( 1, b )], .01f ) );
	}

	[Fact]
	public void DuplicateIdentityIsRejectedEvenIfItNamesTheSameBody()
	{
		var a = new PhysicalBodyState();
		var b = new PhysicalBodyState();
		var world = World( a, b );
		world.Prepare();
		Assert.Throws<ArgumentException>( () => world.CompileGeneratedRecord( Record(), [new( 101, a ), new( 101, a ), new( 202, b )], .01f ) );
	}

	[Fact]
	public void BindingToAnotherWorldIsRejected()
	{
		var a = new PhysicalBodyState();
		var b = new PhysicalBodyState();
		var world = World( a, b );
		world.Prepare();
		Assert.Throws<ArgumentException>( () => world.CompileGeneratedRecord( Record(), [new( 101, a ), new( 202, new PhysicalBodyState() )], .01f ) );
	}

	[Fact]
	public void PreparedWorldIsRequired()
	{
		var world = World( new PhysicalBodyState(), new PhysicalBodyState() );
		Assert.Throws<InvalidOperationException>( () => world.CompileGeneratedRecord( Record(), [], .01f ) );
	}

	[Fact]
	public void TruncatedRecordIsRejected()
	{
		Assert.Throws<ArgumentException>( () => GeneratedContactRecord.Decode( new uint[ 63 ], 0, 1 ) );
	}

	[Fact]
	public void NegativeResolvedIndexIsRejected()
	{
		Assert.Throws<ArgumentOutOfRangeException>( () => GeneratedContactRecord.Decode( Record(), -1, 0 ) );
		Assert.Throws<ArgumentOutOfRangeException>( () => GeneratedContactRecord.Decode( Record(), 0, -1 ) );
	}

	private static PhysicalSolverWorld World( PhysicalBodyState a, PhysicalBodyState b ) =>
		new( [a, b], new BodyDynamicsSettings[ 2 ], [] );

	private static uint[] Record()
	{
		var words = new uint[ 64 ];
		void Row( int start, params float[] values )
		{
			for ( var i = 0; i < values.Length; ++i )
				words[ start + i ] = BitConverter.SingleToUInt32Bits( values[ i ] );
		}
		words[ 3 ] = 101;
		words[ 7 ] = 202;
		Row( 8, 0, 0, 1, .2f );
		Row( 15, .7f );
		Row( 19, .4f );
		Row( 24, 2, 3, 4 );
		Row( 32, 1, .1f, .2f, 2 );
		Row( 40, 3, 2, .3f );
		Row( 48, 5, 6, 7 );
		Row( 56, 8, 9, 10 );
		words[ 43 ] = 12;
		return words;
	}
}
