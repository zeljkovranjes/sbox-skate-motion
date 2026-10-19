using System.Numerics;
using System.Text.Json;
using SkateMotion.Core.Animation;
using SkateMotion.Core.Constraints;
using SkateMotion.Core.Physics;
using Xunit;

namespace SboxSkateMotion.Tests;

public class PhysicalRigFeedbackTests
{
	public static IEnumerable<object[]> Resets => Enumerable.Range( 0, 5 ).Select( i => new object[] { i } );
	public static IEnumerable<object[]> Modes => Enumerable.Range( 0, 36 ).Select( i => new object[] { i } );

	[Theory]
	[MemberData( nameof( Resets ) )]
	public void ResetMatchesEveryCapturedSemanticByteAndPreservesAuthoredFlags( int index )
	{
		using var document = JsonDocument.Parse( PhysicalRigFeedbackFixtures.Json );
		var sample = document.RootElement.GetProperty( "cases" )[ index ];
		var before = Convert.FromHexString( sample.GetProperty( "before" ).GetString()! );
		var state = PhysicalRigFeedbackCodec.Read( before );
		Assert.Equal( before, PhysicalRigFeedbackCodec.Write( state, before ) );
		var flags = state.AuthoredFlags.ToArray();
		state.Reset();
		AssertBytes( sample, state, before );
		Assert.Equal( flags, state.AuthoredFlags );
		Assert.All( state.GroupIndices, i => Assert.Equal( -1, i ) );
		Assert.Equal( Vector4.UnitY, state.ReferencePosition );
	}

	[Theory]
	[MemberData( nameof( Modes ) )]
	public void CompleteModeOwnerMatchesCapturedFeedbackBodiesAndJointRanges( int index )
	{
		using var actorDocument = JsonDocument.Parse( PhysicalRigFeedbackFixtures.Json );
		using var modeDocument = JsonDocument.Parse( PhysicalRigModeFixtures.Json );
		var sample = actorDocument.RootElement.GetProperty( "cases" )[ index + 5 ];
		var mode = modeDocument.RootElement.GetProperty( "cases" )[ index ];
		var before = Convert.FromHexString( sample.GetProperty( "before" ).GetString()! );
		var feedback = PhysicalRigFeedbackCodec.Read( before );
		var setup = Create( mode, feedback );
		Apply( setup.Controller, mode.GetProperty( "profile" ) );
		Assert.Same( feedback, setup.Rig.FeedbackState );
		AssertBytes( sample, feedback, before );
		for ( var i = 0; i < 26; ++i )
		{
			var expected = mode.GetProperty( "outputs" )[ i ];
			var body = setup.Bodies.Bodies[ i ];
			Assert.Equal( V( expected.GetProperty( "properties" ) ), body.Body.Mass.Value.InverseInertia );
			Assert.Equal( expected.GetProperty( "properties" )[ 4 ].GetSingle(), body.Body.Mass.Value.InverseMass );
			Assert.Equal( V( expected.GetProperty( "worldDiagonal" ) ), body.Body.InverseInertiaDiagonal );
			Assert.Equal( V( expected.GetProperty( "worldOffDiagonal" ) ), body.Body.InverseInertiaOffDiagonal );
			Assert.Equal( expected.GetProperty( "shapeFlags" ).GetUInt32(), body.ShapeFlags );
		}
		for ( var i = 0; i < setup.Controller.Joints.Length; ++i )
		{
			var expected = mode.GetProperty( "joints" )[ i ].GetProperty( "expected" );
			var limits = setup.Controller.Joints[ i ].Limits;
			Assert.Equal( expected[ 0 ].GetSingle(), limits.SwingAngle );
			Assert.Equal( expected[ 1 ].GetSingle(), limits.TwistAngle );
			Assert.Equal( expected[ 2 ].GetSingle(), limits.SwingCosine );
			Assert.Equal( expected[ 3 ].GetSingle(), limits.TwistCosine );
		}
		Assert.Equal( setup.Controller.Joints.Length, setup.World.StructuralJointCount );
		setup.World.Prepare();
		var compiled = new JointConstraint[ setup.Controller.Joints.Length ];
		Assert.Equal( compiled.Length, setup.World.CompileStructuralJoints( compiled, .016666668f ) );
		for ( var i = 0; i < compiled.Length; ++i )
		{
			var joint = setup.Controller.Joints[ i ];
			Assert.Equal( setup.World.CompileStructuralJoint( joint, joint.ChildBone, joint.ParentBone, .016666668f ), compiled[ i ] );
		}
	}

