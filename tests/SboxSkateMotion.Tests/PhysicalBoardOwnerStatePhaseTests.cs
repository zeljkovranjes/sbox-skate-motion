using System;
using System.Numerics;
using System.Collections.Generic;
using SkateMotion.Core.Animation;
using SkateMotion.Core.Physics;
using SkateMotion.Core.Vehicle;
using Xunit;

namespace SboxSkateMotion.Tests;

public class PhysicalBoardOwnerStatePhaseTests
{
	[Theory]
	[InlineData( false )]
	[InlineData( true )]
	public void JoinedOwnerReadsAfterMotorAndCompanionBeforeQueriesAndForceDrain( bool suppress )
	{
		var character = PhysicalBoardOwnerFixture.State( true );
		var body = character.BoardBodies[ 6 ].Body;
		body.InverseInertiaDiagonal = Vector3.One;
		var events = new List<string>();
		var boundary = new Observations( character, events, suppress );
		var session = CreateSession( events, () =>
		{
			events.Add( "companion" );
			body.Velocity = Vector3.UnitX;
			body.Pose = LimbRigidTransform.Identity with { Translation = new( 5f, 6f, 7f ) };
		} );
		session.ChangeState( 5, default, boundary.Poses );
		Assert.Equal( new[] { "held", "joints-on" }, events );
		events.Clear();
		var phase = new PhysicalBoardOwnerStatePhase( PhysicalBoardOwnerFixture.Create(), session, boundary, boundary, boundary );
		var queue = new PhysicalBoardForceQueue();
		queue.TryAppend( new( 15, new( 60f, 0f, 0f ), Vector3.Zero ) );
		var ground = PhysicalBoardOwnerFixture.Input();
		var input = new PhysicalNonJumpMotionInput( suppress ? 2 : 0, 0f, .5f, suppress ? 1UL << 22 : 0,
			new( 1UL << 29, suppress ? 1UL << 54 : 0, LimbRigidTransform.Identity, LimbRigidTransform.Identity,
				Vector3.UnitY, 0f, Vector3.UnitY, Vector3.UnitZ, Vector3.Zero, Vector3.UnitZ, 1f, 1f / 60f ),
			new( 1f, 1f, 0f, 1f, 1f ), new( ground.OrientationSource, ground.RigInput, ground.PlacementDeltaTime, 7 ) );
		var direction = new PhysicalDirectionState
		{
			Direction = Vector3.UnitY, SourceFrame = LimbRigidTransform.Identity,
			WorldFrame = LimbRigidTransform.Identity, FilteredFrame = LimbRigidTransform.Identity
		};
		foreach ( var filter in new[] { direction.PrimaryFilter, direction.FirstFilter, direction.SecondFilter } )
		{
			filter.Value = Vector3.UnitY;
			filter.PreviousInput = Vector3.UnitY;
			filter.Coefficients = new( 1f, 0f, 0f, 1f );
		}
		var motor = new PhysicalJumpCharacterState { Launched = true };
		var forces = new PhysicalBoardForceState();
		var state = new PhysicalBoardOwnerPhaseState { Elapsed = 2f };
		phase.UpdateNonJump( state, motor, character, new(), forces, direction, input, body, queue, new() );
		Assert.False( motor.Launched );
		Assert.True( forces.HasRelativeRotation );
		Assert.Equal( 1, direction.HistoryIndex );
		Assert.Equal( 1, queue.Count );
		Assert.Equal( new Vector3( 60f, -40f, 0f ), body.LinearAccumulator );
		Assert.Equal( 2.25f, state.Elapsed );
		Assert.Equal( 2f / 60f, state.PredictedDisplacement.X, 6 );
		Assert.Equal( -40f / 3600f, state.PredictedDisplacement.Y, 6 );
		var expected = new List<string> { "enabled", "input", "poses", "companion", "suppress" };
		if ( !suppress )
			expected.AddRange( [ "probe-input", "actor", "begin", "add", "add", "add", "add", "add", "end" ] );
		expected.AddRange( [ "height", "post", "delta" ] );
		Assert.Equal( expected, events );
		if ( !suppress )
			Assert.Equal( new Vector3( 5f, 6f, 7f ), boundary.LastProbe.Start );
	}

	[Theory]
	[InlineData( 0, .49f, false )]
	[InlineData( 0, .5f, false )]
	[InlineData( 0, -.5001f, true )]
	[InlineData( 1, 2f, false )]
	[InlineData( 2, 0f, true )]
	[InlineData( 3, 1f, true )]
	public void RidingRequiresBothTheCharacterGateAndTheAction( int mode, float vertical, bool expected )
	{
		Assert.Equal( expected, PhysicalNonJumpMotionController.SelectRiding( mode, vertical, .5f, 1UL << 54 ) );
		Assert.False( PhysicalNonJumpMotionController.SelectRiding( mode, vertical, .5f, 0 ) );
	}

