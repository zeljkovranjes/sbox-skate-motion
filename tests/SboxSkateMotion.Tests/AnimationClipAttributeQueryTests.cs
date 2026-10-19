using System;
using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class AnimationClipAttributeQueryTests
{
	private static readonly AnimationAttributeKey Name = new( 1, 2, 3, 4, 5 );
	private static readonly AnimationAttributePayload Sentinel = new( 10, 20, 30, 40, 50, 60 );
	private static readonly AnimationAttributePayload Values = new( 100, 200, 300, 400, 500, 600 );

	[Theory]
	[InlineData( 0 )]
	[InlineData( 1 )]
	[InlineData( 3 )]
	[InlineData( -1 )]
	public void TypedLookupPreservesValueLanesItDoesNotWrite( sbyte kind )
	{
		var leaf = Leaf();
		var result = Result();
		Assert.True( leaf.TryGetAttribute( new[] { Attribute( kind ) }, Name, 15, ref result ) );
		var expected = kind switch
		{
			0 => Sentinel with { A = Values.A },
			1 => Sentinel with { A = Values.A, B = Values.B, C = Values.C, D = Values.D },
			3 => Values,
			_ => Sentinel
		};
		Assert.Equal( expected, result.Payload );
		Assert.Equal( Name, result.Name );
		Assert.Equal( kind, result.Kind );
		Assert.Equal( 6, result.Flags );
		Assert.Equal( -1f, result.Start );
		Assert.Equal( -1f, result.End );
	}

	[Fact]
	public void FullKeyMismatchLeavesTheEntireResultUntouched()
	{
		var leaf = Leaf();
		var result = Result();
		var expected = result;
		Assert.False( leaf.TryGetAttribute( new[] { Attribute( 0 ) }, Name with { E = 6 }, 31, ref result ) );
		Assert.Equal( expected, result );
	}

	[Fact]
	public void FilteredDuplicateDoesNotHideALaterMatchingRecord()
	{
		var leaf = Leaf();
		leaf.Seek( .25f );
		var attributes = new[]
		{
			new AnimationClipAttribute( Name, .8f, 1f, 0, Values ),
			new AnimationClipAttribute( Name, -1f, -1f, 0, Values with { A = 999 } ),
			new AnimationClipAttribute( Name, -1f, -1f, 0, Values with { A = 777 } )
		};
		var result = Result();
		Assert.True( leaf.TryGetAttribute( attributes, Name, 15, ref result ) );
		Assert.Equal( 999u, result.Payload.A );
	}

	[Fact]
	public void FilterRequiresBothStatusAndValueCategoryBits()
	{
		var leaf = Leaf();
		var attributes = new[] { Attribute( 0 ) };
		var result = Result();
		Assert.False( leaf.TryGetAttribute( attributes, Name, 2, ref result ) );
		Assert.False( leaf.TryGetAttribute( attributes, Name, 4, ref result ) );
		Assert.True( leaf.TryGetAttribute( attributes, Name, 6, ref result ) );
	}

	[Fact]
	public void WrapHistoryChangesIntervalStatusWithoutBeingConsumed()
	{
		var leaf = Leaf();
		leaf.Seek( .25f );
		var completion = AnimationPlaybackCompletion.Empty;
		leaf.Advance( .85f, 0f, ref completion );
		var attributes = new[] { new AnimationClipAttribute( Name, .2f, .5f, 0, Values ) };
		var result = Result();
		Assert.True( leaf.TryGetAttribute( attributes, Name, 9, ref result ) );
		Assert.Equal( 9, result.Flags );
		Assert.Equal( 1u, leaf.Clock.WrapCount );
		Assert.Equal( .25f, leaf.Clock.PreviousPosition );
		Assert.False( leaf.TryGetAttribute( attributes, Name, 5, ref result ) );
	}

	[Fact]
	public void CurveUsesPlaybackRateAndFrameRateWithoutApplyingAssetScaleAgain()
	{
		var leaf = Leaf( 2f );
		leaf.SetRate( 2f );
		leaf.Seek( .5f );
		var curve = new AnimationScalarCurve( new[] { new Vector2( 0f, 0f ), new Vector2( 30f, 7f ), new Vector2( 60f, 0f ) } );
		var result = Result();
		Assert.True( leaf.TryGetAttribute( new[] { new AnimationClipAttribute( Name, -1f, -1f, 2, Values, curve ) }, Name, 15, ref result ) );
		Assert.Equal( 7f, result.Payload.Scalar );
		Assert.Equal( Sentinel.B, result.Payload.B );
	}

	[Fact]
	public void CurveOwnsKeysAndHandlesEndpointsInteriorKeysAndSegments()
	{
		var keys = new[] { new Vector2( 0f, -3f ), new Vector2( 10f, 4f ), new Vector2( 20f, 1f ) };
		var curve = new AnimationScalarCurve( keys );
		keys[ 1 ] = Vector2.Zero;
		Assert.Equal( -3f, curve.Sample( -1f ) );
		Assert.Equal( 4f, curve.Sample( 10f ) );
		Assert.Equal( 2.5f, curve.Sample( 15f ) );
		Assert.Equal( 1f, curve.Sample( 21f ) );
		Assert.Throws<ArgumentException>( () => new AnimationScalarCurve( Array.Empty<Vector2>() ) );
	}

	private static AnimationClipAttribute Attribute( sbyte kind ) => new( Name, -1f, .6f, kind, Values );
	private static AnimationAttributeQueryResult Result() => new( Sentinel, default, 10f, 20f, 128, -5 );

	private static AnimationClipPlayback Leaf( float scale = 1f )
	{
		var clip = new RawAnimationClip( "sample", 30f, scale, 31, 0, 1,
			new int[ 3 ], new int[ 3 ], Array.Empty<Vector4>() );
		return new AnimationClipPlayback( clip, 1, new AnimationPoseSamplingContext( 0, false ) );
	}
}
