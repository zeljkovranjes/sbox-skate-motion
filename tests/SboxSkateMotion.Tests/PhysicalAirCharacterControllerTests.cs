using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class PhysicalAirCharacterControllerTests
{
	private static readonly LimbRigidTransform Identity = LimbRigidTransform.Identity;

	[Fact]
	public void AirUpdateUsesRetainedWeightedOffsetBeforeResettingTheRig()
	{
		var state = State( true );
		state.Rig.Cache.WeightedPosition = new( 2, 3, 4 );
		state.Rig.Rig.Skeleton.MutableBones[ 0 ] = At( Vector3.UnitX );
		state.BoardBodies[ 6 ].Body.Pose = At( new( 2, 3, 4 ) );
		state.Output.UpdateFlags = 0x20;
		state.RelativeBodyFrame = At( Vector3.One );
		var placement = new PhysicalRiderFrameState();
		Create().Update( state, placement, Input() );
		Assert.Equal( new Vector3( 8, 17, 26 ), placement.ModelToWorld.Translation );
		Assert.Equal( new Vector3( 9, 17, 26 ), state.Output.Frame.Translation );
		Assert.Equal( 0x80020UL, state.Output.UpdateFlags );
		Assert.Equal( state.Output.Frame, state.BoardTarget.CachedPose );
		Assert.Equal( state.AuthoredFrame, state.Rig.Preparation.SecondaryTarget );
		Assert.Equal( new Vector3( 28, 56, 88 ), state.BoardBodies[ 6 ].Body.Velocity );
		Assert.Equal( At( Vector3.One ), state.RelativeBodyFrame );
		Assert.Equal( state.AuthoredFrame.Translation, placement.Anchor );
		Assert.True( placement.PreserveAnchor );
	}

	[Fact]
	public void SeparateBoardSourceControlsFeedbackAndPendingAnchor()
	{
		var state = State( false );
		state.BoardSource.Frame = At( new( 40, 50, 60 ) );
		state.BoardBodies[ 6 ].Body.Pose = At( new( 2, 3, 4 ) );
		var placement = new PhysicalRiderFrameState { Anchor = new( -1, -2, -3 ), PreserveAnchor = true };
		Create().Update( state, placement, Input() );
		Assert.Equal( new Vector3( 10, 20, 30 ), state.Output.Frame.Translation );
		Assert.Equal( new Vector3( 40, 50, 60 ), state.AuthoredFrame.Translation );
		Assert.Equal( state.AuthoredFrame, state.Rig.Preparation.SecondaryTarget );
		Assert.Equal( state.AuthoredFrame.Translation, placement.Anchor );
		Assert.True( placement.PreserveAnchor );
		foreach ( var body in state.BoardBodies )
			Assert.Equal( new Vector3( 152, 188, 224 ), body.Body.Velocity );
	}

	[Fact]
	public void SharedDeckTargetMovesBeforeTheVelocityFeedbackReadsItsPosition()
	{
		var state = State( true );
		state.BoardTarget.Body = state.BoardBodies[ 6 ].Body;
		state.BoardBodies[ 6 ].Body.Pose = At( new( -4, -5, -6 ) );
		Create().Update( state, new(), Input() );
		Assert.Equal( new Vector3( 10, 20, 30 ), state.BoardBodies[ 6 ].Body.Pose.Translation );
		foreach ( var body in state.BoardBodies )
			Assert.Equal( Vector3.Zero, body.Body.Velocity );
	}

	[Fact]
	public void PendingAirAnchorIsConsumedOnceByTheNextDeckPlacement()
	{
		var state = State( false );
		state.BoardSource.Frame = At( new( 40, 50, 60 ) );
		var placement = new PhysicalRiderFrameState();
		Create().Update( state, placement, Input() );
		var math = new LimbSolverMath( [ -1f / 720f, 1f / 24f, -.5f ],
			[ -1f / 5040f, 1f / 120f, -1f / 6f ], [ 0, 0, 0, 0, 0, 0, 0, MathF.PI / 2f ],
			2f / MathF.PI, MathF.PI / 2f, 0f, MathF.PI );
		var riding = new PhysicalRiderFrameController( math,
			new LimbSignedAngle( math, 1e-8f, MathF.PI * 2f, 1f / (MathF.PI * 2f) ) );
		var deck = state.BoardBodies[ 6 ];
		deck.Body.Pose = At( new( 100, 0, 0 ) );
		deck.Body.Velocity = new( 4, 0, 0 );
		riding.Update( placement, deck, Identity, .25f );
		Assert.Equal( new Vector3( 40, 50, 60 ), placement.Anchor );
		Assert.False( placement.PreserveAnchor );
		riding.Update( placement, deck, Identity, .25f );
		Assert.Equal( new Vector3( 101, 0, 0 ), placement.Anchor );
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

	private static PhysicalAirCharacterInput Input() => new( Identity, new Vector3( 10, 20, 30 ),
		new PhysicalRigInput( new( Identity, Identity, Identity, Identity, Identity, Identity, Identity, Vector3.Zero, Vector3.Zero ),
			new( Enumerable.Repeat( new EffectorPoseInput( Identity, null, Identity, 0f, 0f ), 4 ).ToArray(),
				new bool[ 4 ], new bool[ 4 ], new FootReachInput[ 2 ], false, false, false, false, 0, false, false, 0f, 180f ),
			new( 1f, true, 1f, 1f ), new byte[ 3 ], 1f / 60f, 9.81f, false, false, false ), false, true, 12, .25f, 1f );

	private static LimbRigidTransform At( Vector3 position ) => Identity with { Translation = position };

	private static PhysicalAirCharacterController Create()
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
		return new( new PhysicalAirFrameController( math ), rig, board );
	}
}
