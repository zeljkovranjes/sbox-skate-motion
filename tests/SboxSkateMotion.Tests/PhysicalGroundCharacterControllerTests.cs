using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class PhysicalGroundCharacterControllerTests
{
	private static readonly LimbRigidTransform Identity = LimbRigidTransform.Identity;

	[Fact]
	public void GroundUpdateUsesThePhysicalDeckAndClearsOnlyItsOutputFlag()
	{
		var state = State( true );
		state.Output.UpdateFlags = ulong.MaxValue;
		state.Rig.Rig.Skeleton.MutableBones[ 0 ] = At( Vector3.UnitX );
		state.BoardBodies[ 6 ].Body.Pose = At( new( 2, 3, 4 ) );
		state.BoardBodies[ 6 ].Body.Velocity = new( 4, 0, 0 );
		state.BoardTarget.Body = state.BoardBodies[ 6 ].Body;
		state.BoardTarget.CachedPose = At( new( 20, 30, 40 ) );
		state.BoardCorrectionFlag = 7;
		state.BoardOrientation = new( 0, 0, 0, 1 );
		var placement = new PhysicalRiderFrameState();
		var controller = Create();
		controller.Update( state, placement, Input() );
		Assert.Equal( new Vector3( 4, 3, 4 ), state.Output.Frame.Translation );
		Assert.Equal( new Vector3( 2, 3, 4 ), state.AuthoredFrame.Translation );
		Assert.Equal( state.AuthoredFrame, state.Rig.Preparation.SecondaryTarget );
		Assert.Equal( ulong.MaxValue & ~0x80000UL, state.Output.UpdateFlags );
		Assert.Equal( new Vector3( 20, 30, 40 ), state.BoardTarget.CachedPose.Translation );
		Assert.Equal( new Vector3( 2, 3, 4 ), state.BoardTarget.Body.Pose.Translation );
		Assert.Equal( new Vector3( 4, 0, 0 ), state.BoardBodies[ 6 ].Body.Velocity );
		Assert.Equal( (byte)7, state.BoardCorrectionFlag );
		Assert.Equal( new Vector4( 0, 0, 0, 1 ), state.BoardOrientation );
		controller.Update( state, placement, Input() );
		Assert.Equal( new Vector3( 4, 3, 4 ), state.Output.Frame.Translation );
		Assert.Equal( new Vector3( 2, 3, 4 ), placement.PreviousBoardPosition );
	}

	[Fact]
	public void GroundHeadingRefreshesEveryUpdateWithoutAnExternalRequest()
	{
		var state = State( true );
		var placement = new PhysicalRiderFrameState { LocalFrame = At( Vector3.One ) };
		var controller = Create();
		controller.Update( state, placement, Input() );
		Assert.Equal( Identity, placement.LocalFrame );
		Assert.False( placement.ResetHeading );
		placement.LocalFrame = At( new( 4, 5, 6 ) );
		placement.RootFrame = new( new( Vector3.UnitX, Vector3.UnitZ, -Vector3.UnitY ), Vector3.Zero );
		controller.Update( state, placement, Input() );
		Assert.Equal( Identity, placement.LocalFrame );
		Assert.False( placement.ResetHeading );
	}

	[Fact]
	public void OffsetDeckDrivesFeedbackIndependentlyOfThePublishedRoot()
	{
		var state = State( false );
		state.BoardSource.Frame = At( new( 40, 50, 60 ) );
		state.BoardBodies[ 6 ].Body.Pose = At( new( 2, 3, 4 ) );
		state.BoardBodies[ 6 ].LocalOffset = At( Vector3.UnitX );
		state.Rig.Reset.WorldReference = new( 5, 6, 7 );
		state.RelativeBodyFrame = At( Vector3.One );
		var placement = new PhysicalRiderFrameState { Anchor = new( 10, 20, 30 ), PreserveAnchor = true };
		Create().Update( state, placement, Input() );
		Assert.Equal( new Vector3( 10, 20, 30 ), state.Output.Frame.Translation );
		Assert.Equal( new Vector3( 3, 3, 4 ), state.AuthoredFrame.Translation );
		Assert.Equal( new Vector3( 40, 50, 60 ), state.BoardSource.Frame.Translation );
		Assert.Equal( new Vector3( 5, 6, 7 ), state.Rig.Preparation.SecondFeedbackFrame.Translation );
		Assert.Equal( new Vector3( 7, 17, 26 ), state.Rig.Preparation.ComposedContact.Translation );
		Assert.Equal( Identity, state.RelativeBodyFrame );
		Assert.False( placement.PreserveAnchor );
	}

	[Fact]
	public void CachedDeckIsUsedWhenAnAnchorIsExplicitlyPreserved()
	{
		var state = State( true );
		state.BoardBodies[ 6 ].Body = null;
		state.BoardBodies[ 6 ].CachedPose = At( new( 3, 4, 5 ) );
		var placement = new PhysicalRiderFrameState { Anchor = new( 7, 8, 9 ), PreserveAnchor = true };
		Create().Update( state, placement, Input() );
		Assert.Equal( new Vector3( 7, 8, 9 ), state.Output.Frame.Translation );
		Assert.Equal( new Vector3( 3, 4, 5 ), state.AuthoredFrame.Translation );
		Assert.Equal( state.AuthoredFrame, state.Rig.Preparation.SecondaryTarget );
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

	private static PhysicalGroundCharacterInput Input( float placementDelta = .25f ) => new( Identity,
		new PhysicalRigInput( new( Identity, Identity, Identity, Identity, Identity, Identity, Identity, Vector3.Zero, Vector3.Zero ),
			new( Enumerable.Repeat( new EffectorPoseInput( Identity, null, Identity, 0f, 0f ), 4 ).ToArray(),
				new bool[ 4 ], new bool[ 4 ], new FootReachInput[ 2 ], false, false, false, false, 0, false, false, 0f, 180f ),
			new( 1f, true, 1f, 1f ), new byte[ 3 ], 1f / 60f, 9.81f, false, false, false ), placementDelta );

	private static LimbRigidTransform At( Vector3 position ) => Identity with { Translation = position };

	private static PhysicalGroundCharacterController Create()
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
		return new( new PhysicalRiderFrameController( math,
			new LimbSignedAngle( math, 1e-8f, MathF.PI * 2f, 1f / (MathF.PI * 2f) ) ), rig );
	}
}
