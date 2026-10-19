using System;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using SkateMotion.Core.Animation;
using SkateMotion.Core.Constraints;
using SkateMotion.Core.Physics;
using Xunit;

namespace SboxSkateMotion.Tests;

public class PhysicalRigConstructionTests
{
	[Fact]
	public void LoadedConstructionBindsAllBodiesAndJointsAndReleasesOnlyItsMembership()
	{
		var rig = Construct( Definition() );
		Assert.Equal( 26, rig.Bodies.Bindings.Length );
		Assert.Equal( 4, rig.Targets.Bindings.Length );
		Assert.Equal( 22, rig.StructuralJoints.Length );
		var state = rig.CreateState( new( 24 ), new(), new( 26 ) );
		var bodies = rig.Bodies.Bindings.Concat( rig.Targets.Bindings ).Select( b => b.Body ).Reverse().ToArray();
		Assert.Equal( 30, bodies.Distinct().Count() );
		var world = new PhysicalSolverWorld( bodies, new BodyDynamicsSettings[ 30 ], [], 49, 23 );
		var unrelated = new PhysicalJointState();
		world.RegisterPoseAttachment( unrelated, false, 0, 1 );
		var structural = new PhysicalStructuralRigBinding( world, state.Bodies, rig.StructuralJoints );
		var drives = new PhysicalRigSolverBinding( world, state, rig.CanonicalJoints, rig.Hierarchy.InitialBones, rig.Hierarchy.Parents );
		Assert.Equal( 49, world.PoseAttachmentCount );
		Assert.Equal( 22, world.StructuralJointCount );
		for ( var i = 0; i < 22; ++i )
		{
			Assert.Same( rig.CanonicalJoints[ i + 1 ], state.Joints[ i ] );
			Assert.Equal( 2f, state.Joints[ i ].VelocityGain );
			Assert.Equal( .5f, state.Joints[ i ].PositionGain );
		}
		drives.Release();
		structural.Release();
		Assert.Equal( 1, world.PoseAttachmentCount );
		Assert.True( world.HasPoseAttachment( unrelated, false ) );
		Assert.Equal( 0, world.StructuralJointCount );
	}

	[Fact]
	public void EffectorResetMatchesCapturedDefaultsAndOwnsPoseStorage()
	{
		var definition = Definition();
		var first = Construct( definition );
		var second = Construct( definition );
		var rig = first.Effectors;
		Assert.Equal( default, rig.FootFlags );
		Assert.All( rig.Weights, w => Assert.Equal( default, w ) );
		Assert.All( rig.Feet, f => Assert.Equal( default, f ) );
		for ( var i = 0; i < 4; ++i )
		{
			var pose = rig.Poses[ i ];
			Assert.Equal( default, pose.TargetState );
			Assert.False( pose.InsideBounds );
			Assert.Equal( Vector3.Zero, pose.LocalPosition );
			Assert.All( new[] { pose.PinnedTarget, pose.PinnedReference, pose.AnimatedTarget,
				pose.AuthoredTarget, pose.AnimatedReference, pose.AuthoredReference }, p => Assert.Equal( LimbRigidTransform.Identity, p ) );
			Assert.Equal( i < 2 ? .8f : 1f, rig.Skeleton.Bindings[ i ].MaximumReach );
		}
		for ( var i = 0; i < 24; ++i )
			Assert.Equal( first.Hierarchy.InitialBones[ i ].Compose( first.Hierarchy.BindTransforms[ i ] ), rig.Skeleton.MutableBones[ i ] );
		rig.Skeleton.MutableBones[ 1 ] = default;
		rig.Skeleton.AnimatedBones[ 1 ] = default;
		Assert.NotEqual( default, first.Hierarchy.InitialBones[ 1 ] );
		Assert.NotEqual( default, second.Effectors.Skeleton.MutableBones[ 1 ] );
		Assert.NotSame( first.Bodies.Bindings[ 0 ].Body, second.Bodies.Bindings[ 0 ].Body );
	}

	[Fact]
	public void AuthoredResponseReachesTheBlendedDriveInsteadOfRemainingZero()
	{
		var rig = Construct( Definition() );
		var mask = Enumerable.Repeat( (byte)1, 24 ).ToArray();
		var joint = rig.OutputJoints[ 0 ];
		PhysicalPoseOutput.Update( rig.Effectors.Skeleton.MutableBones, new LimbRigidTransform[ 24 ],
			rig.OutputJoints, mask, new( .5f, true, 1f, 1f ), rig.Presets );
		Assert.Equal( 3, joint.Mode );
		Assert.Equal( joint.VelocityGain * joint.FirstStrength, joint.SecondConstraint.VelocityGain );
		Assert.Equal( joint.FirstStrength * (.5f * 3599.99951171875f), joint.SecondConstraint.PositionGain );
		Assert.True( joint.SecondConstraint.VelocityGain > 0f );
	}

	[Fact]
	public void MissingResponseRemainsAnExplicitConstructionFailure()
	{
		var definition = Definition();
		definition.PhysicalSettings.JointResponse = null;
		Assert.Throws<InvalidOperationException>( () => Construct( definition ) );
	}

	[Theory]
	[InlineData( float.NaN, 1f )]
	[InlineData( 1f, float.PositiveInfinity )]
	public void NonfiniteResponseFailsAssetLoading( float velocity, float position )
	{
		var definition = Definition();
		definition.PhysicalSettings.JointResponse = new( velocity, position );
		Assert.Throws<ArgumentException>( () => Construct( definition ) );
	}

	private static EffectorRigDefinition Definition()
	{
		using var document = JsonDocument.Parse( PhysicalJointTuningFixtures.Json );
		var definition = document.RootElement.GetProperty( "cases" )[ 0 ].GetProperty( "rig" ).Deserialize<EffectorRigDefinition>();
		definition.PhysicalSettings.BodyConstruction = new( .15f, .4f, 47.5f, .75f, 1.1f, .9f, 0, .7f, .001f );
		definition.PhysicalSettings.JointResponse = new( 2f, .5f );
		definition.PhysicalSettings.DrivePresets = [ new( 1f, 1f, 4f, 4f ), new( 1f, 1f, 4f, 4f ) ];
		return definition;
	}

	private static PhysicalRigConstruction Construct( EffectorRigDefinition definition )
	{
		var clips = new ClipSkeleton( Enumerable.Range( 0, 24 ).Reverse().Select( i => $"b{i}" ).Append( "helper" ).ToArray(),
			Enumerable.Range( 0, 25 ).Select( i => i is 0 or 23 ? -1 : i == 24 ? 0 : i - 1 ).ToArray(),
			new Vector3[ 25 ], Enumerable.Repeat( Quaternion.Identity, 25 ).ToArray() );
		var math = new LimbSolverMath( [ -.0013602249091491103f, .04165669530630112f, -.4999990165233612f ],
			[ -.0001950727018993348f, .00833207555115223f, -.16666652262210846f ], new float[ 8 ],
			.6366197466850281f, 1.570796251296997f, 7.549790126404332e-8f, 3.1415927410125732f );
		return new( new( definition, clips ), Enumerable.Repeat( LimbRigidTransform.Identity, 25 ).ToArray(), false, math, Vector3.Zero, 9 );
	}
}
