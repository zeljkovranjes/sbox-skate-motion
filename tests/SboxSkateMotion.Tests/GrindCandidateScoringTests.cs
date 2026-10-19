#nullable enable

using System;
using System.Numerics;
using SkateMotion.Core.Animation;
using SkateMotion.Core.Collision;
using Xunit;

namespace SboxSkateMotion.Tests;

public class GrindCandidateScoringTests
{
	private static GrindScoringCandidate Candidate => new( new( 0, 1, 0, 0 ), Vector4.Zero, new( 1, 0, 0, 0 ),
		new( .5f, 0, 0, 0 ), new( new( 0, 2, 0, 0 ), Vector4.Zero, Vector4.Zero, new( 7 ) ), 2, 0, .1f, 0f );
	private static GrindScoringContext Context => new( 2, false, 0, 0, 1f, new( 0, 1, 0, 0 ), new( 1, 0, 0, 0 ), new( 0, 1, 0, 4 ), 3f );
	private static GrindCandidateInput Input => new( Vector3.Zero, Vector3.Zero, Vector3.Zero, .5f, .3f, .6f, .2f, -1 );
	private static MeshWorldQuery Query() => new( 1e-8f, -.00001f, 2.938735877055719e-39f, 1.52587890625e-5f, 1e-6f );
	private static GrindEdgeSelectionCurve Flat( float value ) => new( [ 0, 1, 2, 3, 4, 5, 6, 7 ], [ value, value, value, value, value, value, value, value ] );

	private static void Run( GrindCandidateScoring pass, GrindScoringCandidate[] records, short count,
		int minimum = 0, GrindScoringContext? context = null, MeshQueryInstance[]? instances = null )
	{
		var production = new GrindCandidateProductionState();
		var height = -10f;
		pass.Evaluate( records, count, minimum, context ?? Context, [], Input, Query(), instances ?? [], ref production, ref height );
	}

	[Theory]
	[InlineData( -1 )]
	[InlineData( 0 )]
	public void NonpositiveCountsPreserveSlotsAndReportNoValidTimes( short count )
	{
		var pass = Create();
		pass.Scores[ 0 ] = 17;
		pass.Biases[ 0 ] = -12;
		pass.RejectionFlags[ 0 ] = true;
		Run( pass, [ Candidate ], count );
		Assert.Equal( 17, pass.Scores[ 0 ] );
		Assert.Equal( -12, pass.Biases[ 0 ] );
		Assert.True( pass.RejectionFlags[ 0 ] );
		Assert.True( pass.AllTimesInvalid );
		Assert.Equal( 0, pass.QueryCount );
	}

	[Theory]
	[InlineData( 0 )]
	[InlineData( 1 )]
	[InlineData( 2 )]
	[InlineData( 3 )]
	public void InvalidTimeClearsActiveScoresButPreservesCandidateAndFrame( int lane )
	{
		var time = Vector4.Zero;
		time[ lane ] = -1;
		var candidate = Candidate with { Time = time };
		var records = new[] { candidate };
		var pass = Create();
		pass.Scores[ 0 ] = 17;
		pass.Biases[ 0 ] = -12;
		pass.RejectionFlags[ 0 ] = true;
		pass.Frames[ 0 ] = new( new( 1 ), new( 2 ), new( 3 ) );
		Run( pass, records, 1 );
		Assert.Equal( 0, pass.Scores[ 0 ] );
		Assert.Equal( 0, pass.Biases[ 0 ] );
		Assert.False( pass.RejectionFlags[ 0 ] );
		Assert.Equal( candidate, records[ 0 ] );
		Assert.Equal( new Vector4( 3 ), pass.Frames[ 0 ].Position );
		Assert.True( pass.AllTimesInvalid );
	}

	[Fact]
	public void ValidTimeClearsAllInvalidFlagEvenWhenSampleGateRejects()
	{
		var pass = Create();
		Run( pass, [ Candidate ], 1, minimum: 3 );
		Assert.False( pass.AllTimesInvalid );
		Assert.Equal( 0, pass.Scores[ 0 ] );
		Assert.Equal( 0, pass.QueryCount );
	}

