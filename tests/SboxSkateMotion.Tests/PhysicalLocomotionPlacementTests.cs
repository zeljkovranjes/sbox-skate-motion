using System;
using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class PhysicalLocomotionPlacementTests
{
	[Theory]
	[InlineData( 0 )]
	[InlineData( 1 )]
	[InlineData( 2 )]
	public void PlacementRetainsPhysicalFeedbackAndBindsCompletedOutputs( int variant )
	{
		PhysicalLocomotionPlacementState previous;
		Vector3 desired;
		uint flags;
		bool active;
		uint[] expected;
		switch ( variant )
		{
		case 0:
			previous = new( new Vector3( -1f, 4f, 2f ), new Vector3( 1.29999995f, -0.200000003f, 2f ) );
			desired = new Vector3( 2f, -2f, 3f );
			flags = 0u;
			active = false;
			expected = new uint[] { 0x4059bfacu, 0x400d3f0du, 0x405dea8eu };
			break;
		case 1:
			previous = new( new Vector3( 2f, -1f, 3f ), new Vector3( 1.29999995f, -0.200000003f, 2f ) );
			desired = new Vector3( -3f, 4f, 1f );
			flags = 1u;
			active = true;
			expected = new uint[] { 0xc0765408u, 0x3fba07b4u, 0x3f378ff4u };
			break;
		case 2:
			previous = new( new Vector3( 2f, 0f, -1f ), new Vector3( 1.29999995f, -0.200000003f, 2f ) );
			desired = new Vector3( 4f, 3f, 2f );
			flags = 3u;
			active = false;
			expected = new uint[] { 0x403b7d71u, 0xbe587a50u, 0x3fd253a2u };
			break;
		default:
			throw new ArgumentOutOfRangeException( nameof( variant ) );
		}
		var secondary = Matrix4x4.Identity;
		secondary.Translation = new Vector3( 1f, 1.89999998f, 3.00166607f );
		var owner = new PhysicalLocomotionOwnerState
		{
			SecondaryFrame = secondary,
			SecondaryCorrectionActive = active,
			Solved = new( Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, new Vector3( 1f, 1.98333323f, 3.00166583f ), Vector3.Zero )
		};
		var input = new PhysicalLocomotionOwnerInput( new Vector3( 1f, 0f, 0f ), default, 0, flags, false, false, false, default );
		var physical = new PhysicalLocomotionMotionInput( default, default, Vector3.UnitY, new Vector3( 0.300000012f, 0.899999976f, 0.100000001f ) );
		var result = PhysicalLocomotionPlacement.Update( previous, owner, input, physical,
			new( desired, new Vector3( 1f, 2f, 3f ) ) );
		Assert.Equal( expected[ 0 ], BitConverter.SingleToUInt32Bits( result.Reference.X ) );
		Assert.Equal( expected[ 1 ], BitConverter.SingleToUInt32Bits( result.Reference.Y ) );
		Assert.Equal( expected[ 2 ], BitConverter.SingleToUInt32Bits( result.Reference.Z ) );
		Assert.Equal( previous.SecondaryTarget, result.SecondaryTarget );
		var supplied = new PhysicalOffboardCharacterInput( default, new( 999f ), 2, 8, 16, .3f, default );
		var bound = PhysicalLocomotionPlacement.ApplyToCharacterInput( owner, result, supplied );
		Assert.Equal( owner.Solved, bound.Solved );
		Assert.Equal( result.Reference, bound.FeedbackReference );
		Assert.Equal( supplied.ActionFlags, bound.ActionFlags );
		Assert.Equal( supplied.RigFlags, bound.RigFlags );
		Assert.Equal( supplied.MotionFlags, bound.MotionFlags );
		Assert.Equal( supplied.Control, bound.Control );
	}

	[Fact]
	public void RejectedOwnerLeavesPlacementAndSolvedStateUnchanged()
	{
		var solved = new PhysicalLocomotionMotionState( Vector3.UnitX, Vector3.UnitY,
			Vector3.UnitZ, new( 1f, 2f, 3f ), new( 4f, 5f, 6f ) );
		var owner = new PhysicalLocomotionOwnerState { Solved = solved };
		var placement = new PhysicalLocomotionPlacementState( new( 4f, 3f, 2f ), new( 1f, 0f, -1f ) );
		var previous = placement;
		var input = new PhysicalLocomotionOwnerInput( default, default, 0, 2, false, false, false, default );
		Assert.False( PhysicalLocomotionOwner.TryPrepareSolveAndPlace( owner, input, default, ref placement, default ) );
		Assert.Equal( previous, placement );
		Assert.Equal( solved, owner.Solved );
	}
}
