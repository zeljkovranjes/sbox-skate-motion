using System.Numerics;
using SkateMotion.Core.Animation;
using SkateMotion.Core.Grinding;
using Xunit;

namespace SboxSkateMotion.Tests;

public class PhysicalHandCandidateControllerTests
{
	[Fact]
	public void CompletedQueryIsConsumedOnceAndAnotherCompletionAllowsRefresh()
	{
		var controller = Controller();
		var state = new PhysicalHandCandidateState { Pending = 1 };
		var surface = Surface();
		controller.Update( state, Input(), surface );
		var trajectory = state.Trajectory;
		var flags = state.Flags;
		var frame = state.ApexFrame;
		var next = Input() with { Point = new Vector3( .5f, 2.5f, -.2f ) };
		state.Pending = 1;
		controller.Update( state, next, surface );
		Assert.Equal( Input().Point, state.Point );
		Assert.Equal( trajectory, state.Trajectory );
		Assert.Equal( frame, state.ApexFrame );
		Assert.Equal( flags, state.Flags );
		Assert.Equal( 0, state.Pending );
		surface.Complete( Classification() );
		state.Pending = 1;
		controller.Update( state, next, surface );
		Assert.Equal( next.Point, state.Point );
		Assert.NotEqual( trajectory, state.Trajectory );
	}

	[Theory]
	[InlineData( 0, true )]
	[InlineData( 1, false )]
	public void IdleAndDisabledUpdatesLeaveTheCompletedQueryAvailable( byte pending, bool enabled )
	{
		var controller = Controller();
		var state = new PhysicalHandCandidateState { Pending = pending, Point = new Vector3( 7f ) };
		var surface = Surface();
		controller.Update( state, Input() with { Enabled = enabled }, surface );
		Assert.Equal( new Vector3( 7f ), state.Point );
		Assert.Equal( 0, state.Pending );
		state.Pending = 1;
		controller.Update( state, Input(), surface );
		Assert.Equal( Input().Point, state.Point );
	}

	[Fact]
	public void BlockedClassificationConsumesTheQueryAndPreservesCandidateState()
	{
		var state = new PhysicalHandCandidateState { Pending = 1, Point = new Vector3( 7f ), Flags = 123 };
		var surface = Surface();
		surface.Complete( Classification() with { Kind = GrindSurfaceKind.Blocked } );
		Controller().Update( state, Input(), surface );
		Assert.False( surface.TryConsume( out _ ) );
		Assert.Equal( new Vector3( 7f ), state.Point );
		Assert.Equal( 123UL, state.Flags );
		Assert.Equal( 0, state.Pending );
	}

	[Theory]
	[InlineData( 0x87654321u, 0x35abcdefu, 0xb5abcdefu )]
	[InlineData( 0x07654321u, 0xb5abcdefu, 0x35abcdefu )]
	public void EdgeCopiesMetadataAndOnlyReplacesTheHighFlag( uint source, uint retained, uint expected )
	{
		var input = Input();
		input = input with { Edge = input.Edge with { Metadata = 0x12345678, Flags = source } };
		var state = new PhysicalHandCandidateState
		{
			Pending = 1,
			Edge = new GrindEdgeRecord( default, 0, retained )
		};
		Controller().Update( state, input, Surface() );
		Assert.Equal( input.Edge.Segment, state.Edge.Segment );
		Assert.Equal( 0x12345678u, state.Edge.Metadata );
		Assert.Equal( expected, state.Edge.Flags );
	}

	[Fact]
	public void ZeroAveragePreservesAllMetricLanesAndPublishesTheRetainedScore()
	{
		var state = new PhysicalHandCandidateState
		{
			Pending = 1,
			Metric = new Vector4( 7, 8, 9, 10 ),
			Flags = 0x123456789abcdef0UL
		};
		Controller( -1f ).Update( state, Input(), Surface() );
		Assert.Equal( new Vector4( 7, 8, 9, 10 ), state.Metric );
		Assert.Equal( BitConverter.SingleToUInt32Bits( 7f ), (uint)(state.Flags >> 32) );
		Assert.Equal( 0x0abcdef0u, (uint)state.Flags & 0x0fffffffu );
	}

	[Fact]
	public void TrajectoryReachesOffsetPointAtApexAndPreservesAuxiliaryLanes()
	{
		var state = new PhysicalHandCandidateState { Pending = 1 };
		Controller().Update( state, Input(), Surface() );
		Assert.Equal( Input().Origin, state.StartFrame.Translation );
		Assert.InRange( Vector3.Distance( new Vector3( 1, 3.3f, 0 ), state.ApexFrame.Translation ), 0f, .000001f );
		Assert.InRange( state.Trajectory.Velocity.Y + state.Trajectory.Acceleration.Y * state.ApexTime, -.000001f, .000001f );
		Assert.Equal( 1f, state.Trajectory.Acceleration.W );
		Assert.Equal( new Vector4( -1f ), state.Trajectory.Auxiliary );
		Assert.Equal( state.Trajectory, state.FirstTrajectory );
		Assert.Equal( state.Trajectory, state.SecondTrajectory );
		Assert.Equal( -.5f * state.ApexTime, state.Trajectory.Velocity.W, 6 );
	}

	[Fact]
	public void ParallelFrameInputsChooseAFiniteOrthonormalBasis()
	{
		var state = new PhysicalHandCandidateState { Pending = 1 };
		Controller().Update( state, Input() with { Tangent = Vector3.UnitY }, Surface() );
		var frame = state.StartFrame.Rotation;
		Assert.Equal( 1f, frame.X.Length(), 6 );
		Assert.Equal( 1f, frame.Y.Length(), 6 );
		Assert.Equal( 1f, frame.Z.Length(), 6 );
		Assert.Equal( 0f, Vector3.Dot( frame.X, frame.Y ), 6 );
		Assert.Equal( frame.Z, Vector3.Cross( frame.X, frame.Y ) );
	}

	private static PhysicalHandCandidateInput Input() => new(
		true, new Vector3( 1, 3, 0 ), new GrindEdgeRecord( new RailSegment( new Vector3( -2, 0, 0 ), new Vector3( 2, 0, 0 ) ), 0, 0 ),
		Vector3.UnitY, Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, Vector3.UnitX, new Vector3( 1, 3, 0 ) );

	private static GrindSurfaceClassification Classification() => default( GrindSurfaceClassification ) with { Kind = GrindSurfaceKind.ThinRail };

	private static GrindSurfaceQueryResult Surface()
	{
		var result = new GrindSurfaceQueryResult();
		result.Complete( Classification() );
		return result;
	}

	private static PhysicalHandCandidateController Controller( float response = 0f )
	{
		var math = new LimbSolverMath( [ -1f / 720f, 1f / 24f, -.5f ],
			[ -1f / 5040f, 1f / 120f, -1f / 6f ], [ 0f, 0f, 0f, 0f, 0f, 0f, 0f, MathF.PI / 2f ],
			2f / MathF.PI, MathF.PI / 2f, 0f, MathF.PI );
		var curve = new GrindEdgeSelectionCurve( [ -3, -2, -1, 0, .1f, .2f, .5f, 1 ],
			[ response, response, response, response, response, response, response, response ] );
		return new PhysicalHandCandidateController( math, curve, .3f, 0f, .2f );
	}
}