	[Fact]
	public void PositionAndVelocityUseDistinctTimeSourcesAndRetainFourthLanes()
	{
		var pass = Create();
		var records = new[] { Candidate with { Axis = new( 0, 1, 0, 9 ), Trajectory = new(
			new( 1, 2, 3, 4 ), new( 2, 4, 6, 8 ), new( 0, -10, 0, 0 ), new( 7 ) ) } };
		Run( pass, records, 1 );
		Assert.Equal( new Vector4( 2, 2.75f, 6, 8 ), pass.Frames[ 0 ].Position );
		Assert.Equal( new Vector4( 2, 2, 6, 8 ), pass.Frames[ 0 ].Velocity );
		Assert.Equal( new Vector4( 0, 1, 0, 9 ), pass.Frames[ 0 ].Direction );
	}

	[Theory]
	[InlineData( 0, 1f, false )]
	[InlineData( 6, -19f, true )]
	[InlineData( 7, -9f, true )]
	[InlineData( 8, -9f, false )]
	public void KindBonusesHaveDistinctDirectionCopyBehavior( int kind, float expected, bool copies )
	{
		var pass = Create();
		var records = new[] { Candidate with { Kind = kind } };
		Run( pass, records, 1 );
		Assert.Equal( expected, pass.Scores[ 0 ] );
		Assert.Equal( copies ? Context.Projection : Candidate.Axis, records[ 0 ].Direction );
	}

	[Fact]
	public void FirstUpdateBonusOnlyAppliesToFirstCandidate()
	{
		var pass = Create();
		Run( pass, [ Candidate, Candidate ], 2, context: Context with { UpdateCount = 1 } );
		Assert.Equal( 41f, pass.Scores[ 0 ] );
		Assert.Equal( 1f, pass.Scores[ 1 ] );
	}

	[Theory]
	[InlineData( .49f, 1f, false )]
	[InlineData( .5f, 4f, true )]
	public void SpecialOrientationCanClearRetainedAcceptanceAndReplacePositiveScore( float orientation, float expected, bool accepted )
	{
		var curve = new GrindEdgeSelectionCurve( [ 0, 1, 2, 3, 4, 5, 6, 7 ], [ 0, 1, 2, 3, 4, 5, 6, 7 ] );
		var pass = Create( distanceCurve: curve );
		var production = new GrindCandidateProductionState( default, false, true );
		var height = -10f;
		pass.Evaluate( [ Candidate ], 1, 0, Context with { Special = true, Orientation = orientation },
			[], Input, Query(), [], ref production, ref height );
		Assert.Equal( expected, pass.Scores[ 0 ] );
		Assert.Equal( accepted, production.Accepted );
	}

	[Fact]
	public void NegativeFallbackRetainsSeparatePenaltyAndUsesSuppliedUpFourthLane()
	{
		var pass = Create( distanceCurve: Flat( -2 ) );
		var records = new[] { Candidate };
		Run( pass, records, 1, instances: [ Floor() ] );
		Assert.Equal( -1f, pass.Scores[ 0 ] );
		Assert.Equal( -90f, pass.Biases[ 0 ] );
		Assert.False( pass.RejectionFlags[ 0 ] );
		Assert.Equal( new Vector4( 0, 1, 0, 3 ), records[ 0 ].Direction );
		Assert.Equal( Candidate.Trajectory, records[ 0 ].Trajectory );
	}

	[Fact]
	public void MultipleRejectionsEachQueryGeometryAndKeepBiasSeparateFromScore()
	{
		var pass = Create();
		var records = new[] { Candidate, Candidate };
		var production = new GrindCandidateProductionState();
		var height = -10f;
		pass.Evaluate( records, 2, 0, Context, [], Input, Query(), [ Floor() ], ref production, ref height );
		Assert.Equal( 2, pass.QueryCount );
		Assert.Equal( 2f, height );
		for ( var i = 0; i < 2; i++ )
		{
			Assert.Equal( 1f, pass.Scores[ i ] );
			Assert.Equal( 10f, pass.Biases[ i ] );
			Assert.True( pass.RejectionFlags[ i ] );
			Assert.Equal( 2.5f, records[ i ].Trajectory.Velocity.Y );
			Assert.Equal( Vector4.Zero, pass.Frames[ i ].Velocity );
		}
	}

