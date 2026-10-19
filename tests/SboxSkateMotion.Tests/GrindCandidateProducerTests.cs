using System;
using System.Numerics;
using SkateMotion.Core.Animation;
using SkateMotion.Core.Collision;
using SkateMotion.Core.Grinding;
using Xunit;

namespace SboxSkateMotion.Tests;

public class GrindCandidateProducerTests
{
	private static readonly GrindEdgeRecord Edge = new( new( new( -3, 0, 0 ), new( 3, 0, 0 ) ), 1, 0 );
	private static MeshWorldQuery Query() => new( 1e-8f, -.00001f, 2.938735877055719e-39f, 1.52587890625e-5f, 1e-6f );
	private static GrindCandidateInput Input => new( Vector3.UnitX, new( 0, 1, 1 ), new( 0, 1, 1 ), .5f, .3f, .6f, .2f, -1 );

	[Fact]
	public void EmptyLookupUsesPredictedBoundsAndPreservesCandidateAndAcceptance()
	{
		var candidate = State() with { Time = .5f, Trajectory = new( new( 1, 2, 3 ), new( 4, 6, 8 ), new( 2, -4, 6 ), .1f ) };
		var state = new GrindCandidateProductionState( candidate, true, true );
		var producer = new GrindCandidateProducer( new(), Create(), 7f );
		Assert.Equal( 0, producer.Evaluate( [], Input, Query(), [], ref state ) );
		Assert.Equal( candidate, state.Candidate );
		Assert.False( state.FoundEdges );
		Assert.True( state.Accepted );
		Assert.Equal( new Vector3( 1.25f, 4, 5.75f ), producer.Bounds.Minimum );
		Assert.Equal( new Vector3( 5.25f, 8.5f, 9.75f ), producer.Bounds.Maximum );
	}

	[Theory]
	[InlineData( 0f )]
	[InlineData( -2f )]
	[InlineData( .75f )]
	public void AcceptedScoreDoesNotDetermineAcceptanceFlag( float score )
	{
		var state = new GrindCandidateProductionState( State(), false, false );
		var producer = new GrindCandidateProducer( new(), Create(), score );
		Assert.Equal( score, producer.Evaluate( Chunks( Edge ), Input, Query(), [ Plane( -.04f, .04f ) ], ref state ) );
		Assert.True( state.FoundEdges );
		Assert.True( state.Accepted );
		Assert.Equal( 1, producer.EdgeCount );
		Assert.Equal( 2, state.Candidate.SampleIndex );
	}

	[Fact]
	public void FoundEdgesCanFailAcceptanceWithoutClearingPriorAcceptedFlag()
	{
		var initial = State();
		var state = new GrindCandidateProductionState( initial, false, true );
		var producer = new GrindCandidateProducer( new(), Create(), 7f );
		Assert.Equal( 0, producer.Evaluate( Chunks( Edge ), Input with { Approach = new( 0, 8, 0 ) }, Query(), [], ref state ) );
		Assert.True( state.FoundEdges );
		Assert.True( state.Accepted );
		Assert.Equal( Edge.Segment, state.Candidate.Edge );
		Assert.Equal( initial.Contact, state.Candidate.Contact );
	}

	[Fact]
	public void EmptyLookupAfterAcceptanceDoesNotRebuildPendingSamples()
	{
		var state = new GrindCandidateProductionState( State(), false, false );
		state.Candidate = state.Candidate with { Trajectory = state.Candidate.Trajectory with { Velocity = new( 8, 0, 0 ) } };
		var second = Edge with { Segment = new( new( -3, 0, .3f ), new( 3, 0, .3f ) ) };
		var acceptance = Create();
		var producer = new GrindCandidateProducer( new(), acceptance, 7f );
		Assert.Equal( 7, producer.Evaluate( Chunks( Edge, second ), Input, Query(), [ Plane( -.04f, .04f ) ], ref state ) );
		Assert.Equal( 1, acceptance.RemainingCount );
		var previous = state.Candidate;
		Assert.Equal( 0, producer.Evaluate( [], Input, Query(), [], ref state ) );
		Assert.Equal( previous, state.Candidate );
		Assert.Equal( 1, acceptance.RemainingCount );
		Assert.Equal( 2, acceptance.SourceEdgeCount );
		Assert.True( state.Accepted );
		Assert.False( state.FoundEdges );
	}

