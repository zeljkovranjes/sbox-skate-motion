using System;
using System.Numerics;
using SkateMotion.Core.Animation;
using SkateMotion.Core.Collision;
using SkateMotion.Core.Grinding;
using Xunit;

namespace SboxSkateMotion.Tests;

public class GrindSurfaceInvestigationTests
{
	private static readonly RailSegment Edge = new( new( -2, 0, 0 ), new( 2, 0, 0 ) );
	private static MeshWorldQuery Query() => new( 1e-8f, -.00001f, 2.938735877055719e-39f, 1.52587890625e-5f, 1e-6f );

	[Theory]
	[InlineData( -.04f, .04f, (int)GrindSurfaceKind.ThinRail )]
	[InlineData( -.2f, 0f, (int)GrindSurfaceKind.WideRail )]
	[InlineData( -3f, 0f, (int)GrindSurfaceKind.Ledge )]
	[InlineData( -3f, 3f, (int)GrindSurfaceKind.Blocked )]
	public void GeometryDeterminesSurfaceKind( float low, float high, int kind )
	{
		var investigation = Create();
		Assert.True( Evaluate( investigation, [ Plane( low, high ) ], false, out var result ) );
		Assert.Equal( 6, investigation.ProbeCount );
		Assert.Equal( (GrindSurfaceKind)kind, result.Kind );
	}

	[Fact]
	public void OptionalDownwardHitDoesNotSurviveTheNextSixProbeInvestigation()
	{
		var investigation = Create();
		var geometry = new[] { Plane( -.04f, .04f ) };
		Assert.True( Evaluate( investigation, geometry, true, out var withDownward ) );
		Assert.Equal( 7, investigation.ProbeCount );
		Assert.Equal( 0u, withDownward.Flags & 0x04000000u );
		Assert.True( Evaluate( investigation, geometry, false, out var withoutDownward ) );
		Assert.Equal( 6, investigation.ProbeCount );
		Assert.Equal( 0x04000000u, withoutDownward.Flags & 0x04000000u );
	}

	[Fact]
	public void DegenerateEdgeReturnsInactiveInsteadOfPreviousClassification()
	{
		var investigation = Create();
		Evaluate( investigation, [ Plane( -3, 3 ) ], true, out _ );
		Assert.False( investigation.TryEvaluate( Query(), [], new( Vector3.Zero, Vector3.UnitY ),
			Vector3.UnitY, .3f, false, Vector3.Zero, -1, uint.MaxValue, out var inactive ) );
		Assert.Equal( 0, investigation.ProbeCount );
		Assert.Equal( 0x80000000u, inactive.Flags );
		Assert.Equal( Vector3.UnitY, inactive.Center );
	}

	[Fact]
	public void EmptyWorldClearsPreviouslyCollectedHits()
	{
		var investigation = Create();
		Evaluate( investigation, [ Plane( -3, 3 ) ], true, out _ );
		Assert.True( Evaluate( investigation, [], false, out var empty ) );
		Assert.Equal( GrindSurfaceKind.ThinRail, empty.Kind );
		Assert.Equal( 11u, empty.AudioSurface );
	}

	[Theory]
	[InlineData( 7, (int)GrindSurfaceKind.Blocked )]
	[InlineData( 8, (int)GrindSurfaceKind.ThinRail )]
	public void UsesSuppliedFilterAndZeroRequestExclusion( int filter, int kind )
	{
		Assert.True( Create().TryEvaluate( Query(), [ Plane( -3, 3 ) with { Filter = 7, Exclusion = uint.MaxValue } ],
			Edge, Vector3.UnitY, .3f, false, Vector3.UnitY, filter, 0, out var result ) );
		Assert.Equal( (GrindSurfaceKind)kind, result.Kind );
	}

	private static bool Evaluate( GrindSurfaceInvestigation investigation, MeshQueryInstance[] instances,
		bool downward, out GrindSurfaceClassification result ) => investigation.TryEvaluate( Query(), instances,
			Edge, Vector3.UnitY, .3f, downward, Vector3.UnitY, -1, 0, out result );

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

	private static GrindSurfaceInvestigation Create()
	{
		var math = new LimbSolverMath( [ -1f / 720f, 1f / 24f, -.5f ],
			[ -1f / 5040f, 1f / 120f, -1f / 6f ], [ 0, 0, 0, 0, 0, 0, 0, MathF.PI / 2 ],
			2 / MathF.PI, MathF.PI / 2, 0, MathF.PI );
		var plan = new GrindSurfaceProbePlan( new( 1e-6f, 1e-5f, .09f, .04f, 1.06f, .001f, 6 ) );
		var classifier = new GrindSurfaceClassifier( math, new( .65f, 1e-6f, .9f, .99f,
			-.035f, .035f, -.24f, -.2f, MathF.PI / 2, MathF.PI * 2, .01f ) );
		return new( plan, classifier );
	}
}
