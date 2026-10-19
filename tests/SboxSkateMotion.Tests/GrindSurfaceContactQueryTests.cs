using System;
using System.Numerics;
using SkateMotion.Core.Animation;
using SkateMotion.Core.Collision;
using SkateMotion.Core.Grinding;
using Xunit;

namespace SboxSkateMotion.Tests;

public class GrindSurfaceContactQueryTests
{
	private static readonly RailSegment Edge = new( new( -2, 0, 0 ), new( 2, 0, 0 ) );
	private static MeshWorldQuery Query() => new( 1e-8f, -.00001f, 2.938735877055719e-39f, 1.52587890625e-5f, 1e-6f );
	private static GrindSurfaceContact Previous => new( new( 1, 2, 3 ), GrindSurfaceKind.Ledge, false,
		new( 4, 5, 6 ), new( 7, 8, 9 ), new( 10, 11, 12 ), new( 13, 14, 15 ) );

	[Theory]
	[InlineData( 10f, 0f, (int)GrindSurfaceKind.ThinRail )]
	[InlineData( 0f, 10f, (int)GrindSurfaceKind.Blocked )]
	[InlineData( 0f, 0f, (int)GrindSurfaceKind.Blocked )]
	public void CandidatePointCentersProbesIndependentlyOfApproachReference( float point, float approach, int kind )
	{
		var result = Create( out _ ).Evaluate( Query(), [ Plane( 3 ) ], Edge,
			new( approach, 1, 1 ), new( point, 1, 0 ), .3f, -1, Previous );
		Assert.Equal( (GrindSurfaceKind)kind, result.Kind );
		Assert.False( result.IsFallback );
	}

	[Fact]
	public void FailedInvestigationPreservesEveryPreviousVector()
	{
		var result = Create( out _ ).Evaluate( Query(), [ Plane( 3 ) ], new( Vector3.Zero, Vector3.UnitY ),
			Vector3.UnitZ, Vector3.UnitY, .3f, -1, Previous );
		Assert.Equal( Previous with { Kind = GrindSurfaceKind.Blocked, IsFallback = true }, result );
	}

	[Theory]
	[InlineData( -1f )]
	[InlineData( 1f )]
	public void ApproachReferenceSelectsTheThinRailDirection( float side )
	{
		var result = Create( out _ ).Evaluate( Query(), [ Plane( .04f ) ], Edge,
			new( 0, 1, side ), Vector3.UnitY, .3f, -1, Previous );
		Assert.Equal( GrindSurfaceKind.ThinRail, result.Kind );
		Assert.Equal( (int)side, MathF.Sign( result.FirstDirection.Z ) );
		Assert.Equal( -Vector3.UnitZ, result.Side );
	}

	[Fact]
	public void CallerUsesSixProbesAfterAnOptionalInvestigation()
	{
		var contactQuery = Create( out var investigation );
		var instances = new[] { Plane( .04f ) };
		investigation.TryEvaluate( Query(), instances, Edge, Vector3.UnitY, .3f,
			true, Vector3.UnitY, -1, uint.MaxValue, out _ );
		Assert.Equal( 7, investigation.ProbeCount );
		var result = contactQuery.Evaluate( Query(), instances, Edge, Vector3.UnitZ, Vector3.UnitY, .3f, -1, Previous );
		Assert.Equal( 6, investigation.ProbeCount );
		Assert.False( result.IsFallback );
		Assert.Equal( GrindSurfaceKind.ThinRail, result.Kind );
	}

	[Fact]
	public void SuccessfulQueryReplacesPriorFallbackState()
	{
		var contactQuery = Create( out _ );
		var failed = contactQuery.Evaluate( Query(), [], new( Vector3.Zero, Vector3.Zero ),
			Vector3.UnitZ, Vector3.UnitY, .3f, -1, Previous );
		var result = contactQuery.Evaluate( Query(), [ Plane( .04f ) ], Edge,
			Vector3.UnitZ, Vector3.UnitY, .3f, -1, failed );
		Assert.False( result.IsFallback );
		Assert.Equal( GrindSurfaceKind.ThinRail, result.Kind );
		Assert.NotEqual( failed.Normal, result.Normal );
	}

	private static MeshQueryInstance Plane( float width )
	{
		var a = new Vector3( -3, 0, -width );
		var b = new Vector3( -3, 0, width );
		var c = new Vector3( 3, 0, width );
		var d = new Vector3( 3, 0, -width );
		var geometry = new MeshQueryGeometry( [ new( a, b, c, 0x183 ), new( a, c, d, 0x183 ) ], a, c );
		var identity = Matrix4x4.Identity;
		identity.M44 = 0;
		return new( geometry, identity, identity, a, c, -1, 0, 1 );
	}

	private static GrindSurfaceContactQuery Create( out GrindSurfaceInvestigation investigation )
	{
		var math = new LimbSolverMath( [ -1f / 720f, 1f / 24f, -.5f ],
			[ -1f / 5040f, 1f / 120f, -1f / 6f ], [ 0, 0, 0, 0, 0, 0, 0, MathF.PI / 2 ],
			2 / MathF.PI, MathF.PI / 2, 0, MathF.PI );
		var plan = new GrindSurfaceProbePlan( new( 1e-6f, 1e-5f, .09f, .04f, 1.06f, .001f, 6 ) );
		var classifier = new GrindSurfaceClassifier( math, new( .65f, 1e-6f, .9f, .99f,
			-.035f, .035f, -.24f, -.2f, MathF.PI / 2, MathF.PI * 2, .01f ) );
		investigation = new( plan, classifier );
		return new( investigation, new( math, .25f, .75f ) );
	}
}
