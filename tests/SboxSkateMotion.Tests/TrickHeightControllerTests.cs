using SkateMotion.Core.Animation;
using SkateMotion.Core.Input;
using System;
using Xunit;

namespace SboxSkateMotion.Tests;

public class TrickHeightControllerTests
{
	private static readonly AnimationAttributeKey Source = AnimationAttributeKey.Encode( "Strength" );
	private static readonly InputCommandKey Speed = InputCommandKey.Encode( "GESTURESPEED"u8 );
	private static readonly InputCommandKey Height = InputCommandKey.Encode( "TRICKHEIGHT"u8 );

	private static InputRequestBuffer Requests( float speed, float? height = null )
	{
		var requests = new InputRequestBuffer( 2 );
		requests.Add( Speed, speed );
		if ( height.HasValue )
			requests.Add( Height, height.Value );
		return requests;
	}

	private static AnimationAttributeQueryResult Attribute( float value, sbyte kind = 0 )
		=> new( new( BitConverter.SingleToUInt32Bits( value ), 0, 0, 0, 0, 0 ), Source, -1f, -1f, 6, kind );

	[Fact]
	public void FixedHeightPrecedesRequestedHeightAndGestureClamping()
	{
		var result = TrickHeightController.Evaluate( Requests( 0.1f, 0.25f ), [ Attribute( 0.5f ) ],
			new( Source, 0.75f, true, true, false, false ) );
		Assert.Equal( 0.75f, result.Height );
		Assert.Equal( 0.1f * 0.2f, result.JumpHeightOverride );
	}

	[Fact]
	public void PresentZeroHeightBypassesAttributeAndGestureSelection()
	{
		var result = TrickHeightController.Evaluate( Requests( 0.75f, 0f ), [ Attribute( 0.5f ) ],
			new( Source, null, true, false, false, false ) );
		Assert.Equal( 0f, result.Height );
	}

	[Theory]
	[InlineData( false, false, 0.5f )]
	[InlineData( false, true, 0.5f )]
	[InlineData( true, false, 0.75f )]
	[InlineData( true, true, 0.5f )]
	public void AttributeFallbackDistinguishesGestureAndClampedGestureModes( bool enabled, bool clamp, float expected )
	{
		var result = TrickHeightController.Evaluate( Requests( 0.75f ), [ Attribute( 0.5f ) ],
			new( Source, null, enabled, clamp, false, false ) );
		Assert.Equal( expected, result.Height );
	}

	[Fact]
	public void NegativeAttributeCanRemainBelowTheNonnegativeGestureLimit()
	{
		var result = TrickHeightController.Evaluate( Requests( -1f ), [ Attribute( -0.5f ) ],
			new( Source, null, true, true, false, false ) );
		Assert.Equal( -0.5f, result.Height );
	}

	[Fact]
	public void UnorderedGestureComparisonPreservesTheAttribute()
	{
		var result = TrickHeightController.Evaluate( Requests( float.NaN ), [ Attribute( 0.5f ) ],
			new( Source, null, true, true, false, false ) );
		Assert.Equal( 0.5f, result.Height );
		Assert.True( float.IsNaN( result.JumpHeightOverride ) );
	}

	[Theory]
	[InlineData( false, false, 0.2f )]
	[InlineData( false, true, 1f )]
	[InlineData( true, false, 0.27f )]
	[InlineData( true, true, 0.27f )]
	public void JumpOverrideUsesGestureSpeedWithManualPriority( bool manual, bool grind, float scale )
	{
		var result = TrickHeightController.Evaluate( Requests( 0.75f ), [],
			new( Source, 0.9f, false, false, manual, grind ) );
		Assert.Equal( 0.75f * scale, result.JumpHeightOverride );
	}

	[Fact]
	public void AttributeProviderUsesTheFirstMatchAndLeavesUnknownPayloadKindsAtZero()
	{
		var result = TrickHeightController.Evaluate( Requests( 0.75f ), [ Attribute( 0.5f, 4 ), Attribute( 0.9f ) ],
			new( Source, null, false, false, false, false ) );
		Assert.Equal( 0f, result.Height );
	}

	[Fact]
	public void MissingInputsYieldZeroWithoutInventingAnticipation()
	{
		var result = TrickHeightController.Evaluate( new( 0 ), [], new( Source, null, false, false, false, false ) );
		Assert.Equal( new TrickHeightResult( 0f, 0f ), result );
	}
}
