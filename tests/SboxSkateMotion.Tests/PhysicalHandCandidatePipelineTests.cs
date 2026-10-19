using System.Numerics;
using SkateMotion.Core.Animation;
using SkateMotion.Core.Collision;
using Xunit;

namespace SboxSkateMotion.Tests;

public class PhysicalHandCandidatePipelineTests
{
	private static PhysicalHandRequestInput Input => new(
		Vector3.Zero, new Vector3( 1, 4, 0 ), Vector3.UnitZ, Vector3.UnitZ, Vector3.UnitX, 0 );

	[Fact]
	public void DeferredEvaluationUsesGeneratedContactAndConsumesTheResultOnce()
	{
		var pipeline = Create();
		var state = new PhysicalHandCandidatePipelineState();
		Assert.True( pipeline.Prepare( [ Chunk() ], [], state, Input, Vector3.Zero, -1 ) );
		Assert.Equal( Vector3.Zero, state.Candidate.Point );
		Assert.Equal( (byte)1, state.Candidate.Pending );
		Assert.Equal( new Vector4( -1 ), state.Candidate.Metric );
		pipeline.Evaluate( state );
		Assert.Equal( state.Request.Source.Point, state.Candidate.Point );
		Assert.NotEqual( 0UL, state.Candidate.Flags & (1UL << 31) );
		Assert.Equal( (byte)0, state.Candidate.Pending );
		Assert.False( state.Surface.TryConsume( out _ ) );
		var trajectory = state.Candidate.Trajectory;
		state.Candidate.Point = new Vector3( 7 );
		pipeline.Evaluate( state );
		Assert.Equal( new Vector3( 7 ), state.Candidate.Point );
		Assert.Equal( trajectory, state.Candidate.Trajectory );
	}

	[Theory]
	[InlineData( false )]
	[InlineData( true )]
	public void FailedRequestLeavesEarlierCompletionAvailable( bool ineligible )
	{
		var pipeline = Create();
		var state = new PhysicalHandCandidatePipelineState();
		Assert.True( pipeline.Prepare( [ Chunk() ], [], state, Input, Vector3.Zero, -1 ) );
		var next = ineligible ? Input with { Velocity = Vector3.Zero } : Input;
		Assert.False( pipeline.Prepare( [], [], state, next, Vector3.Zero, -1 ) );
		pipeline.Evaluate( state );
		Assert.False( state.Request.Source.Enabled );
		Assert.Equal( (byte)0, state.Candidate.Pending );
		Assert.Equal( 0UL, state.Candidate.Flags & (1UL << 31) );
		Assert.True( state.Surface.TryConsume( out _ ) );
		Assert.False( state.Surface.TryConsume( out _ ) );
	}

	[Fact]
	public void ReplacementBeforeConsumptionUsesTheLatestContact()
	{
		var pipeline = Create();
		var state = new PhysicalHandCandidatePipelineState();
		Assert.True( pipeline.Prepare( [ Chunk() ], [], state, Input, Vector3.Zero, -1 ) );
		var first = state.Request.Source.Point;
		Assert.True( pipeline.Prepare( [ Chunk() ], [], state, Input with { RequestedSide = 2 }, Vector3.Zero, -1 ) );
		Assert.Equal( first, state.Request.PreviousPoint );
		Assert.True( state.Request.Source.Point.X < 0f );
		pipeline.Evaluate( state );
		Assert.Equal( state.Request.Source.Point, state.Candidate.Point );
		Assert.NotEqual( first, state.Candidate.Point );
	}

	[Fact]
	public void BlockingMapSurfaceConsumesQueryWithoutActivatingCandidate()
	{
		var pipeline = Create();
		var state = new PhysicalHandCandidatePipelineState();
		Assert.True( pipeline.Prepare( [ Chunk() ], [ Plane() ], state, Input, Vector3.Zero, -1 ) );
		Assert.True( state.Request.Source.Enabled );
		pipeline.Evaluate( state );
		Assert.Equal( 0UL, state.Candidate.Flags & (1UL << 31) );
		Assert.Equal( Vector3.Zero, state.Candidate.Point );
		Assert.Equal( new Vector4( -1 ), state.Candidate.Metric );
		Assert.False( state.Surface.TryConsume( out _ ) );
	}

	[Fact]
	public void NewRequestRecoversAfterBlockedSurfaceAndMiss()
	{
		var pipeline = Create();
		var state = new PhysicalHandCandidatePipelineState();
		Assert.True( pipeline.Prepare( [ Chunk() ], [ Plane() ], state, Input, Vector3.Zero, -1 ) );
		pipeline.Evaluate( state );
		Assert.False( pipeline.Prepare( [], [], state, Input, Vector3.Zero, -1 ) );
		pipeline.Evaluate( state );
		Assert.True( pipeline.Prepare( [ Chunk() ], [], state, Input, Vector3.Zero, -1 ) );
		pipeline.Evaluate( state );
		Assert.Equal( state.Request.Source.Point, state.Candidate.Point );
		Assert.NotEqual( 0UL, state.Candidate.Flags & (1UL << 31) );
	}

	private static PhysicalHandCandidatePipeline Create()
	{
		var math = new LimbSolverMath( [ -1f / 720f, 1f / 24f, -.5f ],
			[ -1f / 5040f, 1f / 120f, -1f / 6f ], [ 0, 0, 0, 0, 0, 0, 0, MathF.PI / 2 ],
			2 / MathF.PI, MathF.PI / 2, 0, MathF.PI );
		var reach = new PhysicalHandReachBuilder( Flat( .1f ), Flat( 12f ), Flat( 0f ), Flat( 1f ),
			Flat( 2f ), Flat( .5f ), Flat( 1f ), .25f );
		var source = new PhysicalHandSourceController( reach,
			new PhysicalHandSourceSelection( new LimbSignedAngle( math, .0001f, MathF.PI * 2, 1 / (MathF.PI * 2) ) ), 1.5f );
		var gate = new PhysicalHandSourceEligibility( new LimbArcSine( -.5f, .9f, -5.5f, 5.6f, 1.57f ), 1f, 30f );
		var plan = new GrindSurfaceProbePlan( new( 1e-6f, 1e-5f, .09f, .04f, 1.06f, .001f, 6 ) );
		var request = new PhysicalHandRequestController( gate, source, plan, .3f );
		var classifier = new GrindSurfaceClassifier( math, new( .65f, 1e-6f, .9f, .99f,
			-.035f, .035f, -.24f, -.2f, MathF.PI / 2, MathF.PI * 2, .01f ) );
		var query = new MeshWorldQuery( 1e-8f, -.00001f, 2.938735877055719e-39f, 1.52587890625e-5f, 1e-6f );
		var response = new GrindEdgeSelectionCurve( [ -3, -2, -1, 0, .1f, .2f, .5f, 1 ], [ .5f, .4f, .3f, .2f, .1f, 0, -.1f, -.2f ] );
		return new( request, classifier, query, new PhysicalHandCandidateController( math, response, .3f, 0f, .2f ) );
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

	private static MeshQueryInstance Plane()
	{
		var a = new Vector3( -3, .5f, -3 );
		var b = new Vector3( -3, .5f, 3 );
		var c = new Vector3( 3, .5f, 3 );
		var d = new Vector3( 3, .5f, -3 );
		var geometry = new MeshQueryGeometry( [ new( a, b, c, 0x183 ), new( a, c, d, 0x183 ) ], a, c );
		var identity = Matrix4x4.Identity;
		identity.M44 = 0;
		return new( geometry, identity, identity, a, c, -1, 0, 1 );
	}
}
