using System;
using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class PhysicalHandPinControllerTests
{
	[Theory]
	[InlineData( false, 2, 5 )]
	[InlineData( true, 3, 9 )]
	public void UsesRawArmAnchorAndWritesOnlySelectedPin( bool rightHand, int slot, int bone )
	{
		var state = State();
		state.Skeleton.AnimatedBones[ bone ] = At( 2, 0, 0 );
		state.Skeleton.MutableBones[ bone ] = At( 100, 100, 100 );
		var before = state.Poses.ToArray();
		var pinning = new[] { true, false, false, false };
		PhysicalHandPinController.Update( state, pinning, rightHand, At( 10, 0, 0 ), new Vector3( 15, 4, 0 ), 2.5f, -.25f );
		Assert.Equal( new Vector3( 13.5f, 2f, 0f ), state.Poses[ slot ].TargetState.Pin );
		Assert.Equal( -.25f, state.Poses[ slot ].TargetState.Weight );
		Assert.Equal( before[ slot ] with
		{
			TargetState = before[ slot ].TargetState with { Pin = new Vector3( 13.5f, 2f, 0f ), Weight = -.25f }
		}, state.Poses[ slot ] );
		for ( var i = 0; i < 4; i++ )
		{
			if ( i != slot )
				Assert.Equal( before[ i ], state.Poses[ i ] );
			Assert.Equal( i == 0 || i == slot, pinning[ i ] );
		}
	}

	[Fact]
	public void RetainedRequestsReplaceWeightAndRespectZeroAndExactReach()
	{
		var state = State();
		var pinning = new bool[ 4 ];
		var target = new Vector3( 3, 4, 0 );
		PhysicalHandPinController.Update( state, pinning, false, At( 0, 0, 0 ), target, 5f, 1.25f );
		Assert.Equal( target, state.Poses[ 2 ].TargetState.Pin );
		Assert.Equal( 1.25f, state.Poses[ 2 ].TargetState.Weight );
		PhysicalHandPinController.Update( state, pinning, false, At( 0, 0, 0 ), target, 0f, 0f );
		Assert.Equal( Vector3.Zero, state.Poses[ 2 ].TargetState.Pin );
		Assert.Equal( 0f, state.Poses[ 2 ].TargetState.Weight );
		PhysicalHandPinController.Update( state, pinning, true, At( 0, 0, 0 ), Vector3.Zero, 0f, .5f );
		Assert.Equal( Vector3.Zero, state.Poses[ 3 ].TargetState.Pin );
		Assert.True( pinning[ 2 ] && pinning[ 3 ] );
	}

	[Fact]
	public void NegativeReachRetainsSignedProjectionAndDegenerateResult()
	{
		var state = State();
		var pinning = new bool[ 4 ];
		PhysicalHandPinController.Update( state, pinning, false, At( 0, 0, 0 ), new Vector3( 2, 0, 0 ), -1f, .5f );
		Assert.Equal( new Vector3( -1, 0, 0 ), state.Poses[ 2 ].TargetState.Pin );
		PhysicalHandPinController.Update( state, pinning, false, At( 0, 0, 0 ), Vector3.Zero, -1f, .5f );
		var pin = state.Poses[ 2 ].TargetState.Pin;
		Assert.True( float.IsNaN( pin.X ) && float.IsNaN( pin.Y ) && float.IsNaN( pin.Z ) );
		Assert.Equal( 0x7fc00000u, BitConverter.SingleToUInt32Bits( pin.X ) );
		Assert.Equal( 0x7fc00000u, BitConverter.SingleToUInt32Bits( pin.Y ) );
		Assert.Equal( 0x7fc00000u, BitConverter.SingleToUInt32Bits( pin.Z ) );
		Assert.True( pinning[ 2 ] );
	}

	private static EffectorRigState State()
	{
		var skeleton = new EffectorRigSkeleton( new EffectorBoneBinding[ 4 ], new int[ 24 ],
			new LimbRigidTransform[ 24 ], new LimbRigidTransform[ 24 ], new LimbRigidTransform[ 24 ] );
		var poses = new EffectorPoseState[ 4 ];
		for ( var i = 0; i < 24; i++ )
			skeleton.AnimatedBones[ i ] = LimbRigidTransform.Identity;
		for ( var i = 0; i < 4; i++ )
		{
			var target = new EffectorTargetState( 3, new Vector3( i ), new Vector3( 2 ), new Vector3( 3 ),
				Vector3.UnitY, .125f, .25f, true );
			poses[ i ] = new EffectorPoseState( target, At( 1, 0, 0 ), At( 2, 0, 0 ), At( 3, 0, 0 ),
				At( 4, 0, 0 ), At( 5, 0, 0 ), At( 6, 0, 0 ), new Vector3( 7 ), true );
		}
		return new EffectorRigState( new EffectorWeightState[ 4 ], poses, new FootReachState[ 2 ], skeleton, default );
	}

	private static LimbRigidTransform At( float x, float y, float z ) =>
		LimbRigidTransform.Identity with { Translation = new Vector3( x, y, z ) };
}
