using System;
using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class PhysicalLocomotionContactCadenceTests
{
	[Fact]
	public void ProcessedAnimationAttributesSupplyMotionAndRequestedPhase()
	{
		var attributes = new PhysicalAnimationAttributeState();
		var attributeInput = new PhysicalAnimationAttributeInput
		{
			BoneNames = Array.Empty<AnimationAttributeKey>(),
			Attributes = new[]
			{
				new PhysicalAnimationAttribute( AnimationAttributeKey.Encode( "ANIMTRANSZ" ), 2f ),
				new PhysicalAnimationAttribute( AnimationAttributeKey.Encode( "CADENCEENDPERCENT" ), -1f ),
				new PhysicalAnimationAttribute( AnimationAttributeKey.Encode( "ANIMTIME" ), 0f )
			}
		};
		PhysicalAnimationAttributeProcessor.Update( attributes, attributeInput );
		var input = new PhysicalLocomotionCadenceInput( new( 0f, 0f, 2f ), Vector3.Zero,
			Vector3.UnitY, Vector3.Zero, false, Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ,
			Vector3.Zero, 0f, 99f, false, 0u );
		var state = PhysicalLocomotionCadenceState.Initial with { Phase = .2f, Duration = .6f };
		Assert.True( PhysicalLocomotionCadence.TryUpdateFromAttributes( ref state, input,
			new( .01f, 2f, 5f, 1e10f ), attributes, null ) );
		Assert.Equal( 0x3e5ddddeu, BitConverter.SingleToUInt32Bits( state.Phase ) );
		Assert.Equal( 1f, state.Rate );
		Assert.Equal( -1f, state.Target );
		Assert.Equal( .6f, state.Duration );
	}

	[Theory]
	[InlineData( 0x80u, 0x3e5b05b0u, 0x3f555555u )]
	[InlineData( 0x100u, 0x3e5097b4u, 0x3e638e38u )]
	public void ContactObservationSelectsTheRetainedPhaseCorrection( uint flags, uint phase, uint rate )
	{
		var state = PhysicalLocomotionCadenceState.Initial with { Phase = .2f, Duration = .6f };
		var input = new PhysicalLocomotionCadenceInput( new( 0f, 0f, 2f ), Vector3.Zero,
			Vector3.UnitY, Vector3.Zero, false, Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ,
			new( 0f, 0f, 2f ), -1f, 0f, false, flags );
		var contact = new PhysicalLocomotionCadenceContactInput( Vector3.Zero, new( 0f, 0f, 2f ) );
		Assert.True( PhysicalLocomotionCadence.TryUpdateWithContacts( ref state, input,
			new( .01f, 2f, 5f, 1e10f ), contact ) );
		Assert.Equal( phase, BitConverter.SingleToUInt32Bits( state.Phase ) );
		Assert.Equal( rate, BitConverter.SingleToUInt32Bits( state.Rate ) );
		Assert.Equal( -1f, state.Target );
		Assert.Equal( .6f, state.Duration );
		Assert.Equal( 1, state.LocomotionIndex );
		Assert.False( state.ForwardTarget );
	}
}
