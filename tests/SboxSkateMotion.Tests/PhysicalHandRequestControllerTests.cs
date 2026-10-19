using System.Numerics;
using SkateMotion.Core.Animation;
using SkateMotion.Core.Grinding;
using Xunit;

namespace SboxSkateMotion.Tests;

public class PhysicalHandRequestControllerTests
{
	private static PhysicalHandRequestInput Input => new(
		Vector3.Zero, new Vector3( 1, 4, 0 ), Vector3.UnitZ, Vector3.UnitZ, Vector3.UnitX, 0 );

	[Theory]
	[InlineData( 0 )]
	[InlineData( 1 )]
	public void IneligibleRequestResetsContactButPreservesPendingAndEdges( byte pending )
	{
		var (controller, plan) = Create();
		var state = new PhysicalHandRequestState();
		state.Source.Point = new Vector3( 7, 8, 9 );
		state.Source.Enabled = true;
		state.Source.Side = 2;
		state.Source.Edge = new( new RailSegment( Vector3.Zero, Vector3.One ), 7, 0xffffffffu );
		var edge = state.Source.Edge;
		var candidate = new PhysicalHandCandidateState
		{
			Pending = pending, Flags = ulong.MaxValue, Point = Vector3.One,
			Metric = Vector4.One, StartTime = 4f, ApexTime = 8f, SideSign = -1f
		};
		Assert.False( controller.Prepare( [], state, candidate,
			Input with { Velocity = -Vector3.UnitY }, Vector3.Zero ) );
		Assert.Equal( new Vector3( 7, 8, 9 ), state.PreviousPoint );
		Assert.False( state.Source.Enabled );
		Assert.Equal( Vector3.Zero, state.Source.Point );
		Assert.Equal( 0, state.Source.Side );
		Assert.Equal( edge, state.Source.Edge );
		Assert.Equal( pending, candidate.Pending );
		Assert.Equal( 0x7f7fffff4fffffffUL, candidate.Flags );
		Assert.Equal( Vector3.Zero, candidate.Point );
		Assert.Equal( new Vector4( -1 ), candidate.Metric );
		Assert.Equal( -1f, candidate.StartTime );
		Assert.Equal( -1f, candidate.ApexTime );
		Assert.Equal( 0f, candidate.SideSign );
		Assert.Equal( 0, plan.Count );
	}

	[Fact]
	public void EligibleLookupMissMarksPendingWithoutKeepingOldActivation()
	{
		var (controller, _) = Create();
		var state = new PhysicalHandRequestState();
		state.Source.Point = new Vector3( 5 );
		state.Source.Enabled = true;
		var candidate = new PhysicalHandCandidateState();
		Assert.False( controller.Prepare( [], state, candidate, Input, Vector3.Zero ) );
		Assert.Equal( (byte)1, candidate.Pending );
		Assert.False( state.Source.Enabled );
		Assert.Equal( Vector3.Zero, state.Source.Point );
		Assert.Equal( new Vector3( 5 ), state.CandidateInput.PreviousPoint );
	}

	[Fact]
	public void SuccessBuildsSixProbesFromSelectedContactAndBindsCandidateInputs()
	{
		var (controller, plan) = Create();
		var state = new PhysicalHandRequestState();
		var candidate = new PhysicalHandCandidateState();
		Assert.True( controller.Prepare( [ Chunk() ], state, candidate, Input, new Vector3( 100 ) ) );
		Assert.Equal( 6, plan.Count );
		Assert.True( state.Source.Enabled );
		Assert.Equal( state.Source.Point, plan.Center );
		Assert.Equal( Input.Velocity, state.CandidateInput.Approach );
		Assert.Equal( Input.Side, state.CandidateInput.Side );
		Assert.Equal( Input.Tangent, state.CandidateInput.Tangent );
		Assert.Equal( state.Source.Edge, state.CandidateInput.Edge );
	}

	[Fact]
	public void SuccessThenIneligibleThenMissThenSuccessRetainsOnlyRequiredState()
	{
		var (controller, plan) = Create();
		var state = new PhysicalHandRequestState();
		var candidate = new PhysicalHandCandidateState();
		Assert.True( controller.Prepare( [ Chunk() ], state, candidate, Input, Vector3.Zero ) );
		var first = state.Source.Point;
		var probe = plan.GetProbe( 0 );
		candidate.Pending = 0;
		Assert.False( controller.Prepare( [], state, candidate, Input with { Velocity = Vector3.Zero }, Vector3.Zero ) );
		Assert.Equal( first, state.PreviousPoint );
		Assert.Equal( (byte)0, candidate.Pending );
		Assert.Equal( probe, plan.GetProbe( 0 ) );
		Assert.False( controller.Prepare( [], state, candidate, Input, Vector3.Zero ) );
		Assert.Equal( Vector3.Zero, state.PreviousPoint );
		Assert.Equal( (byte)1, candidate.Pending );
		Assert.True( controller.Prepare( [ Chunk() ], state, candidate, Input, Vector3.Zero ) );
		Assert.Equal( first, state.Source.Point );
	}

	private static (PhysicalHandRequestController Controller, GrindSurfaceProbePlan Plan) Create()
	{
		var math = new LimbSolverMath( [ -1f / 720f, 1f / 24f, -.5f ],
			[ -1f / 5040f, 1f / 120f, -1f / 6f ], [ 0, 0, 0, 0, 0, 0, 0, MathF.PI / 2 ],
			2 / MathF.PI, MathF.PI / 2, 0, MathF.PI );
		var reach = new PhysicalHandReachBuilder( Flat( .1f ), Flat( 12f ), Flat( 0f ), Flat( 1f ),
			Flat( 2f ), Flat( .5f ), Flat( 1f ), .25f );
		var source = new PhysicalHandSourceController( reach,
			new PhysicalHandSourceSelection( new LimbSignedAngle( math, .0001f, MathF.PI * 2, 1 / (MathF.PI * 2) ) ), 1.5f );
		var gate = new PhysicalHandSourceEligibility( new LimbArcSine( -.5f, .9f, -5.5f, 5.6f, 1.57f ), 1f, 30f );
		var plan = new GrindSurfaceProbePlan( new( .00001f, .00001f, .05f, .1f, .5f, .001f, 6f ) );
		return (new( gate, source, plan, .3f ), plan);
	}

	private static GrindHeightCorrectionCurve Flat( float value ) => new( [ 0f, 2f, 5f, 10f ], [ value, value, value, value ] );

	private static GrindEdgeChunk Chunk()
	{
		var a = new Vector3( -2f, .5f, -.25f );
		var b = new Vector3( 2f, .5f, -.25f );
		return new( new( new Vector3( -10 ), new Vector3( 10 ) ),
			[ new( 0, 255, [ -1, -1, -1, -1, -1, -1, -1, -1 ] ) ], [ -1 ],
			[ new( Vector3.Min( a, b ), Vector3.Max( a, b ) ) ], [ new( new( a, b ), 7, 0x80000000u ) ] );
	}
}
