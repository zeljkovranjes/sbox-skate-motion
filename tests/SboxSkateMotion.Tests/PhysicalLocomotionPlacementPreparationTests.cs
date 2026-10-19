using System;
using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class PhysicalLocomotionPlacementPreparationTests
{
	[Theory]
	[InlineData( false, 0x412406d4u, 0xc06d8bf2u, 0x4002fc96u )]
	[InlineData( true, 0x412547aeu, 0xc0706d3au, 0x4009999au )]
	public void PlacementPredictionSelectsPhysicalVelocityAndPreservesSecondaryPoint(
		bool useOverride, uint x, uint y, uint z )
	{
		var secondary = new Vector3( 3.25f, 4.5f, -5.75f );
		var result = PhysicalLocomotionPlacementPreparation.Prepare( new( 10.25f, -3.75f, 2.125f ),
			new( .1f, 2.3f, -4.7f ), useOverride, new( 4.8f, -.4f, 1.5f ), secondary );
		Assert.Equal( x, BitConverter.SingleToUInt32Bits( result.DesiredPosition.X ) );
		Assert.Equal( y, BitConverter.SingleToUInt32Bits( result.DesiredPosition.Y ) );
		Assert.Equal( z, BitConverter.SingleToUInt32Bits( result.DesiredPosition.Z ) );
		Assert.Equal( secondary, result.SecondaryPosition );
	}

	[Fact]
	public void CorrectionResetPreservesPlacementReferenceAndOtherOwnerState()
	{
		var owner = new PhysicalLocomotionOwnerState
		{
			SecondaryCorrection = new( .1f, .2f, .3f ), SecondaryCorrectionActive = true,
			Feedback = new( 2f, 3f, 4f ), ReferenceActive = true
		};
		var placement = new PhysicalLocomotionPlacementState( new( 9f, 8f, 7f ), new( 3f, 4f, 5f ) );
		PhysicalLocomotionPlacementPreparation.ResetCorrection( owner, ref placement );
		Assert.Equal( Vector3.Zero, owner.SecondaryCorrection );
		Assert.False( owner.SecondaryCorrectionActive );
		Assert.Equal( Vector3.Zero, placement.SecondaryTarget );
		Assert.Equal( new Vector3( 9f, 8f, 7f ), placement.Reference );
		Assert.Equal( new Vector3( 2f, 3f, 4f ), owner.Feedback );
		Assert.True( owner.ReferenceActive );
	}
}
