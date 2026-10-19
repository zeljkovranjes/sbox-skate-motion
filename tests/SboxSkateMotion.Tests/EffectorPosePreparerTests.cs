using System;
using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class EffectorPosePreparerTests
{
	private static readonly LimbRigidTransform Identity = LimbRigidTransform.Identity;
	private static readonly LimbRigidTransform Cached = Identity with { Translation = new Vector3( 7f, 8f, 9f ) };
	private static readonly EffectorPoseInput Input = new( Identity, null, Identity, 0f, 1f );

	[Fact]
	public void InactiveSlotPreservesCachesAndClearsReachFlag()
	{
		var state = State( 0 );
		var result = Run( state, Input );
		Assert.Equal( state with { InsideBounds = false }, result );
	}

	[Theory]
	[InlineData( 0, .2f, 1f, true )]
	[InlineData( 0, 0f, .99f, true )]
	[InlineData( 0, 0f, 1f, false )]
	[InlineData( 0, -1f, 1f, false )]
	[InlineData( 1, float.NaN, float.NaN, true )]
	[InlineData( 0, float.NaN, float.NaN, false )]
	[InlineData( 4, .2f, 1f, true )]
	public void ModeAndWeightsDetermineWhichCachesRefresh( int mode, float targetWeight, float overrideWeight, bool refresh )
	{
		var result = Run( State( mode ), Input with { TargetWeight = targetWeight, OverrideWeight = overrideWeight } );
		Assert.Equal( refresh ? Identity : Cached, result.AnimatedTarget );
		Assert.Equal( refresh ? Identity : Cached, result.AuthoredTarget );
		Assert.Equal( refresh, result.InsideBounds );
	}

	[Fact]
	public void AnimatedAndAuthoredTargetsUseTheirRespectiveFrames()
	{
		var inputs = Inputs( Input with { Animated = Identity with { Translation = Vector3.UnitX },
			Authored = Identity with { Translation = new Vector3( 2f, 2f, 3f ) } } );
		var states = States( State( 1 ) );
		Create().Update( inputs, states, Identity with { Translation = Vector3.UnitX },
			Identity with { Translation = new Vector3( 10f, 0f, 0f ) }, Identity, new Vector3( 1f, 2f, 3f ) );
		Assert.All( states, state =>
		{
			Assert.Equal( new Vector3( 11f, 0f, 0f ), state.AnimatedTarget.Translation );
			Assert.Equal( new Vector3( 1f, 2f, 3f ), state.AuthoredTarget.Translation );
			Assert.Equal( state.AuthoredTarget.Translation, state.LocalPosition );
			Assert.True( state.InsideBounds );
		} );
	}

	[Fact]
	public void PinPreparationRunsEvenWhenOtherCachesAreInactive()
	{
		var state = State( 2 );
		state = state with { TargetState = state.TargetState with { Pin = new Vector3( .25f, 0f, 0f ) } };
		var result = Run( state, Input );
		Assert.Equal( state.TargetState.Pin, result.PinnedTarget.Translation );
		Assert.Equal( state.TargetState.Pin, result.TargetState.Release );
		Assert.Equal( Cached, result.AnimatedTarget );
		Assert.Equal( Cached, result.AuthoredTarget );
		Assert.False( result.InsideBounds );
	}

	[Fact]
	public void ReleasePreparationPersistsItsUpdatedOffset()
	{
		var state = State( 3 );
		state = state with { TargetState = state.TargetState with { Release = Vector3.UnitX, Weight = 1f } };
		var result = Run( state, Input );
		Assert.Equal( new Vector3( .125f, 0f, 0f ), result.TargetState.Offset );
		Assert.Equal( Vector3.UnitX, result.TargetState.Release );
		Assert.Equal( Cached, result.AnimatedTarget );
	}

	[Fact]
	public void MissingReferencePreservesAllReferenceCaches()
	{
		var result = Run( State( 2 ), Input with { TargetWeight = 1f } );
		Assert.Equal( Cached, result.PinnedReference );
		Assert.Equal( Cached, result.AnimatedReference );
		Assert.Equal( Cached, result.AuthoredReference );
		Assert.Equal( Identity, result.AnimatedTarget );
	}

	[Fact]
	public void ReferenceUsesTheAnimatedRelativeTransformForBothTargets()
	{
		var result = Run( State( 1 ), Input with
		{
			Animated = Identity with { Translation = Vector3.UnitX },
			Reference = Identity with { Translation = Vector3.UnitX + Vector3.UnitZ },
			Authored = Identity with { Translation = Vector3.UnitY }
		} );
		Assert.Equal( Vector3.UnitX + Vector3.UnitZ, result.AnimatedReference.Translation );
		Assert.Equal( Vector3.UnitY + Vector3.UnitZ, result.AuthoredReference.Translation );
	}

	[Fact]
	public void BoundsIncludeTheirFacesAndRejectPointsOutsideAnyAxis()
	{
		var inputs = Inputs( Input );
		inputs[ 0 ] = Input with { Authored = Identity with { Translation = Vector3.One } };
		inputs[ 1 ] = Input with { Authored = Identity with { Translation = new Vector3( -1.001f, 0f, 0f ) } };
		inputs[ 2 ] = Input with { Authored = Identity with { Translation = new Vector3( 0f, 1.001f, 0f ) } };
		inputs[ 3 ] = Input with { Authored = Identity with { Translation = new Vector3( 0f, 0f, -1.001f ) } };
		var states = States( State( 1 ) );
		Create().Update( inputs, states, Identity, Identity, Identity, Vector3.One );
		Assert.True( states[ 0 ].InsideBounds );
		Assert.False( states[ 1 ].InsideBounds );
		Assert.False( states[ 2 ].InsideBounds );
		Assert.False( states[ 3 ].InsideBounds );
	}

	[Fact]
	public void SlotModesAndCachedPositionsAreIndependent()
	{
		var states = new[] { State( 0 ), State( 1 ), State( 2 ), State( 3 ) };
		Create().Update( Inputs( Input ), states, Identity, Identity, Identity, Vector3.One );
		Assert.Equal( Cached.Translation, states[ 0 ].LocalPosition );
		Assert.Equal( Vector3.Zero, states[ 1 ].LocalPosition );
		Assert.Equal( Cached.Translation, states[ 2 ].LocalPosition );
		Assert.Equal( Cached.Translation, states[ 3 ].LocalPosition );
		Assert.Equal( Cached, states[ 0 ].PinnedTarget );
		Assert.Equal( Cached, states[ 1 ].PinnedTarget );
		Assert.Equal( Identity, states[ 2 ].PinnedTarget );
		Assert.Equal( Identity, states[ 3 ].PinnedTarget );
	}

	[Fact]
	public void InvalidSlotCountDoesNotMutateCaches()
	{
		var states = States( State( 1 ) );
		Assert.Throws<ArgumentException>( () => Create().Update( new EffectorPoseInput[ 3 ], states,
			Identity, Identity, Identity, Vector3.One ) );
		Assert.Equal( States( State( 1 ) ), states );
	}

	private static EffectorPoseState Run( EffectorPoseState state, EffectorPoseInput input )
	{
		var states = States( state );
		Create().Update( Inputs( input ), states, Identity, Identity, Identity, Vector3.One );
		return states[ 0 ];
	}

	private static EffectorPoseInput[] Inputs( EffectorPoseInput input ) => new[] { input, input, input, input };

	private static EffectorPoseState[] States( EffectorPoseState state ) => new[] { state, state, state, state };

	private static EffectorPoseState State( int mode ) => new(
		new EffectorTargetState( mode, Vector3.Zero, Vector3.Zero, Vector3.Zero, Vector3.UnitY, 1f, 0f, false ),
		Cached, Cached, Cached, Cached, Cached, Cached, Cached.Translation, true );

	private static EffectorPosePreparer Create()
	{
		var math = new LimbSolverMath( new[] { -1f / 720f, 1f / 24f, -.5f }, new[] { -1f / 5040f, 1f / 120f, -1f / 6f },
			new[] { 0f, 0f, 0f, 0f, 0f, 0f, 0f, MathF.PI / 2f }, 2f / MathF.PI, MathF.PI / 2f, 0f, MathF.PI );
		return new EffectorPosePreparer( new EffectorTargetSolver( math, new LimbArcSine( 0f, 0f, 0f, 1f, MathF.PI / 2f ),
			new EffectorTargetSettings( .0001f, .5f, .125f, .01f, .25f, -.6f, .6f ) ) );
	}
}