	[Fact]
	public void PreparedWorldRejectsModeMutationBeforeChangingFeedbackBodiesOrJoints()
	{
		using var document = JsonDocument.Parse( PhysicalRigModeFixtures.Json );
		var setup = Create( document.RootElement.GetProperty( "cases" )[ 0 ], new() { Blend = .75f } );
		var mass = setup.Bodies.Bodies[ 1 ].Body.Mass;
		var joint = setup.Controller.Joints[ 0 ];
		setup.World.Prepare();
		Assert.Throws<InvalidOperationException>( () => setup.Controller.ApplyPrimary( false ) );
		Assert.Equal( .75f, setup.Rig.FeedbackState.Blend );
		Assert.Equal( mass, setup.Bodies.Bodies[ 1 ].Body.Mass );
		Assert.Equal( joint, setup.Controller.Joints[ 0 ] );
	}

	[Fact]
	public void ReleaseDetachesTheFeedbackOwnerAndAllStructuralJoints()
	{
		using var document = JsonDocument.Parse( PhysicalRigModeFixtures.Json );
		var setup = Create( document.RootElement.GetProperty( "cases" )[ 0 ], new() );
		setup.Controller.Release();
		setup.Controller.Release();
		Assert.Null( setup.Rig.FeedbackState );
		Assert.Equal( 0, setup.World.StructuralJointCount );
		Assert.Throws<InvalidOperationException>( () => setup.Controller.ApplyPrimary( true ) );
	}

	[Fact]
	public void BoundInputUsesSharedBlendAndReferenceWithoutChangingOtherCallerSettings()
	{
		var state = new PhysicalRigFeedbackState { Blend = .35f, ReferencePosition = new( 2f, 3f, 4f, 5f ) };
		var input = new PhysicalRigInput( default, default, new( .9f, true, 7f, 8f ), new byte[ 24 ], .02f, 9.8f, false, true, true );
		var bound = state.BindInput( input );
		Assert.Equal( .35f, bound.JointSettings.Blend );
		Assert.Equal( new Vector3( 2f, 3f, 4f ), bound.Frames.ReferencePosition );
		Assert.Equal( input, bound with { JointSettings = input.JointSettings, Frames = input.Frames } );
	}

	[Fact]
	public void RigUpdateConsumesTheResetBlendInsteadOfThePreviousCallerSnapshot()
	{
		using var document = JsonDocument.Parse( PhysicalRigModeFixtures.Json );
		var setup = Create( document.RootElement.GetProperty( "cases" )[ 0 ], new() { Blend = .8f } );
		setup.Controller.ApplyPrimary( true );
		var identity = LimbRigidTransform.Identity;
		var drive = new PhysicalJointState { ChildBone = 1 };
		var preparation = new PhysicalPosePreparationState
		{
			ModelOffset = identity, SecondaryTarget = identity, FirstFeedbackFrame = identity, SecondFeedbackFrame = identity
		};
		var state = new PhysicalRigState( setup.Rig.Rig, setup.Rig.Cache, preparation, setup.Rig.Reset,
			Enumerable.Range( 0, 4 ).Select( _ => new PhysicalBodyBinding { CachedPose = identity } ).ToArray(),
			setup.Rig.Bodies, [ drive ], setup.Rig.Presets ) { FeedbackState = setup.Rig.FeedbackState };
		var input = new PhysicalRigInput( new( identity, identity, identity, identity, identity, identity, identity, Vector3.Zero, Vector3.Zero ),
			default, new( 1f, true, 1f, 1f ), [ 0, 1 ], .016666668f, 0f, false, false, false );
		var updater = RigUpdater();
		updater.Update( state, input );
		Assert.Equal( 3, drive.Mode );
		state.FeedbackState.Blend = 1.5f;
		updater.Update( state, input );
		Assert.Equal( 0, drive.Mode );
	}

