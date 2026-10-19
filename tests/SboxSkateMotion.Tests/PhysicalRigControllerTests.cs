using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class PhysicalRigControllerTests
{
	private static readonly LimbRigidTransform Identity = LimbRigidTransform.Identity;

	[Fact]
	public void SkippedRigStillUpdatesMotionJointDriveAndBodyCorrection()
	{
		var state = State();
		state.Frames = new( Identity, Identity, Identity, Identity, Identity, Identity, Vector3.One, Vector3.One );
		var previous = state.Frames;
		var input = Input() with { Frames = Input().Frames with { ModelToWorld = At( Vector3.UnitX ) }, DeltaTime = .5f };
		Create().Update( state, input );
		Assert.Equal( Vector3.UnitX * 2f, state.Velocity );
		Assert.Equal( Vector3.UnitX, state.PreviousPosition );
		Assert.Equal( previous, state.Frames );
		Assert.Equal( 2, state.RootDrive.Mode );
		Assert.True( state.Bodies[ 0 ].Body.LinearAccumulator.Y < 0f );
		Assert.Equal( Vector3.Zero, state.Bodies[ ^1 ].Body.LinearAccumulator );
	}

	[Theory]
	[InlineData( false, 0, 0 )]
	[InlineData( false, 7, 7 )]
	[InlineData( true, 7, 1 )]
	public void ResetFlagOnlyChangesWhenTheCallerRequestsMarking( bool mark, byte before, byte after )
	{
		var state = State();
		state.Preparation.SuppressionFlag = 1;
		state.ResetFlag = before;
		state.Reset.MotionFlag = 7;
		state.Bodies[ 0 ].Body.Velocity = Vector3.One;
		Create().Update( state, Input() with { MarkReset = mark } );
		Assert.Equal( after, state.ResetFlag );
		Assert.Equal( 0, state.Reset.MotionFlag );
		Assert.Equal( Vector3.Zero, state.Bodies[ 0 ].Body.Velocity );
	}

	[Fact]
	public void MotionUsesCallerTimeWhileCorrectionKeepsItsOwnTimeStep()
	{
		var first = State();
		var second = State();
		var controller = Create();
		var input = Input() with { Frames = Input().Frames with { ModelToWorld = At( Vector3.UnitX ) } };
		controller.Update( first, input with { DeltaTime = .5f } );
		controller.Update( second, input with { DeltaTime = .25f } );
		Assert.Equal( first.Velocity * 2f, second.Velocity );
		Assert.Equal( first.Bodies[ 0 ].Body.LinearAccumulator, second.Bodies[ 0 ].Body.LinearAccumulator );
		controller.Update( second, input with { DeltaTime = .25f } );
		Assert.Equal( Vector3.Zero, second.Velocity );
	}

	[Fact]
	public void SharedTargetBodyIsWrittenBeforeFeedbackVelocityIsComputed()
	{
		var state = State();
		state.Targets[ 2 ].Body = state.Bodies[ ^2 ].Body;
		state.Preparation.FirstFeedbackFrame = At( new( 2, 3, 4 ) );
		Create().Update( state, Input() );
		Assert.Equal( new Vector3( 2, 3, 4 ), state.Bodies[ ^2 ].Body.Pose.Translation );
		Assert.Equal( Vector3.Zero, state.Bodies[ ^2 ].Body.Velocity );
	}

	[Fact]
	public void RootBindingReadsCacheAndPreservesRawAnimationAndCallerInputs()
	{
		var state = State();
		var input = Input() with { SolveRig = true, BindAnimatedRoot = true, Gravity = 0f };
		state.Cache.Bones[ 0 ] = At( new( .1f, .2f, .3f ) );
		var before = input.RigInput.Poses.ToArray();
		Create().Update( state, input );
		Assert.Equal( state.Cache.Bones[ 0 ], state.Frames.Authored );
		Assert.Equal( Identity, state.Rig.Skeleton.AnimatedBones[ 0 ] );
		Assert.Equal( before, input.RigInput.Poses );
	}

	private static PhysicalRigState State()
	{
		var animated = new[] { Identity, Identity, At( -.8f * Vector3.UnitY ) };
		var skeleton = new EffectorRigSkeleton( Enumerable.Repeat( new EffectorBoneBinding( 2, -1, 10f ), 4 ).ToArray(),
			new[] { 0, 0, 1 }, Enumerable.Repeat( Identity, 3 ).ToArray(), animated, animated.ToArray() );
		var pose = new EffectorPoseState( new EffectorTargetState( 0, Vector3.Zero, Vector3.Zero,
			Vector3.Zero, Vector3.UnitY, 0f, 0f, false ), Identity, Identity, Identity, Identity, Identity, Identity, Vector3.Zero, false );
		var rig = new EffectorRigState( new EffectorWeightState[ 4 ], Enumerable.Repeat( pose, 4 ).ToArray(),
			new FootReachState[ 2 ], skeleton, default );
		var prep = new PhysicalPosePreparationState
		{
			ModelOffset = Identity, SecondaryTarget = Identity, FirstFeedbackFrame = Identity, SecondFeedbackFrame = Identity
		};
		var cache = new EffectorPoseCache( 3 );
		Array.Copy( animated, cache.Bones, 3 );
		return new( rig, cache, prep, new( 5 ), Bindings( 4 ), Bindings( 5 ), [], [ default, default ] );
	}

	private static PhysicalBodyBinding[] Bindings( int count ) => Enumerable.Range( 0, count )
		.Select( _ => new PhysicalBodyBinding { CachedPose = Identity, Body = new() { Pose = Identity, InverseMass = 1f } } ).ToArray();

	private static PhysicalRigInput Input() => new(
		new( Identity, Identity, Identity, Identity, Identity, Identity, Identity, Vector3.Zero, Vector3.Zero ),
		new( Enumerable.Repeat( new EffectorPoseInput( Identity, null, Identity, 0f, 0f ), 4 ).ToArray(),
			new bool[ 4 ], new bool[ 4 ], new FootReachInput[ 2 ], false, false, false, false, 0, false, false, 0f, 180f ),
		new( 1f, true, 1f, 1f ), new byte[ 3 ], 1f / 60f, 9.81f, false, false, false );

	private static LimbRigidTransform At( Vector3 position ) => Identity with { Translation = position };

	private static PhysicalRigController Create()
	{
		var math = new LimbSolverMath( new[] { -1f / 720f, 1f / 24f, -.5f },
			new[] { -1f / 5040f, 1f / 120f, -1f / 6f }, new[] { 0f, 0f, 0f, 0f, 0f, 0f, 0f, MathF.PI / 2f },
			2f / MathF.PI, MathF.PI / 2f, 0f, MathF.PI );
		var targets = new EffectorTargetSolver( math, new LimbArcSine( 0f, 0f, 0f, 1f, MathF.PI / 2f ),
			new EffectorTargetSettings( .0001f, .5f, .125f, .01f, .25f, -.6f, .6f ) );
		var arc = new LimbArcTangent( new[] { 1f / 17f, -1f / 15f, 1f / 13f, -1f / 11f, 1f / 9f, -1f / 7f, 1f / 5f, -1f / 3f }, MathF.PI / 2f, MathF.PI );
		var positions = new LimbPositionSolver( math, .0005f, MathF.PI / 180f, 180f / MathF.PI );
		return new( new EffectorRigController( new EffectorWeightController( .25f, .25f, Vector3.One, Vector3.One ),
			new EffectorPosePreparer( targets ), new EffectorPoseBlender( new LimbPoseBlender( math, arc, .00001f, .1f ) ),
			new FootReachController( new FootReachSettings( 0f, 0f, 0f, 0f, 0f, 0f, 1f, 0f, 1f, 1f, 0f ) ),
			new EffectorRigSolver( new LimbChainSolver( positions, new LimbRotationSolver( .0001f, .001f ), .0001f ) ), Vector3.One ) );
	}
}
