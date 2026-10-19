using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class PhysicalOffboardCharacterControllerTests
{
	private static readonly LimbRigidTransform Identity = LimbRigidTransform.Identity;

	[Fact]
	public void FeedbackAndSecondaryTargetUseThePreviousFrameBeforeAdoption()
	{
		var character = State( true );
		var previous = new LimbRigidTransform( new( Vector3.UnitZ, Vector3.UnitY, -Vector3.UnitX ), new( 2, 3, 4 ) );
		character.Rig.Preparation.ModelToWorld = previous;
		character.Rig.Cache.Bones[ 0 ] = At( Vector3.UnitX );
		character.AuthoredFrame = At( new( 9, 8, 7 ) );
		var point = new Vector3( 8, 9, 10 );
		Create().Update( new(), character, new(), Direction(), Input( point, 0 ) );
		Assert.Equal( previous.Compose( At( Vector3.UnitX ) ), character.Rig.Preparation.SecondaryTarget );
		Assert.Equal( previous.Rotation, character.Rig.Preparation.SecondFeedbackFrame.Rotation );
		Assert.Equal( point, character.Rig.Preparation.SecondFeedbackFrame.Translation );
		Assert.Equal( .04f, character.Rig.Preparation.FeedbackHeight );
		Assert.Equal( new Vector3( 4, 2, 7 ), character.Rig.Preparation.ModelToWorld.Translation );
		Assert.Equal( At( new( 9, 8, 7 ) ), character.AuthoredFrame );
		Assert.Equal( Identity, character.RelativeBodyFrame );
	}

	[Fact]
	public void RetainedRootPublishesBeforeSharedBoardSourceIsRead()
	{
		var character = State( true );
		character.Rig.Preparation.ModelToWorld = Identity;
		character.Rig.Rig.Skeleton.MutableBones[ 0 ] = At( new( 99, 99, 99 ) );
		var state = new PhysicalOffboardCharacterState { LocalRootFrame = At( new( 1, 2, 3 ) ) };
		character.Output.UpdateFlags = 0x20;
		Create().Update( state, character, new(), Direction(), Input( new( 8, 9, 10 ), 2 ) );
		Assert.Equal( new Vector3( 5, 4, 10 ), character.Output.Frame.Translation );
		Assert.Equal( character.Output.Frame, character.BoardTarget.CachedPose );
		Assert.Equal( character.Output.Frame, character.AuthoredFrame );
		Assert.Equal( 0x80020UL, character.Output.UpdateFlags );
		Assert.Equal( At( new( 1, 2, 3 ) ), state.LocalRootFrame );
		Assert.Equal( Identity, character.Rig.Preparation.SecondaryTarget );
	}

	private static PhysicalDirectionState Direction() => new()
	{
		Direction = Vector3.UnitY, SourceFrame = Identity, WorldFrame = Identity,
		LocalFrame = Identity, FilteredFrame = Identity
	};

	private static PhysicalOffboardCharacterInput Input( Vector3 feedback, ulong rigFlags ) => new(
		new( Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, new( 4, 2, 7 ), Vector3.Zero ), feedback,
		0, rigFlags, 0, .25f,
		new PhysicalRigInput( new( Identity, Identity, Identity, Identity, Identity, Identity, Identity, Vector3.Zero, Vector3.Zero ),
			new( Enumerable.Repeat( new EffectorPoseInput( Identity, null, Identity, 0f, 0f ), 4 ).ToArray(),
				new bool[ 4 ], new bool[ 4 ], new FootReachInput[ 2 ], false, false, false, false, 0, false, false, 0f, 180f ),
			new( 1f, true, 1f, 1f ), new byte[ 3 ], 1f / 60f, 9.81f, false, false, false ) );

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

	private static LimbRigidTransform At( Vector3 position ) => Identity with { Translation = position };

	private static PhysicalOffboardCharacterController Create()
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
		var alternate = new BoardCorrectionCurve( [ 0f, .5f, 1f, 1.5f, 2f, 2.5f, 3f, 3.5f ], new float[ 8 ] );
		var board = new BoardFrameCorrection( math, curve, alternate,
			new( 2f * MathF.PI, 1f / (2f * MathF.PI), .95f, .996f, MathF.Sqrt( 2f ) ) );
		var direction = new PhysicalDirectionController( math,
			new LimbSignedAngle( math, 1e-8f, MathF.PI * 2f, 1f / (MathF.PI * 2f) ),
			new LimbArcSine( 0f, 0f, 0f, 1f, MathF.PI / 2f ), curve, curve, new( .2f, .05f, .1f, .5f ) );
		return new( new PhysicalLocomotionFrameAdoption( math ), rig, board, direction );
	}
}
