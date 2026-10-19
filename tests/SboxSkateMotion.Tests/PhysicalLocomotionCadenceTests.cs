using System;
using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class PhysicalLocomotionCadenceTests
{
	[Theory]
	[InlineData( false, 0x3e5ddddeu, 0x3f800000u, 0xbf800000u, 0x3f19999au, 1 )]
	[InlineData( true, 0x3ea22222u, 0x3f7fffffu, 0x3f000000u, 0x3e4cccccu, 0 )]
	public void MovingAndSettlingPreserveRetainedPhysicalPhaseWords( bool stopped,
		uint phase, uint rate, uint target, uint duration, int index )
	{
		var state = PhysicalLocomotionCadenceState.Initial with { Phase = stopped ? .3f : .2f, Duration = .6f };
		var input = Input( stopped ? 0f : 2f );
		Assert.True( PhysicalLocomotionCadence.TryUpdate( ref state, input, new( .01f, 2f, 5f, 1e10f ) ) );
		Assert.Equal( phase, BitConverter.SingleToUInt32Bits( state.Phase ) );
		Assert.Equal( rate, BitConverter.SingleToUInt32Bits( state.Rate ) );
		Assert.Equal( target, BitConverter.SingleToUInt32Bits( state.Target ) );
		Assert.Equal( duration, BitConverter.SingleToUInt32Bits( state.Duration.Value ) );
		Assert.Equal( index, state.LocomotionIndex );
		Assert.False( state.ForwardTarget );
	}

	[Fact]
	public void PendingContactCorrectionRejectsWithoutCommittingAnIntermediateState()
	{
		var state = PhysicalLocomotionCadenceState.Initial with { Phase = .2f, Duration = .6f };
		var before = state;
		var input = Input( 2f ) with { ContactFlags = 0x100u };
		Assert.False( PhysicalLocomotionCadence.TryUpdate( ref state, input, new( .01f, 2f, 5f, 1e10f ) ) );
		Assert.Equal( before, state );
	}

	private static PhysicalLocomotionCadenceInput Input( float speed ) => new(
		new( 0f, 0f, speed ), Vector3.Zero, Vector3.UnitY, Vector3.Zero, false,
		Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, new( 0f, 0f, 2f ), -1f, 0f, false, 0u );
}
