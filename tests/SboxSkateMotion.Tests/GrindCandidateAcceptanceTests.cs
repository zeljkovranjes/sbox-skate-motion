using System;
using System.Numerics;
using SkateMotion.Core.Animation;
using SkateMotion.Core.Collision;
using SkateMotion.Core.Grinding;
using Xunit;

namespace SboxSkateMotion.Tests;

public class GrindCandidateAcceptanceTests
{
	private static readonly GrindEdgeRecord Edge = new( new( new( -3, 0, 0 ), new( 3, 0, 0 ) ), 1, 0 );
	private static MeshWorldQuery Query() => new( 1e-8f, -.00001f, 2.938735877055719e-39f, 1.52587890625e-5f, 1e-6f );
	private static GrindCandidateInput Input => new( Vector3.UnitX, new( 0, 1, 1 ), new( 0, 1, 1 ), .5f, .3f, .6f, .2f, -1 );

	[Fact]
	public void AcceptedContactUpdatesQuantizedTimeVelocityAndNormals()
	{
		var state = State();
		var solver = Create();
		Assert.True( solver.TryAccept( [ Edge ], 1, Input, Query(), [ Plane( -.04f, .04f ) ], ref state ) );
		Assert.Equal( 2, state.SampleIndex );
		Assert.Equal( .2f, state.Time );
		Assert.InRange( Vector3.Distance( new Vector3( 2, 0, -.25f ), state.Trajectory.Velocity ), 0, .00001f );
		Assert.True( state.IsActive );
		Assert.False( state.IsLedge );
		Assert.Equal( Vector3.UnitY, state.EdgeUp );
		Assert.Equal( 1, solver.QueryCount );
		Assert.Equal( 0, solver.RemainingCount );
	}

	[Fact]
	public void BlockedContactPreservesPriorActiveAndSideState()
	{
		var state = State();
		var previous = state;
		var solver = Create();
		Assert.False( solver.TryAccept( [ Edge ], 1, Input, Query(), [ Plane( -3, 3 ) ], ref state ) );
		Assert.Equal( GrindSurfaceKind.Blocked, state.Contact.Kind );
		Assert.Equal( previous.Side, state.Side );
		Assert.Equal( previous.HighSide, state.HighSide );
		Assert.Equal( previous.IsLedge, state.IsLedge );
		Assert.Equal( previous.IsActive, state.IsActive );
		Assert.Equal( previous.Trajectory, state.Trajectory );
		Assert.Equal( previous.Normal, state.Normal );
		Assert.Equal( Edge.Segment, state.Edge );
	}

	[Fact]
	public void VerticalRejectionStoresEdgeBeforeQueryingTheMap()
	{
		var state = State();
		var previous = state.Contact;
		var solver = Create();
		Assert.False( solver.TryAccept( [ Edge ], 1, Input with { Approach = new( 0, 8, 0 ) },
			Query(), [ Plane( -.04f, .04f ) ], ref state ) );
		Assert.Equal( 0, solver.QueryCount );
		Assert.Equal( previous, state.Contact );
		Assert.Equal( Edge.Segment, state.Edge );
	}

	[Fact]
	public void HorizontalRejectionRetainsNewContactSideState()
	{
		var state = State();
		var previous = state;
		var solver = Create();
		Assert.False( solver.TryAccept( [ Edge ], 1, Input with { Approach = new( 0, 0, 9 ) },
			Query(), [ Plane( -.04f, .04f ) ], ref state ) );
		Assert.Equal( 1, solver.QueryCount );
		Assert.Equal( GrindSurfaceKind.ThinRail, state.Contact.Kind );
		Assert.False( state.IsLedge );
		Assert.Equal( state.Contact.Side, state.Side );
		Assert.NotEqual( previous.Side, state.Side );
		Assert.Equal( previous.Trajectory, state.Trajectory );
	}

	[Fact]
	public void LowPointRejectionDoesNotReplacePriorEdge()
	{
		var state = State();
		state.Trajectory = state.Trajectory with { Position = new( 0, 2, .05f ) };
		var previous = state;
		var solver = Create();
		Assert.False( solver.TryAccept( [ Edge ], 1, Input, Query(), [ Plane( -.04f, .04f ) ], ref state ) );
		Assert.Equal( previous, state );
		Assert.Equal( 0, solver.QueryCount );
	}

	[Theory]
	[InlineData( .0499f, false )]
	[InlineData( .05f, false )]
	[InlineData( .0501f, true )]
	public void SeparationMustBeStrictlyBelowThreshold( float threshold, bool expected )
	{
		var state = State();
		Assert.Equal( expected, Create().TryAccept( [ Edge ], 1, Input with { SeparationThreshold = threshold },
			Query(), [ Plane( -.04f, .04f ) ], ref state ) );
	}

	[Fact]
	public void ZeroCorrectionAngleLimitRejectsAnActualDirectionChange()
	{
		var state = State();
		var previous = state.Trajectory;
		Assert.False( Create( 0 ).TryAccept( [ Edge ], 1, Input, Query(), [ Plane( -.04f, .04f ) ], ref state ) );
		Assert.Equal( previous, state.Trajectory );
	}

	[Fact]
	public void RejectedBlockedCandidateIsFollowedByAnotherCandidate()
	{
		var state = State();
		state.Trajectory = state.Trajectory with { Velocity = new( 8, 0, 0 ) };
		var second = Edge with { Segment = new( new( -3, 0, .3f ), new( 3, 0, .3f ) ) };
		var solver = Create();
		Assert.True( solver.TryAccept( [ Edge, second ], 2, Input, Query(),
			[ Plane( -.12f, .12f ), Plane( .26f, .34f ) ], ref state ) );
		Assert.Equal( 2, solver.QueryCount );
		Assert.Equal( second.Segment, state.Edge );
		Assert.Equal( GrindSurfaceKind.ThinRail, state.Contact.Kind );
	}

	[Fact]
	public void EmptyRebuildDropsPendingSamplesWithoutResettingState()
	{
		var state = State();
		var solver = Create();
		Assert.True( solver.TryAccept( [ Edge, Edge ], 2, Input, Query(), [ Plane( -.04f, .04f ) ], ref state ) );
		Assert.Equal( 1, solver.RemainingCount );
		var previous = state;
		Assert.False( solver.TryAccept( [], 0, Input, Query(), [], ref state ) );
		Assert.Equal( previous, state );
		Assert.Equal( 0, solver.RemainingCount );
		Assert.Equal( 0, solver.SourceEdgeCount );
		Assert.Equal( 0, solver.QueryCount );
	}

	[Fact]
	public void LedgeContactUpdatesLedgeFlag()
	{
		var state = State() with { IsLedge = false };
		Assert.True( Create().TryAccept( [ Edge ], 1, Input, Query(), [ Plane( -3, 0 ) ], ref state ) );
		Assert.True( state.IsLedge );
		Assert.Equal( GrindSurfaceKind.Ledge, state.Contact.Kind );
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
