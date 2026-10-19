using System.Numerics;
using SkateMotion.Core.Collision;
using Xunit;

namespace SboxSkateMotion.Tests;

public class TriangleRayQueryTests
{
	private static readonly TriangleContact Seed = new( new( 7f, 8f, 9f ), new( 4f, 5f, 6f ), new( 1f, 2f, 3f ), 99f );

	[Fact]
	public void FrontFaceReturnsPositionNormalBarycentricsAndFraction()
	{
		var contact = Seed;
		Assert.True( Evaluate( new( .25f, .25f, 1f ), new( 0f, 0f, -2f ), ref contact ) );
		Assert.Equal( new TriangleContact( new( .25f, .25f, 0f ), Vector3.UnitZ, new( .25f, .25f, 0f ), .5f ), contact );
	}

	[Theory]
	[InlineData( 1f, 2f )]
	[InlineData( -1f, 2f )]
	[InlineData( 1f, 0f )]
	public void BackFacingAndStationarySegmentsPreserveThePreviousContact( float startZ, float deltaZ )
	{
		var contact = Seed;
		Assert.False( Evaluate( new( .25f, .25f, startZ ), new( 0f, 0f, deltaZ ), ref contact ) );
		Assert.Equal( Seed, contact );
	}

	[Theory]
	[InlineData( -.000005f, .25f, true )]
	[InlineData( -.00002f, .25f, false )]
	[InlineData( .25f, -.000005f, true )]
	[InlineData( .25f, -.00002f, false )]
	[InlineData( .5f, .500005f, true )]
	[InlineData( .5f, .50002f, false )]
	public void BarycentricToleranceExtendsAllThreeTriangleEdges( float x, float y, bool accepted )
	{
		var contact = Seed;
		Assert.Equal( accepted, Evaluate( new( x, y, 1f ), new( 0f, 0f, -2f ), ref contact ) );
		if ( !accepted )
			Assert.Equal( Seed, contact );
		else
			Assert.Equal( new Vector3( x, y, 0f ), contact.Barycentric );
	}

	[Theory]
	[InlineData( -.000005f )]
	[InlineData( 1.000005f )]
	public void AcceptedEndpointToleranceDoesNotClampTheFraction( float fraction )
	{
		var contact = Seed;
		Assert.True( Evaluate( new( .25f, .25f, fraction ), -Vector3.UnitZ, ref contact ) );
		Assert.Equal( fraction, contact.Fraction );
	}

	[Theory]
	[InlineData( -2f )]
	[InlineData( 4f )]
	public void IntervalRejectionWritesOnlyTheUnscaledNumerator( float startZ )
	{
		var contact = Seed;
		Assert.False( Evaluate( new( .25f, .25f, startZ ), new( 0f, 0f, -2f ), ref contact ) );
		Assert.Equal( Seed with { Fraction = startZ }, contact );
	}

	[Fact]
	public void DeterminantThresholdIsStrict()
	{
		var query = new TriangleRayQuery( 1f, -.00001f );
		var contact = Seed;
		Assert.False( query.Evaluate( new( .25f, .25f, .5f ), -Vector3.UnitZ,
			Vector3.Zero, Vector3.UnitX, Vector3.UnitY, ref contact ) );
		Assert.Equal( Seed, contact );
	}

	[Fact]
	public void DegenerateTrianglePreservesThePreviousContact()
	{
		var query = new TriangleRayQuery( 1e-8f, -.00001f );
		var contact = Seed;
		Assert.False( query.Evaluate( Vector3.UnitZ, -Vector3.UnitZ,
			Vector3.Zero, Vector3.UnitX, Vector3.UnitX * 2f, ref contact ) );
		Assert.Equal( Seed, contact );
	}

	private static bool Evaluate( Vector3 start, Vector3 delta, ref TriangleContact contact ) =>
		new TriangleRayQuery( 1e-8f, -.00001f ).Evaluate( start, delta,
			Vector3.Zero, Vector3.UnitX, Vector3.UnitY, ref contact );
}
