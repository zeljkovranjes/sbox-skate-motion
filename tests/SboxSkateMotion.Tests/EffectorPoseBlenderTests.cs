using System;
using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class EffectorPoseBlenderTests
{
	private static readonly LimbRigidTransform Identity = LimbRigidTransform.Identity;
	private static readonly EffectorPoseInput Input = new( Identity, Identity, Identity, .5f, .5f );

	[Theory]
	[InlineData( 0 )]
	[InlineData( 4 )]
	public void InactiveAndUnknownModesPreserveEveryCache( int mode )
	{
		var state = State( mode );
		Assert.Equal( state, Run( state, Input ) );
	}

	[Theory]
	[InlineData( 0f )]
	[InlineData( -.5f )]
	[InlineData( float.NaN )]
	public void TargetModeSkipsNonpositiveOrUnorderedWeight( float weight )
	{
		var state = State( 1 );
		Assert.Equal( state, Run( state, Input with { TargetWeight = weight } ) );
	}

	[Fact]
	public void TargetModeBlendsBothPosesAndPreservesOtherState()
	{
		var state = State( 1 );
		var result = Run( state, Input );
		Assert.Equal( state with
		{
			AnimatedTarget = At( 2f ), AnimatedReference = At( 6f )
		}, result );
	}

	[Fact]
	public void MissingReferencePreservesItsCacheInTargetMode()
	{
		var state = State( 1 );
		var result = Run( state, Input with { Reference = null } );
		Assert.Equal( state.AnimatedReference, result.AnimatedReference );
		Assert.Equal( At( 2f ), result.AnimatedTarget );
	}

	[Fact]
	public void PrimaryAndLinkedTranslationsKeepTheirDistinctRounding()
	{
		var epsilon = MathF.Pow( 2f, -23f );
		var state = State( 1 ) with
		{
			AnimatedTarget = At( -1f / epsilon ), AnimatedReference = At( -1f / epsilon ),
			AuthoredTarget = At( 1f + epsilon ), AuthoredReference = At( 1f + epsilon )
		};
		var result = Run( state, Input with { TargetWeight = 1f - epsilon } );
		Assert.Equal( 0f, result.AnimatedTarget.Translation.X );
		Assert.Equal( -MathF.Pow( 2f, -46f ), result.AnimatedReference.Translation.X );
	}

	[Fact]
	public void CompletedRotationStillExtrapolatesAuthoredPosition()
	{
		var result = Run( State( 1 ), Input with { TargetWeight = 2f } );
		Assert.Equal( At( 8f ), result.AnimatedTarget );
		Assert.Equal( At( 12f ), result.AnimatedReference );
	}

	[Theory]
	[InlineData( 2 )]
	[InlineData( 3 )]
	public void OverridesRunWithInactiveTargetAndMissingReference( int mode )
	{
		var result = Run( State( mode ), Input with { TargetWeight = 0f, Reference = null } );
		Assert.Equal( At( 5f ), result.AnimatedTarget );
		Assert.Equal( At( 12f ), result.AnimatedReference );
	}

	[Fact]
	public void OverrideRunsAfterAuthoredBlending()
	{
		var result = Run( State( 2 ), Input );
		Assert.Equal( At( 6f ), result.AnimatedTarget );
		Assert.Equal( At( 13f ), result.AnimatedReference );
	}

	[Theory]
	[InlineData( 1f )]
	[InlineData( 2f )]
	[InlineData( float.NaN )]
	public void CompletedOrUnorderedOverrideCopiesPinnedCaches( float weight )
	{
		var state = State( 2 );
		var result = Run( state, Input with { OverrideWeight = weight } );
		Assert.Equal( state.PinnedTarget, result.AnimatedTarget );
		Assert.Equal( state.PinnedReference, result.AnimatedReference );
	}

	[Fact]
	public void AuthoredFrameUsesWorldRootPositionAndSuppliedOrientation()
	{
		var inputs = Inputs( Input with { TargetWeight = 1f } );
		var states = States( State( 1 ) );
		var orientation = new LimbFrame( Vector3.UnitY, -Vector3.UnitX, Vector3.UnitZ );
		Create().Update( inputs, states, At( 2f ), At( 3f ), orientation );
		Assert.Equal( new Vector3( 5f, 4f, 0f ), states[ 0 ].AnimatedTarget.Translation );
		Assert.Equal( orientation, states[ 0 ].AnimatedTarget.Rotation );
	}

	[Fact]
	public void FramesAreOrthonormalizedBeforeCompletedRotationBlend()
	{
		var authored = new LimbRigidTransform( new LimbFrame( new Vector3( 2f, 1f, 1f ),
			new Vector3( 0f, 3f, 1f ), Vector3.UnitZ * 4f ), Vector3.Zero );
		var result = Run( State( 1 ) with { AuthoredTarget = authored }, Input with { TargetWeight = 1f } );
		Assert.Equal( Identity.Rotation, result.AnimatedTarget.Rotation );
	}

	[Fact]
	public void AllFourSlotsRetainIndependentModesAndWeights()
	{
		var inputs = Inputs( Input );
		var states = new[] { State( 0 ), State( 1 ), State( 2 ), State( 3 ) };
		inputs[ 3 ] = Input with { OverrideWeight = 1f };
		Create().Update( inputs, states, Identity, Identity, Identity.Rotation );
		Assert.Equal( At( 0f ), states[ 0 ].AnimatedTarget );
		Assert.Equal( At( 2f ), states[ 1 ].AnimatedTarget );
		Assert.Equal( At( 6f ), states[ 2 ].AnimatedTarget );
		Assert.Equal( At( 10f ), states[ 3 ].AnimatedTarget );
	}

	[Fact]
	public void RequiresFourSlots()
	{
		Assert.Throws<ArgumentException>( () => Create().Update( new EffectorPoseInput[ 3 ], States( State( 1 ) ),
			Identity, Identity, Identity.Rotation ) );
	}

	private static LimbRigidTransform At( float x ) => Identity with { Translation = new Vector3( x, 0f, 0f ) };
	private static EffectorPoseInput[] Inputs( EffectorPoseInput input ) => new[] { input, input, input, input };
	private static EffectorPoseState[] States( EffectorPoseState state ) => new[] { state, state, state, state };
	private static EffectorPoseState State( int mode ) => new(
		new EffectorTargetState( mode, Vector3.One, Vector3.One, Vector3.One, Vector3.UnitY, .3f, .4f, true ),
		At( 10f ), At( 20f ), At( 0f ), At( 4f ), At( 4f ), At( 8f ), Vector3.One, true );

	private static EffectorPoseState Run( EffectorPoseState state, EffectorPoseInput input )
	{
		var states = States( state );
		Create().Update( Inputs( input ), states, Identity, Identity, Identity.Rotation );
		return states[ 0 ];
	}

	private static EffectorPoseBlender Create()
	{
		var angles = new LimbSolverMath( new[] { -1f / 720f, 1f / 24f, -.5f },
			new[] { -1f / 5040f, 1f / 120f, -1f / 6f }, new float[ 8 ], 2f / MathF.PI, MathF.PI / 2f, 0f, MathF.PI );
		var arc = new LimbArcTangent( new[] { 1f / 17f, -1f / 15f, 1f / 13f, -1f / 11f,
			1f / 9f, -1f / 7f, 1f / 5f, -1f / 3f }, MathF.PI / 2f, MathF.PI );
		return new EffectorPoseBlender( new LimbPoseBlender( angles, arc, .00001f, .1f ) );
	}
}
