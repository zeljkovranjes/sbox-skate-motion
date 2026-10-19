using System;
using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class PhysicalLocomotionOwnerTests
{
	[Theory]
	[InlineData( 0 )]
	[InlineData( 1 )]
	[InlineData( 2 )]
	public void OrdinaryOwnerPreparesContactFrameMotionAndGaitWithoutExternalFrameResults( int variant )
	{
		var state = CreateState( variant );
		var target = Matrix4x4.Identity;
		target.Translation = new( 2f, 3f, 4f );
		var contact = new Matrix4x4( 0f, 0f, -1f, 0f, 0f, 1f, 0f, 0f,
			1f, 0f, 0f, 0f, .11f, .2f, .32f, 0f );
		var input = new PhysicalLocomotionOwnerInput( Vector3.Zero, contact, variant == 0 ? 7u : 8u,
			1u, false, variant == 2, variant == 2, target );
		Assert.True( PhysicalLocomotionOwner.TryPrepareAndSolve( state, input,
			new( Vector3.Zero, Vector3.Zero, Vector3.UnitY, Vector3.UnitY ) ) );
		var attributes = new PhysicalAnimationAttributeState
		{
			AnimationTranslation = new( 0f, 0f, 2f, 0f ), CadenceEndPercent = -1f
		};
		var cadence = PhysicalLocomotionCadenceState.Initial with { Phase = .2f, Duration = .6f };
		var physical = new PhysicalLocomotionCadenceInput( Vector3.Zero, new( 999f ), Vector3.Zero,
			Vector3.Zero, false, Vector3.Zero, Vector3.Zero, Vector3.Zero, Vector3.Zero, 0f, 99f, false, 1u );
		Assert.True( PhysicalLocomotionOwner.TryUpdateCadence( state, ref cadence, physical,
			new( .01f, 2f, 5f, 1e10f ), attributes, null ) );
		var expected = variant switch
		{
			0 => new uint[]
			{
				0xbf7ffffbu, 0xba350a21u, 0x3a350a21u, 0xbf7ffffbu, 0x3f800000u,
				0x3ff33333u, 0x40401b4cu, 0x3f800000u, 0x3ff33333u, 0x40401b4cu,
				0x3c936ef8u, 0x00000000u, 0xbd1251c8u, 0x42bc7442u, 0x42bc7442u,
				0x433c7442u, 0x3f800000u, 0xc358cccdu, 0x3f000000u, 0x43d72666u,
				0x00000000u, 0xbf800000u, 0x3dccba00u, 0x3f497e28u, 0x43573993u,
			},
			1 => new uint[]
			{
				0x3f7fffffu, 0x00000000u, 0x00000000u, 0x3f7fffffu, 0x3f800000u,
				0x3ff33333u, 0x40401b4eu, 0x3f800000u, 0x3ff33333u, 0x40401b4eu,
				0x00000000u, 0x00000000u, 0x00000000u, 0x00000000u, 0x00000000u,
				0x00000000u, 0x00000000u, 0x00000000u, 0x00000000u, 0x3dcccccdu,
				0x00000000u, 0xbf800000u, 0x3dccc900u, 0x3e55603au, 0x3f00a368u,
			},
			2 => new uint[]
			{
				0x3f7fffffu, 0x00000000u, 0x00000000u, 0x3f7fffffu, 0x3f80369du,
				0x3ff3a06du, 0x40406d3au, 0x3f4fc963u, 0x3ff3a06du, 0x403a58bfu,
				0x3dc49ba5u, 0x3e449ba5u, 0x3e9374bcu, 0x00000000u, 0x00000000u,
				0x00000000u, 0x00000000u, 0x3dcccccdu, 0x3e4ccccdu, 0x3ecccccdu,
				0x3dccccc0u, 0xbf800000u, 0x3eccccc0u, 0x3e55fd9du, 0x3f09dc36u,
			},
			_ => throw new ArgumentOutOfRangeException( nameof( variant ) )
		};
		var actual = new[]
		{
			state.Frame.M11, state.Frame.M13, state.Frame.M31, state.Frame.M33,
			state.Frame.M41, state.Frame.M42, state.Frame.M43,
			state.SecondaryFrame.M41, state.SecondaryFrame.M42, state.SecondaryFrame.M43,
			state.Feedback.X, state.Feedback.Y, state.Feedback.Z,
			state.AngularHistory.X, state.AngularHistory.Y, state.AngularHistory.Z, state.AngularHistory.W,
			state.Movement.X, state.Movement.Y, state.Movement.Z,
			state.Solved.Motion.X, state.Solved.Motion.Y, state.Solved.Motion.Z, cadence.Phase, cadence.Rate
		};
		for ( var index = 0; index < expected.Length; index++ )
			Assert.Equal( expected[ index ], BitConverter.SingleToUInt32Bits( actual[ index ] ) );
		Assert.Equal( variant == 0, state.ReferenceActive );
		if ( variant == 2 )
		{
			Assert.Equal( new Vector3( .19f, 0f, .095f ), state.SecondaryCorrection );
			Assert.Equal( 0x400cccccu, BitConverter.SingleToUInt32Bits( state.TargetFrame.M41 ) );
		}
	}

	[Fact]
	public void UnsupportedTargetModeRejectsBeforeChangingRetainedState()
	{
		var state = CreateState( 0 );
		var frame = state.Frame;
		var contact = state.Contact;
		Assert.False( PhysicalLocomotionOwner.TryPrepareAndSolve( state,
			new( Vector3.Zero, Matrix4x4.Identity, 8u, 1u, true, false, false, default ), default ) );
		Assert.Equal( frame, state.Frame );
		Assert.Equal( contact, state.Contact );
		Assert.Equal( new Vector3( 0f, 0f, .1f ), state.Movement );
	}

	private static PhysicalLocomotionOwnerState CreateState( int variant )
	{
		var frame = Matrix4x4.Identity;
		frame.Translation = new( 1f, 2f, 3f );
		var contact = Matrix4x4.Identity;
		contact.Translation = new( .1f, .2f, .3f );
		return new()
		{
			Frame = frame,
			Solved = new( Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, new( 1f, 2f, 3f ), Vector3.UnitZ ),
			Contact = new( contact, 7u, new( .2f, .3f, .4f ), new( .1f, .2f, .3f ),
				new( .4f, .5f, .6f ), new( .7f, .8f, .9f ) ),
			Movement = new( 0f, 0f, .1f ), SmoothedUp = Vector3.UnitY,
			AngularHistory = variant == 2 ? new( .1f, .2f, .3f, .4f ) : Vector4.Zero,
			Feedback = variant == 2 ? new( .1f, .2f, .3f ) : Vector3.Zero,
			SecondaryCorrection = variant == 2 ? new( .2f, 0f, .1f ) : Vector3.Zero,
			ReferenceActive = variant == 2, SecondaryCorrectionActive = variant == 2
		};
	}
}
