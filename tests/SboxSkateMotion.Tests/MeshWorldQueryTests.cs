using System.Numerics;
using SkateMotion.Core.Collision;
using Xunit;

namespace SboxSkateMotion.Tests;

public class MeshWorldQueryTests
{
	private static readonly MeshQueryFilter Both = new( 0, 0, 0, 0, true, true );
	private static MeshWorldQuery Query() => new( 1e-8f, -.00001f, 2.938735877055719e-39f, 1.52587890625e-5f, 1e-6f );
	private static MeshQueryRequest Request => new( new( .25f, .25f, 1f ), new( .25f, .25f, -1f ), 0f, -1, 0, Both );

	[Fact]
	public void StationaryRoundedQueryReturnsTheCanonicalMiss()
	{
		var result = Query().Evaluate( Request with { End = Request.Start, Radius = .1f }, [ Instance() ] );
		Assert.Equal( 0u, result.HitCount );
		Assert.Equal( float.MaxValue, result.Fraction );
		Assert.Equal( Vector3.Zero, result.Position );
		Assert.Equal( Vector3.UnitY, result.Normal );
		Assert.Equal( 0u, result.Identity );
		Assert.Equal( 0f, result.Transform.M44 );
	}

	[Theory]
	[InlineData( 0f, 0u )]
	[InlineData( 7.62939453125e-6f, 0u )]
	[InlineData( 1.52587890625e-5f, 0u )]
	[InlineData( 1.5258790881489404e-5f, 1u )]
	public void SegmentMovementMustStrictlyExceedThreshold( float movement, uint hits )
	{
		var request = Request with { Start = new( .25f, .25f, 0f ), End = new( .25f, .25f, -movement ), Radius = .1f };
		Assert.Equal( hits, Query().Evaluate( request, [ Instance() ] ).HitCount );
	}

	[Theory]
	[InlineData( -1, 7, 1u )]
	[InlineData( 7, -1, 1u )]
	[InlineData( 7, 7, 1u )]
	[InlineData( 7, 0, 0u )]
	public void InstanceFilterUsesBothWildcardDirections( int queryFilter, int instanceFilter, uint hits )
	{
		Assert.Equal( hits, Query().Evaluate( Request with { Filter = queryFilter },
			[ Instance() with { Filter = instanceFilter } ] ).HitCount );
	}

	[Fact]
	public void ExclusionStillAppliesWhenFilterIsWildcard()
	{
		Assert.Equal( 0u, Query().Evaluate( Request with { Exclusion = 2 },
			[ Instance() with { Exclusion = 3 } ] ).HitCount );
		Assert.Equal( 1u, Query().Evaluate( Request with { Exclusion = 4 },
			[ Instance() with { Exclusion = 3 } ] ).HitCount );
	}

	[Fact]
	public void InstanceBoundsAreTestedInLocalSpace()
	{
		var instance = Instance( new( 10f, 20f, 30f ) );
		var request = Request with { Start = new( 10.25f, 20.25f, 31f ), End = new( 10.25f, 20.25f, 29f ) };
		var result = Query().Evaluate( request, [ instance ] );
		Assert.Equal( 1u, result.HitCount );
		Assert.Equal( new Vector3( 10.25f, 20.25f, 30f ), result.Position );
		Assert.Equal( 0u, Query().Evaluate( request, [ instance with { Minimum = new( 2f ), Maximum = new( 3f ) } ] ).HitCount );
	}

	[Fact]
	public void NearestInstanceWinsAndAllAcceptedInstancesAreCounted()
	{
		var far = Instance() with { Identity = 7 };
		var near = Instance( new( 0f, 0f, .5f ) ) with { Identity = 9 };
		foreach ( var instances in new[] { new[] { far, near }, new[] { near, far } } )
		{
			var result = Query().Evaluate( Request, instances );
			Assert.Equal( 9u, result.Identity );
			Assert.Equal( .25f, result.Fraction );
			Assert.Equal( 2u, result.HitCount );
		}
	}

	[Fact]
	public void EqualInstancesKeepTheFirstIdentity()
	{
		var result = Query().Evaluate( Request, [ Instance() with { Identity = 555 }, Instance() with { Identity = 777 } ] );
		Assert.Equal( 555u, result.Identity );
		Assert.Equal( 2u, result.HitCount );
	}

	[Fact]
	public void OriginalMeshBoundsCanRejectAnOtherwiseAcceptedTangentOverlap()
	{
		var request = Request with { Start = new( 0f, .25f, 0f ), End = new( -1.5258790881489404e-5f, .25f, 0f ), Radius = .1f };
		Assert.Equal( 0u, Query().Evaluate( request, [ Instance() ] ).HitCount );
	}

	[Fact]
	public void SurfaceMaskPropagatesThroughWorldDispatch()
	{
		var result = Query().Evaluate( Request with { Surfaces = Both with { Surface0 = 8 } }, [ Instance() ] );
		Assert.Equal( 0u, result.HitCount );
		Assert.Equal( float.MaxValue, result.Fraction );
	}

	[Fact]
	public void RepeatedQueriesDoNotRetainPreviousContacts()
	{
		var query = Query();
		Assert.Equal( 1u, query.Evaluate( Request, [ Instance() ] ).HitCount );
		Assert.Equal( 0u, query.Evaluate( Request, [] ).HitCount );
	}

	private static MeshQueryInstance Instance( Vector3 translation = default )
	{
		var geometry = new MeshQueryGeometry( [ new( Vector3.Zero, Vector3.UnitX, Vector3.UnitY, 3 ) ],
			Vector3.Zero, new( 1f, 1f, 0f ) );
		return new( geometry, Matrix4x4.CreateTranslation( translation ), Matrix4x4.CreateTranslation( -translation ),
			geometry.Minimum, geometry.Maximum, -1, 0, 123 );
	}
}
