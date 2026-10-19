using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class PhysicalHandContactControllerTests
{
	[Theory]
	[InlineData( false, false, 2 )]
	[InlineData( false, true, 3 )]
	[InlineData( true, false, 3 )]
	[InlineData( true, true, 2 )]
	public void HandSelectionCombinesObjectSideAndRequestSide( bool objectSide, bool requestSide, int slot )
	{
		var rig = Rig();
		var state = new PhysicalHandContactState { Flags = 1UL << 63 };
		var pinning = new bool[ 4 ];
		var input = Input() with
		{
			Point = new Vector3( .1f, 0, 0 ),
			ObjectFlags = objectSide ? 1UL << 29 : 0,
			RequestFlags = requestSide ? 1UL << 63 : 0
		};
		Controller().Update( state, rig, new EffectorPoseCache( 24 ), pinning, input );
		Assert.True( pinning[ slot ] );
		Assert.False( pinning[ slot == 2 ? 3 : 2 ] );
		Assert.Equal( input.Point, rig.Poses[ slot ].TargetState.Pin );
		Assert.InRange( state.Weight, .1666666f, .1666668f );
		Assert.Equal( state.Weight, rig.Poses[ slot ].TargetState.Weight );
	}

	[Fact]
	public void ResetClearsOnlyRetainedModesBeforeBuildingTheNextRequest()
	{
		var rig = Rig();
		var state = new PhysicalHandContactState { Weight = .9f, Radius = new Vector4( .3f ), Flags = 0xc000000000000123UL };
		var pinning = new bool[ 4 ];
		Controller().Update( state, rig, new EffectorPoseCache( 24 ), pinning,
			Input() with { RequestFlags = 1UL << 62 } );
		Assert.Equal( 0x123UL, state.Flags );
		Assert.Equal( new Vector4( -1f ), state.Radius );
		Assert.InRange( state.Weight, .1666666f, .1666668f );
		Assert.True( pinning[ 2 ] );
	}

	[Fact]
	public void FailedMetricLaneFadesOutWithoutClearingEarlierPinRequests()
	{
		var rig = Rig();
		var state = new PhysicalHandContactState { Flags = 1UL << 63 };
		var pinning = new bool[ 4 ];
		var cache = new EffectorPoseCache( 24 );
		var controller = Controller();
		controller.Update( state, rig, cache, pinning, Input() );
		var inactive = Input() with { Metric = new Vector4( 1, 1, 1, -1 ) };
		controller.Update( state, rig, cache, pinning, inactive );
		Assert.True( state.Weight > 0f );
		var lastWeight = rig.Poses[ 2 ].TargetState.Weight;
		controller.Update( state, rig, cache, pinning, inactive );
		Assert.Equal( 0f, state.Weight );
		Assert.Equal( lastWeight, rig.Poses[ 2 ].TargetState.Weight );
		Assert.True( pinning[ 2 ] );
		Assert.NotEqual( 0UL, state.Flags & (1UL << 63) );
	}

	[Fact]
	public void ProjectionContractsAcrossUpdatesAndUsesIndependentRawPinAnchor()
	{
		var rig = Rig();
		var cache = new EffectorPoseCache( 24 );
		cache.Bones[ 3 ] = At( .5f );
		cache.Bones[ 6 ] = At( 1f );
		var state = new PhysicalHandContactState { Radius = Vector4.One, Flags = 1UL << 62 };
		var pinning = new bool[ 4 ];
		var controller = Controller();
		controller.Update( state, rig, cache, pinning, Input() );
		Assert.Equal( .5f, state.Radius.X, 6 );
		Assert.Equal( .5f, rig.Poses[ 2 ].TargetState.Pin.X, 6 );
		rig.Skeleton.AnimatedBones[ 5 ] = At( -1f );
		controller.Update( state, rig, cache, pinning, Input() );
		Assert.Equal( .47f, state.Radius.X, 6 );
		Assert.Equal( -.35f, rig.Poses[ 2 ].TargetState.Pin.X, 6 );
	}

	private static PhysicalHandContactController Controller() => new(
		new GrindHeightCorrectionCurve( [ 0f, .25f, .75f, 1.5f ], [ .05f, .15f, .25f, .4f ] ),
		.1f, .12f, .2f, .03f );

	private static PhysicalHandContactInput Input() => new(
		LimbRigidTransform.Identity, Vector3.Zero, Vector4.One, 0, 0 );

	private static LimbRigidTransform At( float x ) =>
		LimbRigidTransform.Identity with { Translation = new Vector3( x, 0, 0 ) };

	private static EffectorRigState Rig()
	{
		var skeleton = new EffectorRigSkeleton( new EffectorBoneBinding[ 4 ], new int[ 24 ],
			new LimbRigidTransform[ 24 ], new LimbRigidTransform[ 24 ], new LimbRigidTransform[ 24 ] );
		for ( var i = 0; i < 24; i++ )
			skeleton.AnimatedBones[ i ] = LimbRigidTransform.Identity;
		return new EffectorRigState( new EffectorWeightState[ 4 ], new EffectorPoseState[ 4 ],
			new FootReachState[ 2 ], skeleton, default );
	}
}
