using System;
using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class PhysicalLocomotionContactReferenceTests
{
	[Theory]
	[InlineData( true )]
	[InlineData( false )]
	public void ContactHistorySuppliesRelativeGaitAndResetsOnIdentityChange( bool sameContact )
	{
		var oldFrame = Matrix4x4.Identity;
		oldFrame.Translation = new( .1f, .2f, .3f );
		var frame = new Matrix4x4( 0f, 0f, -1f, 0f, 0f, 1f, 0f, 0f,
			1f, 0f, 0f, 0f, .11f, .2f, .32f, 1f );
		var initial = new PhysicalLocomotionContactReferenceState( oldFrame, 7u,
			new( .2f, .3f, .4f ), new( .1f, .2f, .3f ), new( .4f, .5f, .6f ), new( .7f, .8f, .9f ) );
		var identity = sameContact ? 7u : 8u;
		var result = PhysicalLocomotionContactReference.Update( initial, frame, identity, true, new( 1f, 2f, 3f ) );
		uint[] expected = sameContact
			? new uint[] { 0x42d93334u, 0x00000000u, 0xc356ccceu, 0x4358cccdu, 0xbf000000u, 0xc3d7199au, 0x42d93334u, 0x00000000u, 0xc356ccceu, 0x45c11bb8u, 0xc182b852u, 0xc63fa62fu, 0x3f497e28u, 0x43573993u }
			: new uint[] { 0x00000000u, 0x00000000u, 0x00000000u, 0x00000000u, 0x00000000u, 0x00000000u, 0x3ecccccdu, 0x3f000000u, 0x3f19999au, 0x00000000u, 0x00000000u, 0x00000000u, 0x3e55603au, 0x3f00a368u };
		var vectors = new[] { result.Velocity, result.Reference, result.PreviousVelocity, result.Acceleration };
		for ( var index = 0; index < vectors.Length; index++ )
		{
			Assert.Equal( expected[ index * 3 ], Bits( vectors[ index ].X ) );
			Assert.Equal( expected[ index * 3 + 1 ], Bits( vectors[ index ].Y ) );
			Assert.Equal( expected[ index * 3 + 2 ], Bits( vectors[ index ].Z ) );
		}
		Assert.Equal( identity, result.Identity );
		Assert.Equal( frame, result.Frame );

		// The remaining owner frame preparation is an explicit input to this connection.
		uint[] prepared = sameContact
			? new uint[] { 0xbf7ffffbu, 0xba350a21u, 0x00000000u, 0x3a350a21u, 0xbf7ffffbu, 0x3ff33333u, 0x40401b4cu }
			: new uint[] { 0x3f7fffffu, 0x00000000u, 0x00000000u, 0x00000000u, 0x3f7fffffu, 0x3ff33333u, 0x40401b4eu };
		var row0 = new Vector3( Float( prepared[ 0 ] ), 0f, Float( prepared[ 1 ] ) );
		var row1 = new Vector3( 0f, Float( 0x3f7fffffu ), 0f );
		var row2 = new Vector3( Float( prepared[ 3 ] ), Float( prepared[ 2 ] ), Float( prepared[ 4 ] ) );
		var solved = PhysicalLocomotionMotionPreparation.Update(
			new( Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, new( 1f, 2f, 3f ), Vector3.UnitZ ),
			new( new( 1f, Float( prepared[ 5 ] ), Float( prepared[ 6 ] ) ), row2, Vector3.UnitY, Vector3.UnitY ) );
		var physical = new PhysicalLocomotionCadenceInput( Vector3.Zero, new( 999f ), Vector3.Zero,
			Vector3.Zero, false, row0, row1, row2, Vector3.Zero, 0f, 99f, false, 1u );
		var attributes = new PhysicalAnimationAttributeState
		{
			AnimationTranslation = new( 0f, 0f, 2f, 0f ), AnimationTime = 0f, CadenceEndPercent = -1f
		};
		var cadence = PhysicalLocomotionCadenceState.Initial with { Phase = .2f, Duration = .6f };
		Assert.True( PhysicalLocomotionContactReference.TryUpdateCadence( result, solved, ref cadence,
			physical, new( .01f, 2f, 5f, 1e10f ), attributes, null ) );
		Assert.Equal( expected[ 12 ], Bits( cadence.Phase ) );
		Assert.Equal( expected[ 13 ], Bits( cadence.Rate ) );
	}

	private static uint Bits( float value ) => BitConverter.SingleToUInt32Bits( value );
	private static float Float( uint value ) => BitConverter.UInt32BitsToSingle( value );
}