	private static PhysicalRigController RigUpdater()
	{
		var math = new LimbSolverMath( [ -1f / 720f, 1f / 24f, -.5f ],
			[ -1f / 5040f, 1f / 120f, -1f / 6f ], [ 0f, 0f, 0f, 0f, 0f, 0f, 0f, MathF.PI / 2f ],
			2f / MathF.PI, MathF.PI / 2f, 0f, MathF.PI );
		var targets = new EffectorTargetSolver( math, new LimbArcSine( 0f, 0f, 0f, 1f, MathF.PI / 2f ),
			new EffectorTargetSettings( .0001f, .5f, .125f, .01f, .25f, -.6f, .6f ) );
		var arc = new LimbArcTangent( [ 1f / 17f, -1f / 15f, 1f / 13f, -1f / 11f, 1f / 9f, -1f / 7f, 1f / 5f, -1f / 3f ], MathF.PI / 2f, MathF.PI );
		var positions = new LimbPositionSolver( math, .0005f, MathF.PI / 180f, 180f / MathF.PI );
		return new( new EffectorRigController( new EffectorWeightController( .25f, .25f, Vector3.One, Vector3.One ),
			new EffectorPosePreparer( targets ), new EffectorPoseBlender( new LimbPoseBlender( math, arc, .00001f, .1f ) ),
			new FootReachController( new FootReachSettings( 0f, 0f, 0f, 0f, 0f, 0f, 1f, 0f, 1f, 1f, 0f ) ),
			new EffectorRigSolver( new LimbChainSolver( positions, new LimbRotationSolver( .0001f, .001f ), .0001f ) ), Vector3.One ) );
	}

	private static void AssertBytes( JsonElement sample, PhysicalRigFeedbackState state, byte[] before )
	{
		var expected = Convert.FromHexString( sample.GetProperty( "after" ).GetString()! );
		var actual = PhysicalRigFeedbackCodec.Write( state, before );
		for ( var i = 0; i < actual.Length; ++i )
			if ( !PhysicalRigFeedbackCodec.IsPadding( i ) )
				Assert.True( expected[ i ] == actual[ i ], $"Feedback byte {i:x}: {expected[ i ]:x2} != {actual[ i ]:x2}" );
	}

