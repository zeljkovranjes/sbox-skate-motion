using System;
using System.Numerics;
using SkateMotion.Core.Animation;
using SkateMotion.Core.Constraints;
using SkateMotion.Core.Physics;
using Xunit;

namespace SboxSkateMotion.Tests;

public class PhysicalSolverWorldTests
{
	[Fact]
	public void ConnectedBodiesExchangeBothReactionsBeforeAnyPoseIsIntegrated()
	{
		var bodies = new[] { Body( -.1f ), Body( 0f ), Body( .1f ) };
		var world = World( bodies, [ 0, 1, 2 ] );
		world.Prepare();
		var drives = Chain( world );
		world.SolveAndIntegrate( [], [], drives, 1, .1f, Vector3.Zero );
		Near( -.05f, bodies[ 0 ].Pose.Translation.X );
		Near( .025f, bodies[ 1 ].Pose.Translation.X );
		Near( .025f, bodies[ 2 ].Pose.Translation.X );
		Near( .5f, bodies[ 0 ].Velocity.X );
		Near( .25f, bodies[ 1 ].Velocity.X );
		Near( -.75f, bodies[ 2 ].Velocity.X );
		Near( 0f, (bodies[ 0 ].Velocity + bodies[ 1 ].Velocity + bodies[ 2 ].Velocity).Length() );
		Assert.Equal( bodies[ 1 ].Pose.Translation, world.Body( 1 ).Position );
	}

	[Fact]
	public void NonRespondingEndpointRetainsMassAndIsNotWrittenThroughTheIntegrationList()
	{
		var bodies = new[] { Body( -.1f ), Body( 0f ), Body( .1f ) };
		bodies[ 2 ].MotionMode = 2;
		bodies[ 2 ].AccumulatorState = 7;
		var world = World( bodies, [ 0, 1 ] );
		world.Prepare();
		var drives = Chain( world );
		world.SolveAndIntegrate( [], [], drives, 1, .1f, Vector3.Zero );
		Near( -.05f, bodies[ 0 ].Pose.Translation.X );
		Near( .1f, bodies[ 1 ].Pose.Translation.X );
		Assert.Equal( .1f, bodies[ 2 ].Pose.Translation.X );
		Assert.Equal( Vector3.Zero, bodies[ 2 ].Velocity );
		Assert.Equal( 1f, bodies[ 2 ].InverseMass );
		Assert.Equal( Vector3.One, bodies[ 2 ].InverseInertiaDiagonal );
		Assert.Equal( 7, bodies[ 2 ].AccumulatorState );
		Assert.Equal( 0f, world.Body( 2 ).InverseMass );
	}

	[Fact]
	public void IntegrationPreservesBodyOwnershipAndCarriesTheQuietCounterBetweenFrames()
	{
		var body = Body( 0f );
		body.AccumulatorState = 2;
		body.MotionMetric = 1f;
		body.DefaultLinearAccumulator = new( 1f, 2f, 3f );
		var mass = new PhysicalBodyMass( Vector3.One, 1f );
		body.Mass = mass;
		var world = new PhysicalSolverWorld( [ body ],
			[ new( Vector3.One, 0f, 0f, 100f, 100f, 1f, 1f, 4 ) ], [ 0 ] );
		for ( var i = 0; i < 3; ++i )
		{
			world.Prepare();
			world.SolveAndIntegrate( [], [], [], 1, .1f, Vector3.Zero );
		}
		Assert.Equal( 4, body.AccumulatorState );
		Assert.Equal( 4, body.MotionMode );
		Assert.Equal( mass, body.Mass );
		Assert.Equal( new Vector3( 1f, 2f, 3f ), body.DefaultLinearAccumulator );
		Assert.Equal( Vector4.UnitW, body.Orientation );
	}

	[Fact]
	public void ASharedBodyCannotBeRegisteredOrIntegratedTwice()
	{
		var body = Body( 0f );
		Assert.Throws<ArgumentException>( () => World( [ body, body ], [ 0 ] ) );
		Assert.Throws<ArgumentException>( () => World( [ body ], [ 0, 0 ] ) );
		Assert.Throws<ArgumentOutOfRangeException>( () => World( [ body ], [ 1 ] ) );
	}

	[Fact]
	public void EachStepRequiresFreshPhysicalInputs()
	{
		var body = Body( 0f );
		var world = World( [ body ], [ 0 ] );
		Assert.Throws<InvalidOperationException>( () => world.SolveAndIntegrate( [], [], [], 1, .1f, Vector3.Zero ) );
		world.Prepare();
		world.SolveAndIntegrate( [], [], [], 1, .1f, Vector3.Zero );
		Assert.Throws<InvalidOperationException>( () => world.SolveAndIntegrate( [], [], [], 1, .1f, Vector3.Zero ) );
		body.Velocity = Vector3.UnitX;
		world.Prepare();
		world.SolveAndIntegrate( [], [], [], 1, .1f, Vector3.Zero );
		Near( .1f, body.Pose.Translation.X );
	}

	private static DriveConstraint[] Chain( PhysicalSolverWorld world )
	{
		var result = new DriveConstraint[ 2 ];
		for ( var i = 0; i < result.Length; ++i )
		{
			var geometry = new DriveGeometry( i, i + 1, Vector3.Zero, Vector3.Zero, Quaternion.Identity, Quaternion.Identity );
			result[ i ] = DriveCompiler.Compile( world.Body( i ), world.Body( i + 1 ), geometry,
				new( DriveMode.Position, 0f, 0f, 10000f ), new( DriveMode.Position, 0f, 0f, 0f ), .1f );
		}
		return result;
	}

	private static PhysicalBodyState Body( float x ) => new()
	{
		Pose = new( LimbRigidTransform.Identity.Rotation, new( x, 0f, 0f ) ), Orientation = Vector4.UnitW,
		MotionMode = 4, InverseMass = 1f, InverseInertiaDiagonal = Vector3.One
	};

	private static PhysicalSolverWorld World( PhysicalBodyState[] bodies, int[] order )
	{
		var settings = new BodyDynamicsSettings[ bodies.Length ];
		Array.Fill( settings, new( Vector3.One, 0f, 0f, 100f, 100f, 1f, 0f, 0 ) );
		return new( bodies, settings, order );
	}

	private static void Near( float expected, float actual ) => Assert.InRange( MathF.Abs( expected - actual ), 0f, .000001f );
}
