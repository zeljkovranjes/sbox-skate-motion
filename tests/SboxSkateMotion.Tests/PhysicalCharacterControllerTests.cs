using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class PhysicalCharacterControllerTests
{
	private static readonly LimbRigidTransform Identity = LimbRigidTransform.Identity;

	[Fact]
	public void SharedFrameOwnerUpdatesBeforeBoardTargetAndFeedback()
	{
		var state = State( true );
		state.Rig.Rig.Skeleton.MutableBones[ 0 ] = At( Vector3.UnitX );
		state.Output.UpdateFlags = 1UL << 50;
		state.BoardTarget.Body = state.BoardBodies[ 6 ].Body;
		Create().Update( state, Input( new( 2, 3, 4 ) ) );
		Assert.Equal( new Vector3( 3, 3, 4 ), state.RootFrame.Translation );
		Assert.Equal( state.RootFrame, state.Output.Frame );
		Assert.Equal( state.RootFrame, state.AuthoredFrame );
		Assert.Equal( state.RootFrame, state.BoardTarget.CachedPose );
		Assert.Equal( (1UL << 50) | 0x80000UL, state.Output.UpdateFlags );
		foreach ( var body in state.BoardBodies )
			Assert.Equal( Vector3.Zero, body.Body.Velocity );
	}

	[Fact]
	public void SeparateBoardSourceRetainsItsFrameAndSuppliesFeedback()
	{
		var state = State( false );
		state.BoardSource.Frame = At( new( -1, -2, -3 ) );
		Create().Update( state, Input( new( 2, 3, 4 ) ) );
		Assert.Equal( new Vector3( 2, 3, 4 ), state.Output.Frame.Translation );
		Assert.Equal( new Vector3( -1, -2, -3 ), state.AuthoredFrame.Translation );
		Assert.Equal( state.AuthoredFrame, state.Rig.Preparation.SecondaryTarget );
		foreach ( var body in state.BoardBodies )
			Assert.Equal( new Vector3( -1, -2, -3 ), body.Body.Velocity );
	}

	[Fact]
	public void PlacementUsesRetainedWeightBeforeResetAndClearsRelativeFrames()
	{
		var state = State( true );
		var weighted = new Vector3( .5f, .25f, -.125f );
		state.Rig.Cache.WeightedPosition = weighted;
		state.BoardOrientation = new( 0, 0, 0, 1 );
		state.BoardCorrectionFlag = 7;
		state.RelativeBodyFrame = At( new( 100, 200, 300 ) );
		var input = Input( new( 2, 3, 4 ) );
		Create().Update( state, input );
		Assert.Equal( input.ReferencePosition, state.Placement.ModelToWorld.Transform( weighted ) );
		Assert.Equal( 0, state.BoardCorrectionFlag );
		Assert.Equal( Identity, state.Placement.RelativeFrame );
		Assert.Equal( Identity, state.Placement.FirstHelperFrame );
		Assert.Equal( Identity, state.Placement.SecondHelperFrame );
		Assert.Equal( Identity, state.RelativeBodyFrame );
		Assert.Equal( 2, state.Rig.RootDrive.Mode );
	}

	private static PhysicalCharacterState State( bool sharedOwner )
	{
		var poses = new[] { Identity, Identity, At( -.8f * Vector3.UnitY ) };
		var skeleton = new EffectorRigSkeleton( Enumerable.Repeat( new EffectorBoneBinding( 2, -1, 10f ), 4 ).ToArray(),
			[ 0, 0, 1 ], Enumerable.Repeat( Identity, 3 ).ToArray(), poses, poses.ToArray() );
		var pose = new EffectorPoseState( new EffectorTargetState( 0, Vector3.Zero, Vector3.Zero,
			Vector3.Zero, Vector3.UnitY, 0f, 0f, false ), Identity, Identity, Identity, Identity, Identity, Identity, Vector3.Zero, false );
		var rig = new EffectorRigState( new EffectorWeightState[ 4 ], Enumerable.Repeat( pose, 4 ).ToArray(),
			new FootReachState[ 2 ], skeleton, default );
		var prep = new PhysicalPosePreparationState
		{
			ModelOffset = Identity, SecondaryTarget = Identity, FirstFeedbackFrame = Identity, SecondFeedbackFrame = Identity
		};
		var cache = new EffectorPoseCache( 3 );
		Array.Copy( poses, cache.Bones, 3 );
		var physical = new PhysicalRigState( rig, cache, prep, new( 5 ), Bindings( 4 ), Bindings( 5 ), [], [ default, default ] );
		var owner = new PhysicalFrameOwner { Frame = Identity };
		return new( physical, owner, sharedOwner ? owner : new() { Frame = Identity }, Bindings( 1 )[ 0 ], Bindings( 7 ) );
	}

	private static PhysicalBodyBinding[] Bindings( int count ) => Enumerable.Range( 0, count )
		.Select( _ => new PhysicalBodyBinding { CachedPose = Identity, Body = new() { Pose = Identity, InverseMass = 1f } } ).ToArray();

	private static PhysicalCharacterInput Input( Vector3 reference ) => new( Identity, Identity, reference,
		new PhysicalRigInput( new( Identity, Identity, Identity, Identity, Identity, Identity, Identity, Vector3.Zero, Vector3.Zero ),
			new( Enumerable.Repeat( new EffectorPoseInput( Identity, null, Identity, 0f, 0f ), 4 ).ToArray(),
				new bool[ 4 ], new bool[ 4 ], new FootReachInput[ 2 ], false, false, false, false, 0, false, false, 0f, 180f ),
			new( 1f, true, 1f, 1f ), new byte[ 3 ], 1f / 60f, 9.81f, false, false, false ), 1f, 1f );

	private static LimbRigidTransform At( Vector3 position ) => Identity with { Translation = position };

	private static PhysicalCharacterController Create()
	{
		var math = new LimbSolverMath( [ -1f / 720f, 1f / 24f, -.5f ],
			[ -1f / 5040f, 1f / 120f, -1f / 6f ], [ 0f, 0f, 0f, 0f, 0f, 0f, 0f, MathF.PI / 2f ],
			2f / MathF.PI, MathF.PI / 2f, 0f, MathF.PI );
		var targets = new EffectorTargetSolver( math, new LimbArcSine( 0f, 0f, 0f, 1f, MathF.PI / 2f ),
			new EffectorTargetSettings( .0001f, .5f, .125f, .01f, .25f, -.6f, .6f ) );
		var arc = new LimbArcTangent( [ 1f / 17f, -1f / 15f, 1f / 13f, -1f / 11f, 1f / 9f, -1f / 7f, 1f / 5f, -1f / 3f ], MathF.PI / 2f, MathF.PI );
		var positions = new LimbPositionSolver( math, .0005f, MathF.PI / 180f, 180f / MathF.PI );
		var rig = new PhysicalRigController( new EffectorRigController(
			new EffectorWeightController( .25f, .25f, Vector3.One, Vector3.One ), new EffectorPosePreparer( targets ),
			new EffectorPoseBlender( new LimbPoseBlender( math, arc, .00001f, .1f ) ),
			new FootReachController( new FootReachSettings( 0f, 0f, 0f, 0f, 0f, 0f, 1f, 0f, 1f, 1f, 0f ) ),
			new EffectorRigSolver( new LimbChainSolver( positions, new LimbRotationSolver( .0001f, .001f ), .0001f ) ), Vector3.One ) );
		var curve = new BoardCorrectionCurve( [ 0f, .5f, 1f, 1.5f, 2f, 2.5f, 3f, 3.5f ],
			[ 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f ] );
		var board = new BoardFrameCorrection( math, curve, curve,
			new( 2f * MathF.PI, 1f / (2f * MathF.PI), .95f, .996f, MathF.Sqrt( 2f ) ) );
		return new( rig, board );
	}
}