	private static (PhysicalRigModeController Controller, PhysicalRigState Rig, PhysicalRigBodyModes Bodies, PhysicalSolverWorld World)
		Create( JsonElement mode, PhysicalRigFeedbackState feedback )
	{
		var bodies = new PhysicalRigModeBody[ 26 ];
		var bindings = new PhysicalBodyBinding[ 26 ];
		var settings = new BodyDynamicsSettings[ 26 ];
		for ( var i = 0; i < 26; ++i )
		{
			var data = mode.GetProperty( "inputs" )[ i ];
			var pose = data.GetProperty( "pose" );
			PhysicalBodyMass Mass( string prefix ) => new( V( data.GetProperty( prefix + "Inertia" ) ), data.GetProperty( prefix + "InverseMass" ).GetSingle() );
			var body = new PhysicalBodyState
			{
				Pose = new( new( V( pose[ 0 ] ), V( pose[ 1 ] ), V( pose[ 2 ] ) ), V( pose[ 3 ] ) ),
				Orientation = Vector4.UnitW, Mass = Mass( "initial" ), MotionMode = 4,
				InverseInertiaDiagonal = new( 9f, 4f, 5f ), InverseInertiaOffDiagonal = new( 8f, 7f, 3f ), InverseMass = 6f
			};
			bindings[ i ] = new() { Body = body, CachedPose = body.Pose };
			bodies[ i ] = new( body, Mass( "primary" ), Mass( "alternate" ) )
			{
				MaximumInertia = .33f, LinearDamping = 15f, AngularDamping = 16f, ShapeFlags = (uint)(0x31 + i),
				ShapeMode = (uint)(0x41 + i), CollisionCounter = (uint)(i % 3), ContactPropertySlot = 3
			};
			settings[ i ] = new( Vector3.One, 2f, 3f, 100f, 100f, 5f, 0f, 0 );
		}
		var bodyModes = new PhysicalRigBodyModes( bodies, [ new( .5f, 8f, 30f ), new( .6f, 9f, 31f ), new( .7f, 10f, 32f ) ], false );
		var poses = Enumerable.Repeat( LimbRigidTransform.Identity, 24 ).ToArray();
		var skeleton = new EffectorRigSkeleton( new EffectorBoneBinding[ 4 ], new int[ 24 ], poses, poses, poses );
		var rig = new PhysicalRigState( new( new EffectorWeightState[ 4 ], new EffectorPoseState[ 4 ], new FootReachState[ 2 ], skeleton, default ),
			new( 24 ), new(), new( 26 ), new PhysicalBodyBinding[ 4 ], bindings, [], [ default, default ] );
		var bones = new PhysicalStructuralBone[ 24 ];
		var joints = new PhysicalStructuralJoint[ mode.GetProperty( "joints" ).GetArrayLength() ];
		var scales = new PhysicalJointRangeScales[ joints.Length ];
		for ( var i = 0; i < joints.Length; ++i )
		{
			var data = mode.GetProperty( "joints" )[ i ];
			var child = data.GetProperty( "child" ).GetInt32();
			var authored = data.GetProperty( "authored" );
			var scale = data.GetProperty( "scales" );
			bones[ child ] = new( Vector4.UnitW, Vector4.UnitW, Vector4.UnitW, Vector3.Zero, authored[ 0 ].GetSingle(), authored[ 1 ].GetSingle() );
			joints[ i ] = new( data.GetProperty( "parent" ).GetInt32(), child,
				new( Vector4.UnitW, Vector3.Zero, Vector4.UnitW, Vector3.Zero ), Vector4.UnitW,
				new( Vector4.One, 2f, 3f, 0f, 0f, 1f, 1f, 4, 2 ) );
			scales[ i ] = new( scale[ 0 ].GetSingle(), scale[ 1 ].GetSingle(), scale[ 2 ].GetSingle(), scale[ 3 ].GetSingle() );
		}
		var world = new PhysicalSolverWorld( bodies.Select( b => b.Body ).ToArray(), settings, Enumerable.Range( 0, 26 ).ToArray(), 0, joints.Length );
		var math = new LimbSolverMath( [ -.0013602249091491103f, .04165669530630112f, -.4999990165233612f ],
			[ -.0001950727018993348f, .00833207555115223f, -.16666652262210846f ], new float[ 8 ],
			.6366197466850281f, 1.570796251296997f, 7.549790126404332e-8f, 3.1415927410125732f );
		var controller = new PhysicalRigModeController( world, rig, bodyModes, feedback, bones, joints, scales,
			Enumerable.Range( 0, 24 ).Select( i => (byte)(i % 2) ).ToArray(),
			Enumerable.Range( 0, 24 ).Select( i => (float)(.04 + i * .017) ).ToArray(), math );
		return (controller, rig, bodyModes, world);
	}

	private static void Apply( PhysicalRigModeController controller, JsonElement profile )
	{
		bool Flag( string name ) => profile.GetProperty( name ).GetBoolean();
		if ( Flag( "secondary" ) )
			controller.ApplySecondary( Flag( "primaryLimits" ), new( Flag( "alternateMass" ), Flag( "alternateInertia" ),
				Flag( "specialFriction" ), .37f, .19f, .41f, .27f ), 1.3f, .7f );
		else
			controller.ApplyPrimary( Flag( "primaryCollision" ) );
	}

	private static Vector3 V( JsonElement data ) => new( data[ 0 ].GetSingle(), data[ 1 ].GetSingle(), data[ 2 ].GetSingle() );
}