	[Fact]
	public void FollowingPassClearsOnlyItsActiveSlots()
	{
		var pass = Create();
		Run( pass, [ Candidate, Candidate ], 2, instances: [ Floor() ] );
		Run( pass, [ Candidate with { Time = new( -1 ) } ], 1 );
		Assert.Equal( 0, pass.Scores[ 0 ] );
		Assert.Equal( 0, pass.Biases[ 0 ] );
		Assert.False( pass.RejectionFlags[ 0 ] );
		Assert.Equal( 1, pass.Scores[ 1 ] );
		Assert.Equal( 10, pass.Biases[ 1 ] );
		Assert.True( pass.RejectionFlags[ 1 ] );
	}

	private static MeshQueryInstance Floor()
	{
		var a = new Vector3( -3, 0, -3 );
		var b = new Vector3( -3, 0, 3 );
		var c = new Vector3( 3, 0, 3 );
		var d = new Vector3( 3, 0, -3 );
		var geometry = new MeshQueryGeometry( [ new( a, b, c, 0 ), new( a, c, d, 0 ) ], a, c );
		var identity = Matrix4x4.Identity;
		identity.M44 = 0;
		return new( geometry, identity, identity, a, c, -1, 0, 1 );
	}

	private static GrindCandidateScoring Create( GrindEdgeSelectionCurve? distanceCurve = null )
	{
		var math = new LimbSolverMath( [ -1f / 720f, 1f / 24f, -.5f ],
			[ -1f / 5040f, 1f / 120f, -1f / 6f ], [ 0, 0, 0, 0, 0, 0, 0, MathF.PI / 2 ],
			2 / MathF.PI, MathF.PI / 2, 0, MathF.PI );
		var angle = new LimbSignedAngle( math, .0001f, MathF.PI * 2, 1 / (MathF.PI * 2) );
		var sampler = new GrindEdgeContactSampler( angle, new( 1, 1e-6f, .1f, -.2f, 10, MathF.PI ) );
		var queue = new GrindEdgeSampleQueue( Flat( 0 ), new( 1000, -10000, MathF.PI ) );
		var plan = new GrindSurfaceProbePlan( new( 1e-6f, 1e-5f, .09f, .04f, 1.06f, .001f, 6 ) );
		var classifier = new GrindSurfaceClassifier( math, new( .65f, 1e-6f, .9f, .99f,
			-.035f, .035f, -.24f, -.2f, MathF.PI / 2, MathF.PI * 2, .01f ) );
		var contacts = new GrindSurfaceContactQuery( new( plan, classifier ), new( math, .25f, .75f ) );
		var normal = new GrindCandidateNormal( math, 1e-6f, 1.52587890625e-5f,
			MathF.PI / 180, MathF.PI * 2, 1 / (MathF.PI * 2) );
		var correction = new GrindContactCorrection( .1f );
		var acceptance = new GrindCandidateAcceptance( sampler, queue, contacts, correction, normal, angle,
			new( -.4f, .9f, 1e-6f, 7, 36, 64, .5f, .35f, 1, .65f, .7f, 180, 1, .1f, 20, .1f, MathF.PI / 180, .0001f ) );
		var curve = distanceCurve ?? Flat( 0 );
		var rejection = new GrindCandidateRejection( new( .5f, .5f, 0, 10, 10, 100, .01f, 0, .5f, 100 ),
			new( [ 0, 1, 2, 3 ], [ .5f, .5f, .5f, .5f ] ), correction );
		return new( new( new(), acceptance, 100 ), new( 1e-6f, 100, 3, curve ), rejection,
			new( .1f, 1e-6f, 200, 20, 40, 500, .1f, .5f, -500, -20, -10 ), Flat( 0 ), Flat( 0 ), curve );
	}
}