	[Fact]
	public void DisplacementUsesPostForceAccumulatorsWithoutConsumingThem()
	{
		var body = new PhysicalBodyState
		{
			Velocity = new( 1.25f, -2.5f, .125f ),
			LinearAccumulator = new( 360f, -180f, 90f ),
			AngularAccumulator = new( -4f, 8f, -16f )
		};
		var result = PhysicalBoardOwnerStatePhase.PredictDisplacement( body );
		Assert.Equal( 0x3df7777a, BitConverter.SingleToInt32Bits( result.X ) );
		Assert.Equal( unchecked((int)0xbdbbbbbd), BitConverter.SingleToInt32Bits( result.Y ) );
		Assert.Equal( 0x3cdddde0, BitConverter.SingleToInt32Bits( result.Z ) );
		Assert.Equal( new Vector3( 360f, -180f, 90f ), body.LinearAccumulator );
		Assert.Equal( new Vector3( -4f, 8f, -16f ), body.AngularAccumulator );
		Assert.Equal( new Vector3( 1.25f, -2.5f, .125f ), body.Velocity );
	}

	private static PhysicalBoardSession CreateSession( List<string> events, Action companion )
	{
		var curve = new BoardCorrectionCurve( [ 0f, 1f, 2f, 3f, 4f, 5f, 6f, 7f ], new float[ 8 ] );
		var math = new LimbSolverMath( [ 0f, 0f, 0f ], [ 0f, 0f, 0f ], new float[ 8 ], 2f / MathF.PI, MathF.PI / 2f, 0f, MathF.PI );
		var blend = new LimbPoseBlender( math, new( new float[ 8 ], MathF.PI / 2f, MathF.PI ), .00001f, .1f );
		void Unexpected() => throw new InvalidOperationException( "Unexpected companion transition." );
		var actuators = new PhysicalBoardActuators( () => events.Add( "held" ), Unexpected,
			enabled => events.Add( enabled ? "joints-on" : "joints-off" ), Unexpected,
			() => 4UL, _ => companion(), Unexpected, _ => Unexpected(), _ => Unexpected(),
			_ => Unexpected(), Unexpected, _ => Unexpected() );
		return new( new( curve, .001f, 30f, 5f, .1f ), new( curve, blend, .05f ),
			new( 100f, .05f ), 30f, new BoardTargetDrive(), new BoardHeldConstraints(), actuators );
	}

	private sealed class Observations( PhysicalCharacterState character, List<string> events, bool suppress )
		: IPhysicalBoardOwnerObservations, IPhysicalBoardProbeSink, IPhysicalBoardForceProperties
	{
		internal PhysicalBoardProbe LastProbe { get; private set; }
		internal PhysicalBoardPoses Poses => new( LimbRigidTransform.Identity, LimbRigidTransform.Identity,
			LimbRigidTransform.Identity, LimbRigidTransform.Identity, LimbRigidTransform.Identity,
			Vector3.UnitX, -Vector3.UnitX, Vector3.Zero, Vector3.Zero, Vector3.UnitZ, Vector3.UnitY, Vector3.UnitY );
		public bool BoardSessionEnabled { get { events.Add( "enabled" ); return true; } }
		public PhysicalBoardInput ReadBoardInput()
		{
			events.Add( "input" );
			Assert.Equal( new Vector3( 0f, -40f, 0f ), character.BoardBodies[ 6 ].Body.LinearAccumulator );
			Assert.Equal( suppress ? 0x80000UL : 0UL, character.Output.UpdateFlags & 0x80000UL );
			return new( 5, 0, 0, 0, 0f, false, 0, false, false );
		}
		public PhysicalBoardPoses ReadBoardPoses() { events.Add( "poses" ); return Poses; }
		public bool SuppressQueries { get { events.Add( "suppress" ); return suppress; } }
		public PhysicalBoardProbeInput ReadProbeInput()
		{
			events.Add( "probe-input" );
			Assert.Equal( Vector3.UnitX, character.BoardBodies[ 6 ].Body.Velocity );
			return new( 0, Vector3.UnitZ, Vector3.UnitY, 0f, Vector3.Zero, Vector3.UnitY );
		}
		public int ExcludedActor { get { events.Add( "actor" ); return 42; } }
		public float DeltaTime { get { events.Add( "delta" ); return .25f; } }
		public void Begin() => events.Add( "begin" );
		public void Add( PhysicalBoardProbe probe ) { events.Add( "add" ); LastProbe = probe; }
		public void End() => events.Add( "end" );
		public float ReadApplicationHeight() { events.Add( "height" ); return 0f; }
		public void ReadPostForceProperty() => events.Add( "post" );
	}
}
