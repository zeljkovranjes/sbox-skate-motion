using System;
using System.Linq;
using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class EffectorRigControllerTests
{
	private static readonly LimbRigidTransform Identity = LimbRigidTransform.Identity;
	private static readonly EffectorRigFrame Frame = new( Identity, Identity, Identity, Identity.Rotation,
		Identity, Identity.Rotation, 0f, Vector3.Zero );

	[Fact]
	public void ModeRequestChangesWeightsDuringTheSameUpdate()
	{
		var state = State();
		state.Weights[ 0 ] = new EffectorWeightState( 0, .9f, 0f );
		var input = Input();
		input.Pinning[ 0 ] = true;
		Create().Update( state, Frame, input );
		Assert.Equal( new EffectorWeightState( 2, 0f, .5f ), state.Weights[ 0 ] );
		Assert.Equal( 2, state.Poses[ 0 ].TargetState.Mode );
		Assert.True( input.Pinning[ 0 ] );
	}

	[Fact]
	public void UpdatedWeightsOverrideStalePoseInputWeights()
	{
		var state = State();
		var input = Input();
		var before = input.Poses.ToArray();
		Create().Update( state, Frame, input );
		Assert.Equal( -2f * Vector3.UnitY, state.Poses[ 0 ].AnimatedTarget.Translation );
		Assert.Equal( before, input.Poses );
	}

	[Fact]
	public void ModeResetPrecedesReleaseTargetAccumulation()
	{
		var state = State();
		state.Poses[ 0 ] = state.Poses[ 0 ] with
		{
			TargetState = state.Poses[ 0 ].TargetState with { Offset = Vector3.One * 10f, Release = -.6f * Vector3.UnitY }
		};
		var input = Input();
		input.Releasing[ 0 ] = true;
		Create().Update( state, Frame, input );
		Assert.Equal( 3, state.Weights[ 0 ].Mode );
		Assert.Equal( 1f, state.Weights[ 0 ].Override );
		Assert.InRange( Vector3.Distance( .1f * Vector3.UnitY, state.Poses[ 0 ].TargetState.Offset ), 0f, 1e-7f );
	}

	[Fact]
	public void HandWeightUsesPreviousPreparedPosition()
	{
		var state = State();
		state.Weights[ 0 ] = default;
		state.Weights[ 2 ] = new EffectorWeightState( 1, 0f, 0f );
		state.Poses[ 2 ] = state.Poses[ 2 ] with { LocalPosition = Vector3.One * 10f };
		var input = Input() with { LeftHandEnabled = true };
		input.Poses[ 2 ] = input.Poses[ 2 ] with { Authored = Identity };
		var controller = Create();
		controller.Update( state, Frame, input );
		Assert.Equal( 0f, state.Weights[ 2 ].Target );
		Assert.Equal( Vector3.Zero, state.Poses[ 2 ].LocalPosition );
		controller.Update( state, Frame, input );
		Assert.Equal( .25f, state.Weights[ 2 ].Target );
	}

	[Fact]
	public void ReleaseDecayCompletesBeforeDefaultModeReturns()
	{
		var state = State();
		var input = Input();
		input.Releasing[ 0 ] = true;
		var controller = Create();
		controller.Update( state, Frame, input );
		Assert.Equal( 1f, state.Weights[ 0 ].Override );
		input.Releasing[ 0 ] = false;
		foreach ( var expected in new[] { .75f, .5f, .25f, 0f } )
		{
			controller.Update( state, Frame, input );
			Assert.Equal( 3, state.Weights[ 0 ].Mode );
			Assert.Equal( expected, state.Weights[ 0 ].Override );
		}
		controller.Update( state, Frame, input );
		Assert.Equal( 0, state.Weights[ 0 ].Mode );
		Assert.Equal( Vector3.Zero, state.Poses[ 0 ].TargetState.Offset );
	}

	[Fact]
	public void RequestsRemainAvailableToTheirOwningCaller()
	{
		var state = State();
		var input = Input();
		input.Pinning[ 0 ] = true;
		input.Releasing[ 0 ] = true;
		state.Poses[ 0 ] = state.Poses[ 0 ] with { TargetState = state.Poses[ 0 ].TargetState with { OrientationRequested = true } };
		Create().Update( state, Frame, input );
		Assert.True( input.Pinning[ 0 ] );
		Assert.True( input.Releasing[ 0 ] );
		Assert.True( state.Poses[ 0 ].TargetState.OrientationRequested );
		Assert.Equal( 2, state.Weights[ 0 ].Mode );
	}

	[Fact]
	public void FootCorrectionRunsBeforeFinalBoneApplication()
	{
		var state = State( validChain: true );
		state.Feet[ 0 ] = new FootReachState( 0, .25f, 0f, Vector3.Zero );
		var footSettings = new FootReachSettings( -1f, 1f, -.25f, .25f, 0f, 0f, 1f, 0f, 1f, 1f, 0f );
		Create( footSettings ).Update( state, Frame, Input() );
		Assert.Equal( .25f, state.Feet[ 0 ].Smoothed );
		Assert.Equal( -Vector3.UnitY, state.Skeleton.MutableBones[ 2 ].Translation );
		Assert.Equal( -Vector3.UnitY, state.Poses[ 0 ].AnimatedTarget.Translation );
	}

	[Fact]
	public void FreshContactsAndPersistentFlagsSurviveTheWholeSequence()
	{
		var state = State();
		state.FootFlags = new FootReachFlags( true, true );
		var input = Input();
		input.Contacts[ 0 ] = new FootReachInput( 7, new Vector3( .1f, .2f, .3f ) );
		Create().Update( state, Frame, input );
		Assert.Equal( 1, state.Feet[ 0 ].ContactState );
		Assert.Equal( new Vector3( .1f, .2f, .3f ), state.Feet[ 0 ].CachedContact );
		Assert.True( state.FootFlags.Persistent );
		Assert.False( state.FootFlags.Frame );
	}

	[Fact]
	public void BoneFailureStillRetainsEarlierStageUpdates()
	{
		var state = State();
		var previousBones = state.Skeleton.MutableBones.ToArray();
		Create().Update( state, Frame, Input() );
		Assert.Equal( previousBones, state.Skeleton.MutableBones );
		Assert.Equal( -2f * Vector3.UnitY, state.Poses[ 0 ].AnimatedTarget.Translation );
		Assert.Equal( -2f * Vector3.UnitY, state.Poses[ 0 ].LocalPosition );
	}

	[Fact]
	public void ReusingControllerDoesNotCarryAnotherRigsRequests()
	{
		var controller = Create();
		var first = State();
		var pinned = Input();
		pinned.Pinning[ 0 ] = true;
		controller.Update( first, Frame, pinned );
		var second = State();
		controller.Update( second, Frame, Input() );
		Assert.Equal( 1, second.Weights[ 0 ].Mode );
		Assert.Equal( 0f, second.Weights[ 0 ].Override );
		Assert.Equal( -2f * Vector3.UnitY, second.Poses[ 0 ].AnimatedTarget.Translation );
	}

	[Fact]
	public void InvalidInputCountDoesNotAdvancePersistentState()
	{
		var state = State();
		var previous = state.Weights.ToArray();
		Assert.Throws<ArgumentException>( () => Create().Update( state, Frame, Input() with { Contacts = new FootReachInput[ 1 ] } ) );
		Assert.Equal( previous, state.Weights );
	}

	private static EffectorRigState State( bool validChain = false )
	{
		var animated = new[] { Identity, validChain ? At( new Vector3( .3f, -.4f, 0f ) ) : Identity, At( -.8f * Vector3.UnitY ) };
		var skeleton = new EffectorRigSkeleton( Enumerable.Repeat( new EffectorBoneBinding( 2, -1, 10f ), 4 ).ToArray(),
			new[] { 0, 0, 1 }, Enumerable.Repeat( Identity, 3 ).ToArray(), animated, animated.ToArray() );
		var weights = new EffectorWeightState[ 4 ];
		weights[ 0 ] = new EffectorWeightState( 1, 1f, 0f );
		var pose = new EffectorPoseState( new EffectorTargetState( 0, -.7f * Vector3.UnitY, -.6f * Vector3.UnitY,
			Vector3.Zero, Vector3.UnitY, .5f, 0f, false ), Identity, Identity, Identity, Identity, Identity, Identity, Vector3.Zero, false );
		return new EffectorRigState( weights, Enumerable.Repeat( pose, 4 ).ToArray(), new FootReachState[ 2 ], skeleton, default );
	}

	private static EffectorRigInput Input() => new(
		Enumerable.Repeat( new EffectorPoseInput( At( -.8f * Vector3.UnitY ), null, At( -2f * Vector3.UnitY ), 0f, 1f ), 4 ).ToArray(),
		new bool[ 4 ], new bool[ 4 ], new[] { new FootReachInput( 7, null ), new FootReachInput( 7, null ) },
		false, false, false, false, 7, false, false, 0f, 180f );

	private static LimbRigidTransform At( Vector3 position ) => Identity with { Translation = position };

	private static EffectorRigController Create( FootReachSettings? footSettings = null )
	{
		var math = new LimbSolverMath( new[] { -1f / 720f, 1f / 24f, -.5f },
			new[] { -1f / 5040f, 1f / 120f, -1f / 6f }, new[] { 0f, 0f, 0f, 0f, 0f, 0f, 0f, MathF.PI / 2f },
			2f / MathF.PI, MathF.PI / 2f, 0f, MathF.PI );
		var targets = new EffectorTargetSolver( math, new LimbArcSine( 0f, 0f, 0f, 1f, MathF.PI / 2f ),
			new EffectorTargetSettings( .0001f, .5f, .125f, .01f, .25f, -.6f, .6f ) );
		var arc = new LimbArcTangent( new[] { 1f / 17f, -1f / 15f, 1f / 13f, -1f / 11f, 1f / 9f, -1f / 7f, 1f / 5f, -1f / 3f }, MathF.PI / 2f, MathF.PI );
		var positions = new LimbPositionSolver( math, .0005f, MathF.PI / 180f, 180f / MathF.PI );
		return new EffectorRigController( new EffectorWeightController( .25f, .25f, Vector3.One, Vector3.One ),
			new EffectorPosePreparer( targets ), new EffectorPoseBlender( new LimbPoseBlender( math, arc, .00001f, .1f ) ),
			new FootReachController( footSettings ?? new FootReachSettings( 0f, 0f, 0f, 0f, 0f, 0f, 1f, 0f, 1f, 1f, 0f ) ),
			new EffectorRigSolver( new LimbChainSolver( positions, new LimbRotationSolver( .0001f, .001f ), .0001f ) ), Vector3.One );
	}
}
