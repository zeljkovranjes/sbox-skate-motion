using System.Numerics;
using SkateMotion.Core.Collision;
using Xunit;

namespace SboxSkateMotion.Tests;

public class MeshTriangleQueryTests
{
	private static readonly MeshTriangle Triangle = new( Vector3.Zero, Vector3.UnitX, Vector3.UnitY, 3 );
	private static readonly MeshQueryFilter Both = new( 0, 0, 0, 0, true, true );
	private static MeshContact Empty => new( Vector3.Zero, Vector3.Zero, Matrix4x4.Identity, float.MaxValue, 0, 0, 0 );
	private static MeshTriangleQuery Query() => new( 1e-8f, -.00001f, 2.938735877055719e-39f );

	[Fact]
	public void RoundedEdgeKeepsTheFaceNormalAndFeaturePosition()
	{
		var result = Empty;
		Assert.True( Evaluate( new( .5f, -.05f, 1f ), .1f, Triangle, Both, ref result ) );
		Assert.Equal( new Vector3( .5f, 0f, 0f ), result.Position );
		Assert.Equal( Vector3.UnitZ, result.Normal );
		Assert.Equal( .4566987156867981f, result.Fraction );
		Assert.Equal( (ushort)3, result.Material );
		Assert.Equal( 123u, result.Identity );
	}

	[Theory]
	[InlineData( false, false, false )]
	[InlineData( true, false, true )]
	[InlineData( false, true, false )]
	[InlineData( true, true, true )]
	public void FrontApproachHonorsDirectionFlags( bool front, bool back, bool accepted )
	{
		var result = Empty;
		Assert.Equal( accepted, Evaluate( new( .25f, .25f, 1f ), .1f, Triangle,
			Both with { Front = front, Back = back }, ref result ) );
		Assert.Equal( accepted ? 1u : 0u, result.HitCount );
		if ( !accepted ) Assert.Equal( Empty, result );
	}

	[Theory]
	[InlineData( 0, 1u, 0u, 0u, 0u )]
	[InlineData( 32, 0u, 1u, 0u, 0u )]
	[InlineData( 64, 0u, 0u, 1u, 0u )]
	[InlineData( 127, 0u, 0u, 0u, 0x80000000u )]
	[InlineData( 128, 0u, 0u, 0u, 2u )]
	[InlineData( 4095, 0u, 0u, 0u, 0x80000000u )]
	public void ExcludedMaterialPreservesAllContactData( ushort material, uint a, uint b, uint c, uint d )
	{
		var result = Empty;
		Assert.False( Evaluate( new( .25f, .25f, 1f ), 0f, Triangle with { Material = material },
			new( a, b, c, d, true, true ), ref result ) );
		Assert.Equal( Empty, result );
	}

	[Fact]
	public void LastFilterWordIsSharedBetweenAudioAndPhysicalRanges()
	{
		var filter = Both with { Surface3 = 2u };
		Assert.True( filter.Excludes( 97 ) );
		Assert.True( filter.Excludes( 128 ) );
		Assert.False( filter.Excludes( 3 ) );
	}

	[Fact]
	public void NearerTriangleReplacesContactWhileEqualTriangleKeepsFirstIdentity()
	{
		var result = Empty;
		var start = new Vector3( .25f, .25f, 1f );
		Assert.True( Evaluate( start, 0f, Triangle, Both, ref result ) );
		var raised = Triangle with { A = Triangle.A + Vector3.UnitZ * .5f,
			B = Triangle.B + Vector3.UnitZ * .5f, C = Triangle.C + Vector3.UnitZ * .5f, Material = 7 };
		Assert.True( Evaluate( start, 0f, raised, Both, ref result ) );
		Assert.Equal( .25f, result.Fraction );
		Assert.Equal( (ushort)7, result.Material );
		Assert.True( Evaluate( start, 0f, raised with { Material = 9 }, Both, ref result ) );
		Assert.Equal( (ushort)7, result.Material );
		Assert.Equal( 3u, result.HitCount );
	}

	[Fact]
	public void AcceptedFartherTriangleIncrementsCountWithoutReplacingSeed()
	{
		var seed = new MeshContact( new( 7f ), new( 8f ), Matrix4x4.CreateScale( 2f ), .25f, 19, 42, 5 );
		var result = seed;
		Assert.True( Evaluate( new( .25f, .25f, 1f ), 0f, Triangle, Both, ref result ) );
		Assert.Equal( seed with { HitCount = 6 }, result );
	}

	[Fact]
	public void MirroringReversesTheFaceAndSelectsBackApproach()
	{
		var result = Empty;
		var transform = Matrix4x4.CreateScale( -1f, 1f, 1f ) * Matrix4x4.CreateTranslation( 3.25f, -5.5f, 2.75f );
		Assert.True( Query().Evaluate( new( 3f, -5.25f, 3.75f ), new( 0f, 0f, -2f ), .1f,
			new( -10f ), new( 10f ), Triangle, transform, 17, Both with { Front = false }, ref result ) );
		Assert.Equal( -Vector3.UnitZ, result.Normal );
		Assert.Equal( new Vector3( 3f, -5.25f, 2.75f ), result.Position );
		Assert.Equal( transform, result.Transform );
		Assert.Equal( 17u, result.Identity );
	}

	[Fact]
	public void LocalBoundsRejectBeforeChangingTheResult()
	{
		var result = Empty;
		Assert.False( Query().Evaluate( new( .25f, .25f, 1f ), new( 0f, 0f, -2f ), 0f,
			new( 2f ), new( 3f ), Triangle, Matrix4x4.Identity, 123, Both, ref result ) );
		Assert.Equal( Empty, result );
	}

	[Theory]
	[InlineData( -.000001f, 0f )]
	[InlineData( 1.000001f, 1f )]
	public void AcceptedTolerantFractionIsClampedBeforeSelection( float startZ, float expected )
	{
		var result = Empty;
		Assert.True( Query().Evaluate( new( .25f, .25f, startZ ), new( 0f, 0f, -1f ), 0f,
			new( -10f ), new( 10f ), Triangle, Matrix4x4.Identity, 123, Both, ref result ) );
		Assert.Equal( expected, result.Fraction );
		Assert.Equal( 1u, result.HitCount );
	}

	[Fact]
	public void AcceptedCountWrapsWithoutLosingTheSelectedContact()
	{
		var result = Empty with { HitCount = uint.MaxValue };
		Assert.True( Evaluate( new( .25f, .25f, 1f ), 0f, Triangle, Both, ref result ) );
		Assert.Equal( 0u, result.HitCount );
		Assert.Equal( .5f, result.Fraction );
	}

	private static bool Evaluate( Vector3 start, float radius, MeshTriangle triangle,
		MeshQueryFilter filter, ref MeshContact result ) => Query().Evaluate( start, new( 0f, 0f, -2f ), radius,
			new( -10f ), new( 10f ), triangle, Matrix4x4.Identity, 123, filter, ref result );
}