	[Fact]
	public void PredictedWindowCanExcludeAnEdgeNearTheCurrentPosition()
	{
		var state = new GrindCandidateProductionState( State() with { Time = 3f }, false, false );
		var acceptance = Create();
		var producer = new GrindCandidateProducer( new(), acceptance, 7f );
		Assert.Equal( 0, producer.Evaluate( Chunks( Edge ), Input, Query(), [ Plane( -.04f, .04f ) ], ref state ) );
		Assert.False( state.FoundEdges );
		Assert.False( state.Accepted );
		Assert.Equal( 0, acceptance.QueryCount );
	}

	private static GrindEdgeChunk[] Chunks( params GrindEdgeRecord[] records )
	{
		var bounds = new GrindEdgeBounds[ records.Length ];
		var next = new int[ records.Length ];
		for ( var i = 0; i < records.Length; i++ )
		{
			bounds[ i ] = new( Vector3.Min( records[ i ].Segment.Start, records[ i ].Segment.End ),
				Vector3.Max( records[ i ].Segment.Start, records[ i ].Segment.End ) );
			next[ i ] = i + 1 < records.Length ? i + 1 : -1;
		}
		return [ new( new( new( -100 ), new( 100 ) ),
			[ new( records.Length == 0 ? -1 : 0, 255, [ -1, -1, -1, -1, -1, -1, -1, -1 ] ) ], next, bounds, records ) ];
	}

	private static GrindCandidateState State()
	{
		var previous = new GrindSurfaceContact( new( 2, 3, 4 ), GrindSurfaceKind.Ledge, true,
			new( 5, 6, 7 ), new( 8, 9, 10 ), new( 11, 12, 13 ), new( 14, 15, 16 ) );
		return new( new( new( 0, .2f, .05f ), new( 2, 0, 0 ), new( 0, -10, 0 ), .1f ), 0f, -10,
			new( 17, 18, 19 ), new( 20, 21, 22 ), new( 23, 24, 25 ), new( 26, 27, 28 ),
			new( new( 29, 30, 31 ), new( 32, 33, 34 ) ), true, true, previous );
	}

	private static MeshQueryInstance Plane( float low, float high )
	{
		var a = new Vector3( -3, 0, low );
		var b = new Vector3( -3, 0, high );
		var c = new Vector3( 3, 0, high );
		var d = new Vector3( 3, 0, low );
		var geometry = new MeshQueryGeometry( [ new( a, b, c, 0x183 ), new( a, c, d, 0x183 ) ], a, c );
		var identity = Matrix4x4.Identity;
		identity.M44 = 0;
		return new( geometry, identity, identity, a, c, -1, 0, 1 );
	}

	private static GrindCandidateAcceptance Create( float angleLimit = 180f )
	{
		var math = new LimbSolverMath( [ -1f / 720f, 1f / 24f, -.5f ],
			[ -1f / 5040f, 1f / 120f, -1f / 6f ], [ 0, 0, 0, 0, 0, 0, 0, MathF.PI / 2 ],
			2 / MathF.PI, MathF.PI / 2, 0, MathF.PI );
		var angle = new LimbSignedAngle( math, .0001f, MathF.PI * 2, 1 / (MathF.PI * 2) );
		var sampler = new GrindEdgeContactSampler( angle, new( 1, 1e-6f, .1f, -.2f, 10, MathF.PI ) );
		var queue = new GrindEdgeSampleQueue( new( [ -4, -3, -2, -1, 0, 1, 2, 3 ], [ 0, 0, 0, 0, 0, 0, 0, 0 ] ),
			new( 1000, -10000, MathF.PI ) );
		var plan = new GrindSurfaceProbePlan( new( 1e-6f, 1e-5f, .09f, .04f, 1.06f, .001f, 6 ) );
		var classifier = new GrindSurfaceClassifier( math, new( .65f, 1e-6f, .9f, .99f,
			-.035f, .035f, -.24f, -.2f, MathF.PI / 2, MathF.PI * 2, .01f ) );
		var contacts = new GrindSurfaceContactQuery( new( plan, classifier ), new( math, .25f, .75f ) );
		var normal = new GrindCandidateNormal( math, 1e-6f, 1.52587890625e-5f,
			MathF.PI / 180, MathF.PI * 2, 1 / (MathF.PI * 2) );
		return new( sampler, queue, contacts, new( .1f ), normal, angle,
			new( -.4f, .9f, 1e-6f, 7, 36, 64, .5f, .35f, 1, .65f, .7f, angleLimit, 1, .1f, 20, .1f, MathF.PI / 180, .0001f ) );
	}
}
