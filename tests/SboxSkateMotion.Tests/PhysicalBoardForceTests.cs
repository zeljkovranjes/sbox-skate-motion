using System;
using System.Collections.Generic;
using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class PhysicalBoardForceTests
{
	[Fact]
	public void QueueAcceptsTwentyOneEntriesAndOnlyExplicitResetClearsThem()
	{
		var queue = new PhysicalBoardForceQueue();
		for ( var i = 0; i < 21; i++ )
			Assert.True( queue.TryAppend( new( (uint)i, Vector3.UnitX, Vector3.Zero ) ) );
		Assert.False( queue.TryAppend( new( 99, Vector3.UnitY, Vector3.Zero ) ) );
		var body = Body();
		var properties = new Properties();
		PhysicalBoardForceConsumer.Apply( queue, body, properties );
		Assert.Equal( new Vector3( 21, 0, 0 ), body.LinearAccumulator );
		Assert.Equal( 21, queue.Count );
		PhysicalBoardForceConsumer.Apply( queue, body, properties );
		Assert.Equal( new Vector3( 42, 0, 0 ), body.LinearAccumulator );
		queue.Reset();
		Assert.Equal( 0, queue.Count );
		Assert.Equal( 2, properties.Reads );
	}

	[Fact]
	public void ForceUsesRotatedLocalApplicationAndClearsAccumulatorState()
	{
		var body = Body();
		body.Pose = new( new( Vector3.UnitY, -Vector3.UnitX, Vector3.UnitZ ), new( 100, 200, 300 ) );
		body.AccumulatorState = 9;
		var queue = new PhysicalBoardForceQueue();
		queue.TryAppend( new( 15, Vector3.UnitZ, Vector3.UnitX ) );
		PhysicalBoardForceConsumer.Apply( queue, body, new Properties { Height = 2f } );
		Assert.Equal( Vector3.UnitZ, body.LinearAccumulator );
		Assert.Equal( new Vector3( 1, 2, 0 ), body.AngularAccumulator );
		Assert.Equal( 0, body.AccumulatorState );
	}

	[Fact]
	public void QuerySubmissionPreservesGroundResultsButClearsInactiveForwardCache()
	{
		var state = new PhysicalBoardProbeState { SkeletonFlags = ulong.MaxValue };
		state.Ground.Position = new( 4, 5, 6 );
		state.Ground.Valid = true;
		state.Forward.Position = Vector3.One;
		state.Forward.Valid = true;
		var sink = new Sink();
		var bindings = Bindings();
		PhysicalBoardProbeProducer.Update( state, new( 701, Vector3.UnitZ, Vector3.UnitY,
			1f, Vector3.Zero, Vector3.UnitY ), bindings, -1, sink );
		Assert.Equal( 5, sink.Queries.Count );
		Assert.Equal( new Vector3( 6, -100, 0 ), sink.Queries[ 4 ].End );
		Assert.Equal( .15f, sink.Queries[ 4 ].Radius );
		Assert.Equal( new Vector3( 4, 5, 6 ), state.Ground.Position );
		Assert.False( state.Ground.Valid );
		Assert.Equal( Vector3.Zero, state.Forward.Position );
		Assert.Equal( ulong.MaxValue & ~(1UL << 61), state.SkeletonFlags );
		Assert.Equal( new[] { "begin", "end" }, sink.Events );
	}

	[Fact]
	public void SuppressedQueriesStillConsumeForcesAndLeaveCachesUntouched()
	{
		var state = new PhysicalBoardProbeState { ForwardEnabled = true, SkeletonFlags = 99 };
		state.Ground.Valid = true;
		var sink = new Sink();
		var body = Body();
		var queue = new PhysicalBoardForceQueue();
		queue.TryAppend( new( 15, Vector3.UnitX, Vector3.Zero ) );
		PhysicalBoardPostMotorController.Update( true, state, default, [], -1, sink, queue, body, new Properties() );
		Assert.Empty( sink.Events );
		Assert.True( state.Ground.Valid );
		Assert.True( state.ForwardEnabled );
		Assert.Equal( 99UL, state.SkeletonFlags );
		Assert.Equal( Vector3.UnitX, body.LinearAccumulator );
	}

	[Fact]
	public void ForwardQueryUsesOwnerPositionAndLeavesItsOldResultPending()
	{
		var state = new PhysicalBoardProbeState();
		state.Forward.Normal = Vector3.UnitX;
		state.Forward.Valid = true;
		var sink = new Sink();
		PhysicalBoardProbeProducer.Update( state, new( 100, Vector3.UnitZ, Vector3.UnitZ,
			.1f, new( 2, 3, 4 ), Vector3.UnitY ), Bindings(), 42, sink );
		Assert.Equal( 6, sink.Queries.Count );
		Assert.Equal( new Vector3( 2, 3, 4.05f ), sink.Queries[ 5 ].Start );
		Assert.Equal( new Vector3( 2, -1, 4.05f ), sink.Queries[ 5 ].End );
		Assert.Equal( 42, sink.Queries[ 5 ].ExcludedActor );
		Assert.Equal( Vector3.UnitX, state.Forward.Normal );
		Assert.False( state.Forward.Valid );
	}

	[Fact]
	public void NonLaunchMotorUpdatesResponseAndAppliesContactForceToThePrimaryBody()
	{
		var primary = Body();
		var board = Body();
		var state = new PhysicalBoardForceState { SmoothedResponse = .8f, FirstTimer = .2f };
		var queue = new PhysicalBoardForceQueue();
		var input = new PhysicalBoardForceInput( 1UL << 29, 1UL << 59,
			LimbRigidTransform.Identity, LimbRigidTransform.Identity with { Translation = Vector3.UnitX },
			Vector3.UnitY, 0f, Vector3.UnitY, Vector3.UnitZ, Vector3.Zero, Vector3.UnitZ, 2f, .5f );
		Producer().Update( state, input, new( .25f, .5f, .1f, .5f, 3f ), new() { Body = board }, primary, queue );
		Assert.Equal( .6f, state.SmoothedResponse );
		Assert.Equal( .6f, state.FirstResponse );
		Assert.Equal( .2f, state.FirstTimer );
		Assert.True( state.HasRelativeRotation );
		Assert.Equal( new Vector3( 0, -40, 0 ), primary.LinearAccumulator );
		Assert.Equal( new Vector3( 0, 0, -40 ), primary.AngularAccumulator );
		Assert.Equal( Vector3.Zero, board.LinearAccumulator );
		Assert.Equal( Vector3.Zero, board.AngularAccumulator );
		Assert.Equal( 0, queue.Count );
	}

	[Fact]
	public void SpeedCorrectionClampsVelocityChangeAndDoesNotBrakeWhenAlreadyFaster()
	{
		var input = new PhysicalBoardForceInput( 0, 1UL << 49, LimbRigidTransform.Identity,
			LimbRigidTransform.Identity, Vector3.UnitY, 0f, Vector3.UnitY, Vector3.UnitZ,
			Vector3.Zero, Vector3.UnitZ, 2f, .5f );
		var tuning = new PhysicalBoardForceTuning( .25f, .5f, .1f, .5f, 3f );
		Assert.True( Producer().TrySpeedCorrection( input, tuning, out var force, out _, out var velocity ) );
		Assert.Equal( new Vector3( 0, 0, 3 ), velocity );
		Assert.Equal( new Vector3( 0, 0, 2 ), force.Force );
		Assert.Equal( new Vector3( 0, .1f, 0 ), force.Application );
		Assert.Equal( 15U, force.Kind );
		Assert.False( Producer().TrySpeedCorrection( input with { Velocity = new( 0, 0, 4 ) },
			tuning, out force, out _, out _ ) );
		Assert.Equal( Vector3.Zero, force.Force );
	}

	private static PhysicalBoardForceProducer Producer()
	{
		var math = new LimbSolverMath( [ -1f / 720f, 1f / 24f, -.5f ],
			[ -1f / 5040f, 1f / 120f, -1f / 6f ], [ 0, 0, 0, 0, 0, 0, 0, MathF.PI / 2f ],
			2f / MathF.PI, MathF.PI / 2f, 0f, MathF.PI );
		var curve = new BoardCorrectionCurve( [ 0, .1f, .2f, .3f, .4f, .5f, .7f, 1 ], new float[ 8 ] );
		return new( math, new( math, 1e-8f, MathF.PI * 2f, 1f / (MathF.PI * 2f) ), curve, curve, curve );
	}

	private static PhysicalBodyState Body() => new()
	{
		Pose = LimbRigidTransform.Identity, InverseMass = 1f, InverseInertiaDiagonal = Vector3.One
	};

	private static PhysicalBodyBinding[] Bindings()
	{
		var result = new PhysicalBodyBinding[ 7 ];
		for ( var i = 0; i < result.Length; i++ )
			result[ i ] = new() { CachedPose = LimbRigidTransform.Identity with { Translation = new( i, 0, 0 ) } };
		return result;
	}

	private sealed class Properties : IPhysicalBoardForceProperties
	{
		internal float Height;
		internal int Reads;
		public float ReadApplicationHeight() => Height;
		public void ReadPostForceProperty() => Reads++;
	}

	private sealed class Sink : IPhysicalBoardProbeSink
	{
		internal List<PhysicalBoardProbe> Queries { get; } = new();
		internal List<string> Events { get; } = new();
		public void Begin() => Events.Add( "begin" );
		public void Add( PhysicalBoardProbe probe ) => Queries.Add( probe );
		public void End() => Events.Add( "end" );
	}
}
