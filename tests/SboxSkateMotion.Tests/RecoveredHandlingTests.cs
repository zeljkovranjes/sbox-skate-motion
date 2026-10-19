using System;
using System.Numerics;
using SkateMotion.Core;
using SkateMotion.Core.Vehicle;
using Xunit;

namespace SboxSkateMotion.Tests;

public class RecoveredHandlingTests
{
	[Fact]
	public void CurvesInterpolateAndHoldTheirEnds()
	{
		var curve = new LinearCurve( new[] { 0f, 10f }, new[] { 1f, 3f } );
		Assert.Equal( 1f, curve.Evaluate( -5f ) );
		Assert.Equal( 2f, curve.Evaluate( 5f ), 4 );
		Assert.Equal( 3f, curve.Evaluate( 50f ) );
		Assert.Equal( 1f, curve.Evaluate( float.NaN ) );
		Assert.Throws<ArgumentException>( () => new LinearCurve( new[] { 1f, 1f }, new[] { 0f, 0f } ) );
	}

	[Fact]
	public void PopLaunchIsNearlyTheSameForAnyWindupAndWeakerOnWalls()
	{
		var tap = HandlingCurves.PopSpeed( HandlingCurves.PopStrength( 0.03f ), 0f, 3f );
		var full = HandlingCurves.PopSpeed( HandlingCurves.PopStrength( 0.5f ), 0f, 3f );
		Assert.InRange( tap, 3.29f, 3.31f );
		Assert.InRange( full, 3.45f, 3.47f );
		// Steep ground and very high speed take height off the pop.
		Assert.True( HandlingCurves.PopSpeed( 1f, 64f, 3f ) < full * 0.7f );
		Assert.True( HandlingCurves.PopSpeed( 1f, 0f, 20f ) < full );
		Assert.True( HandlingCurves.PopSpeed( 1f, 90f, 3f ) > 0f );
	}

	[Fact]
	public void LandingsGetLessForgivingWithSpeed()
	{
		Assert.Equal( 1.6f, HandlingCurves.MaxLandingAngle( 5f ) );
		Assert.True( HandlingCurves.MaxLandingAngle( 15f ) < 1.1f );
		Assert.Equal( 0.263f, HandlingCurves.MaxLandingAngle( 40f ) );
	}

	[Fact]
	public void RollingFrictionIsNilAtRidingSpeedsAndRisesWhenVeryFast()
	{
		Assert.Equal( 0f, HandlingCurves.RollingDeceleration( 5f ) );
		Assert.True( HandlingCurves.RollingDeceleration( 20f ) > 1f );
	}

	[Fact]
	public void PushesAddSpeedOnlyWhileTheFootIsDownAndStopAtTheLimit()
	{
		var settings = SkateSettings.RecoveredDefaults;
		var movement = new SkateMovement();
		var speed = 0f;
		var step = 1f / 240f;
		// Foot down for 0.25 s: the first push lifts the board to its target at a limited rate.
		for ( var t = 0f; t < 0.25f; t += step )
			speed += movement.RecoveredAcceleration( new ControlInput( Push: 1f, PushContact: true ), speed, settings, step ) * step;
		Assert.InRange( speed, 4.4f, 4.5f );
		// Foot in the air adds nothing.
		Assert.Equal( 0f, movement.RecoveredAcceleration( new ControlInput( Push: 1f, PushContact: false ), speed, settings, step ) );
		// Many pushes never pass the push limit.
		for ( var push = 0; push < 20; push++ )
		{
			movement.RecoveredAcceleration( new ControlInput( Push: 1f, PushContact: false ), speed, settings, step );
			for ( var t = 0f; t < 0.25f; t += step )
				speed += movement.RecoveredAcceleration( new ControlInput( Push: 1f, PushContact: true ), speed, settings, step ) * step;
		}
		Assert.InRange( speed, 8.4f, HandlingCurves.PushLimit + 0.001f );
	}

	[Fact]
	public void WithoutAnAnimationPushesFollowAStroke()
	{
		var settings = SkateSettings.RecoveredDefaults;
		var movement = new SkateMovement();
		var pushing = 0;
		var step = 1f / 240f;
		for ( var t = 0f; t < settings.PushPeriod * 2f; t += step )
		{
			if ( movement.RecoveredAcceleration( new ControlInput( Push: 1f ), 2f, settings, step ) > 0f )
				pushing++;
		}
		// Two strokes, each in contact for its fallback time (until the target is reached).
		Assert.InRange( pushing * step, 0.05f, 2f * SkateMovement.FallbackContact + 0.01f );
	}

	[Fact]
	public void SteeringEasesDownWhilePushingAndInAManual()
	{
		var settings = SkateSettings.RecoveredDefaults;
		var free = new SteeringResponse();
		var pushing = new SteeringResponse();
		var manual = new SteeringResponse();
		float a = 0f, b = 0f, c = 0f;
		for ( var i = 0; i < 120; i++ )
		{
			a = free.Angle( settings, 1f, 1.5f, false, false, 1f / 60f );
			b = pushing.Angle( settings, 1f, 1.5f, true, false, 1f / 60f );
			c = manual.Angle( settings, 1f, 1.5f, false, true, 1f / 60f );
		}
		Assert.InRange( a, 0.27f, 0.281f );
		Assert.InRange( b / a, 0.49f, 0.51f );
		Assert.InRange( c / a, 0.59f, 0.61f );
		// Faster means less truck angle, and the wheels' grip limits it further.
		var fast = new SteeringResponse();
		for ( var i = 0; i < 120; i++ )
			b = fast.Angle( settings, 1f, 12f, false, false, 1f / 60f );
		Assert.True( b < a * 0.6f );
	}

	[Fact]
	public void AirSpinFollowsTheStickAndLinesUpWithTravel()
	{
		var air = new AirControl();
		// Stick held: the spin rate climbs toward the stick's rate about world up.
		var body = new BoardBody( Vector3.Zero, Quaternion.Identity, new Vector3( 3f, 0f, 3f ), Vector3.Zero, Vector3.One );
		var change = air.Step( body, 1f, 9.8f, 0.01f );
		Assert.True( change.Z > 0f );
		Assert.Equal( 0f, change.X );
		// No stick, deck turned 30 degrees from its travel: it turns back toward it.
		var turned = body with { Rotation = Quaternion.CreateFromAxisAngle( Vector3.UnitZ, 0.5f ) };
		air.Land();
		Assert.True( air.Step( turned, 0f, 9.8f, 0.01f ).Z < 0f );
		// Riding backwards lines up the other way round, not with a half turn.
		var backwards = body with { Rotation = Quaternion.CreateFromAxisAngle( Vector3.UnitZ, MathF.PI - 0.2f ) };
		air.Land();
		Assert.True( air.Step( backwards, 0f, 9.8f, 0.01f ).Z > 0f );
		Assert.Equal( 1f, AirControl.InputShare( 0f ) );
		Assert.InRange( AirControl.InputShare( 1f ), 0.41f, 0.43f );
	}

	[Fact]
	public void GrindFrictionBuildsUpOverFourSeconds()
	{
		Assert.InRange( HandlingCurves.GrindFrictionShare( 0f ), 0.63f, 0.64f );
		Assert.InRange( HandlingCurves.GrindFrictionShare( 2f ), 0.81f, 0.82f );
		Assert.Equal( 1f, HandlingCurves.GrindFrictionShare( 10f ) );
	}
}
