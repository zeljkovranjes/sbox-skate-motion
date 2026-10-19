using System;
using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class VerticalAttachmentControllerTests
{
	[Fact]
	public void FreshHistoryUsesAnUnboundedPreviousHeight()
	{
		var controller = Create();
		Assert.Equal( float.MaxValue, controller.PreviousHeight );
		Assert.False( controller.Active );
		Assert.Equal( 0, controller.PreviousCategory );
		Assert.Equal( 0f, controller.Elapsed );
	}

	[Fact]
	public void InactiveUpdatePublishesHistoryWithoutTouchingPendingRequest()
	{
		var controller = Create();
		var attachment = new BoardAttachmentController();
		attachment.RequestHeight( 4f, 9f );
		controller.Update( Input() with { Category = 4, ProjectedSpeed = -3f }, attachment );
		Assert.Equal( 4, controller.PreviousCategory );
		Assert.Equal( -3f, controller.PreviousSpeed );
		Assert.False( controller.Active );
		Assert.Equal( 0f, controller.Elapsed );
		Assert.Equal( 4f, attachment.Frame.Translation.Y );
		Assert.Equal( 9f, attachment.HeightRemaining );
		Assert.True( attachment.HeightRequested );
	}

	[Theory]
	[InlineData( 6, 1, true, false, 0f, 0 )]
	[InlineData( 2, 1, false, false, 1f, 2 )]
	[InlineData( 2, 3, false, false, 0f, 1 )]
	[InlineData( 2, 1, true, true, 1f, 3 )]
	public void TransitionSelectsTheRequiredMode( int previous, int category, bool primary, bool secondary, float trigger, int mode )
	{
		var controller = Create();
		controller.PreviousCategory = previous;
		controller.Update( Input() with { Category = category, PrimaryActive = primary, SecondaryActive = secondary, Trigger = trigger }, new BoardAttachmentController() );
		Assert.Equal( mode, controller.Mode );
		Assert.True( controller.Active );
		Assert.Equal( 1f / 60f, controller.Elapsed );
	}

	[Fact]
	public void SuppressionCancelsBeforeIntegrationAndRetainsPendingAttachment()
	{
		var controller = Create();
		controller.Active = true;
		controller.Height = 3f;
		var attachment = new BoardAttachmentController();
		attachment.RequestHeight( 4f, 9f );
		controller.Update( Input() with { Suppress = true }, attachment );
		Assert.False( controller.Active );
		Assert.Equal( 3f, controller.Height );
		Assert.Equal( 0f, controller.Elapsed );
		Assert.Equal( 4f, attachment.Frame.Translation.Y );
		Assert.True( attachment.HeightRequested );
	}

	[Fact]
	public void SecondarySignalBypassesSuppression()
	{
		var controller = Create();
		controller.Active = true;
		controller.Mode = 3;
		var attachment = new BoardAttachmentController();
		controller.Update( Input() with { Suppress = true, SecondaryActive = true }, attachment );
		Assert.True( controller.Active );
		Assert.True( attachment.HeightRequested );
	}

	[Fact]
	public void InitialTrackingUsesPreviousProjectedSpeed()
	{
		var controller = Create();
		controller.PreviousCategory = 2;
		controller.PreviousSpeed = -2f;
		controller.Update( Input() with { Category = 3, ProjectedSpeed = -7f }, new BoardAttachmentController() );
		Assert.Equal( -2f, controller.Velocity );
		Assert.Equal( -7f, controller.PreviousSpeed );
	}

	[Fact]
	public void InitialPrimaryRejectsPositiveStartingVelocity()
	{
		var controller = Create();
		controller.PreviousCategory = 6;
		controller.Update( Input() with { PrimaryActive = true, ProjectedSpeed = 5f }, new BoardAttachmentController() );
		Assert.Equal( 0f, controller.Velocity );
		Assert.Equal( 1f, controller.Height );
	}

	[Fact]
	public void ForcedRequestOverridesTimeoutAndStopsController()
	{
		var controller = Create();
		controller.Active = true;
		controller.Mode = 2;
		controller.Elapsed = 2f;
		controller.Height = 1f;
		var attachment = new BoardAttachmentController { Frame = LimbRigidTransform.Identity with { Translation = new Vector3( 4f, 0f, 5f ) } };
		controller.Update( Input() with { Category = 4, ForceRequest = true }, attachment );
		Assert.False( controller.Active );
		Assert.True( attachment.HeightRequested );
		Assert.Equal( 6f, attachment.HeightRemaining );
		Assert.Equal( new Vector3( 4f, 1f, 5f ), attachment.Frame.Translation );
	}

	[Fact]
	public void ExpiredControllerDoesNotEraseAnEarlierRequest()
	{
		var controller = Create();
		controller.Active = true;
		controller.Mode = 2;
		controller.Elapsed = 2f;
		var attachment = new BoardAttachmentController();
		attachment.RequestHeight( 4f, 9f );
		controller.Update( Input(), attachment );
		Assert.False( controller.Active );
		Assert.Equal( 4f, attachment.Frame.Translation.Y );
		Assert.Equal( 9f, attachment.HeightRemaining );
		Assert.True( attachment.HeightRequested );
	}

	[Fact]
	public void TrackingBaselineMovesByAtMostTheAuthoredStep()
	{
		var controller = Create();
		controller.Active = true;
		controller.Mode = 1;
		controller.Elapsed = .3f;
		controller.Baseline = .5f;
		controller.Update( Input() with { Category = 3, CurrentHeight = 5f }, new BoardAttachmentController() );
		Assert.Equal( .6f, controller.Baseline );
	}

	[Fact]
	public void ImpulseUsesSpeedMagnitudeAndSeparateReleaseCount()
	{
		var controller = Create();
		controller.Active = true;
		controller.Mode = 3;
		controller.Velocity = -20f;
		var attachment = new BoardAttachmentController();
		controller.Update( Input() with { SecondaryActive = true }, attachment );
		Assert.Equal( 1.2f, controller.Height );
		Assert.Equal( -20f, controller.Velocity );
		Assert.Equal( 7f, attachment.HeightRemaining );
	}

	[Fact]
	public void CurveOwnsItsPointsAndClampsOutsideRange()
	{
		var x = new[] { 0f, 1f, 2f, 3f };
		var y = new[] { 0f, 2f, 4f, 6f };
		var curve = new VerticalAttachmentCurve( x, y );
		x[ 0 ] = 10f;
		y[ 0 ] = 99f;
		Assert.Equal( 0f, curve.Evaluate( -1f ) );
		Assert.Equal( 1f, curve.Evaluate( .5f ) );
		Assert.Equal( 6f, curve.Evaluate( 5f ) );
	}

	[Fact]
	public void CurveRejectsUnorderedAndNonfiniteData()
	{
		var y = new[] { 0f, 1f, 2f, 3f };
		Assert.Throws<ArgumentException>( () => new VerticalAttachmentCurve( new[] { 0f, 2f, 1f, 3f }, y ) );
		Assert.Throws<ArgumentException>( () => new VerticalAttachmentCurve( new[] { 0f, 1f, 2f, float.NaN }, y ) );
	}

	private static VerticalAttachmentController Create()
	{
		var curve = new VerticalAttachmentCurve( new[] { 0f, 1f, 2f, 3f }, new[] { 1f, 1f, 1f, 1f } );
		return new VerticalAttachmentController( new VerticalAttachmentSettings(
			0f, 10f, 0f, 0f, .1f, 0f, 0f, .2f, 0f, 0f, .1f, 1f, .5f, 2f, 7f, .2f ), curve, curve, curve );
	}

	private static VerticalAttachmentInput Input() => new( 1, 0f, false, false, false, false, false, 2f, 0f, 1f );
}
