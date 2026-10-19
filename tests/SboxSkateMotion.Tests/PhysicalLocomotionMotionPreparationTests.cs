using System;
using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class PhysicalLocomotionMotionPreparationTests
{
	[Theory]
	[InlineData( false )]
	[InlineData( true )]
	public void SolvedPositionHistoryFeedsGaitAndLimitsDownwardAcceleration( bool falling )
	{
		var retained = new PhysicalLocomotionMotionState( Vector3.UnitX, Vector3.UnitY,
			Vector3.UnitZ, Vector3.Zero, falling ? new( 0f, -2f, 2f ) : Vector3.UnitZ );
		var input = new PhysicalLocomotionMotionInput( falling ? new( 0f, -.1f, .08f ) : new( 0f, 0f, .04f ),
			Vector3.UnitZ, falling ? new( .2f, .9f, .1f ) : Vector3.UnitY, Vector3.UnitY );
		var solved = PhysicalLocomotionMotionPreparation.Update( retained, input );
		uint[] expected = falling
			? new uint[] { 0x3e7d1a1bu, 0x403521e7u, 0xbd4cccccu, 0x3da3d708u, 0xc0400000u, 0x40999999u, 0x3e5e2305u, 0x3f79e764u, 0x328ad5e3u }
			: new uint[] { 0x3e6147aeu, 0x3f999999u, 0x00000000u, 0x3d23d708u, 0x00000000u, 0x40199999u, 0x00000000u, 0x3f7fffffu, 0x00000000u };
		Assert.Equal( expected[ 2 ], Bits( solved.Position.Y ) );
		Assert.Equal( expected[ 3 ], Bits( solved.Position.Z ) );
		Assert.Equal( expected[ 4 ], Bits( solved.Motion.Y ) );
		Assert.Equal( expected[ 5 ], Bits( solved.Motion.Z ) );
		Assert.Equal( expected[ 6 ], Bits( solved.FrameRow1.X ) );
		Assert.Equal( expected[ 7 ], Bits( solved.FrameRow1.Y ) );
		Assert.Equal( expected[ 8 ], Bits( solved.FrameRow1.Z ) );
		var attributes = new PhysicalAnimationAttributeState
		{
			AnimationTranslation = new( 0f, 0f, 2f, 0f ), AnimationTime = 0f, CadenceEndPercent = -1f
		};
		var cadence = PhysicalLocomotionCadenceState.Initial with { Phase = .2f, Duration = .6f };
		var physical = new PhysicalLocomotionCadenceInput( Vector3.Zero, Vector3.Zero, Vector3.Zero,
			Vector3.Zero, false, Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, Vector3.Zero, 0f, 99f, false, 0u );
		Assert.True( PhysicalLocomotionMotionPreparation.TryUpdateCadence( solved, ref cadence, physical,
			new( .01f, 2f, 5f, 1e10f ), attributes, null ) );
		Assert.Equal( expected[ 0 ], Bits( cadence.Phase ) );
		Assert.Equal( expected[ 1 ], Bits( cadence.Rate ) );
	}

	private static uint Bits( float value ) => BitConverter.SingleToUInt32Bits( value );
}
