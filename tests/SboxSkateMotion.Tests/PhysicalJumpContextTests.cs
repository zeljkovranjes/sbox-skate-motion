using System;
using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class PhysicalJumpContextTests
{
	[Fact]
	public void MissingSurfaceRetainsItsVectorsWhileUpdatingTheMode()
	{
		var retained = Input();
		var result = PhysicalJumpContextController.Prepare( new(), new(), retained,
			new( false, true, Vector3.Zero, Vector3.Zero, Vector3.Zero, Vector3.Zero, Vector3.Zero ), 4 );
		Assert.Equal( retained with { Mode = 4 }, result );
	}

	[Fact]
	public void AvailableSurfaceRefreshesOnlyTheRetainedQueryFields()
	{
		var retained = Input();
		var surface = new PhysicalJumpSurfaceInput( true, true, Vector3.UnitY,
			Vector3.UnitX, Vector3.UnitZ, -Vector3.UnitX, -Vector3.UnitY );
		var result = PhysicalJumpContextController.Prepare( new(), new(), retained, surface, -1 );
		Assert.Equal( retained.Velocity, result.Velocity );
		Assert.Equal( retained.Position, result.Position );
		Assert.Equal( retained.ReferencePosition, result.ReferencePosition );
		Assert.Equal( retained.Mode, result.Mode );
		Assert.True( result.CorrectionEnabled );
		Assert.Equal( surface.FallbackDirection, result.FallbackDirection );
		Assert.Equal( surface.Correction, result.Correction );
		Assert.Equal( surface.PrimaryDirection, result.PrimaryDirection );
		Assert.Equal( surface.SecondaryDirection, result.SecondaryDirection );
		Assert.Equal( surface.Anchor, result.Anchor );
	}

	[Theory]
	[InlineData( -2, 0, 0UL )]
	[InlineData( 0, 0, 0UL )]
	[InlineData( 1, 0, 0UL )]
	[InlineData( 2, 1, 0x2000000UL )]
	public void HoldCountdownReassertsTheFlagOnlyWhilePositive( int before, int after, ulong flags )
	{
		var state = new PhysicalJumpVelocityState { HoldFrames = before, Triggered = true };
		var attributes = new PhysicalAnimationAttributeState();
		PhysicalJumpContextController.Prepare( attributes, state, Input(), default, -1 );
		Assert.Equal( after, state.HoldFrames );
		Assert.Equal( flags, attributes.ActionFlags );
		Assert.False( state.Triggered );
	}

	[Fact]
	public void ExpiredHoldDoesNotClearAnAlreadyPublishedFlag()
	{
		var attributes = new PhysicalAnimationAttributeState { ActionFlags = 0x2000001 };
		PhysicalJumpContextController.Prepare( attributes, new() { HoldFrames = 1 }, Input(), default, -1 );
		Assert.Equal( 0x2000001UL, attributes.ActionFlags );
	}

	[Fact]
	public void PreparationAdvancesByTheFixedStepAndCapsAtOne()
	{
		var state = new PhysicalJumpVelocityState();
		PhysicalJumpContextController.Prepare( new(), state, Input(), default, -1 );
		Assert.Equal( 0x3b656042u, BitConverter.SingleToUInt32Bits( state.Cooldown ) );
		state.Cooldown = .999f;
		PhysicalJumpContextController.Prepare( new(), state, Input(), default, -1 );
		Assert.Equal( 1f, state.Cooldown );
	}

	private static PhysicalJumpVelocityInput Input() => new( new( 1f, 2f, 3f ),
		new( 4f, 5f, 6f ), new( 7f, 8f, 9f ), Vector3.UnitX, Vector3.UnitY,
		Vector3.UnitZ, -Vector3.UnitX, -Vector3.UnitY, 2, false );
}
