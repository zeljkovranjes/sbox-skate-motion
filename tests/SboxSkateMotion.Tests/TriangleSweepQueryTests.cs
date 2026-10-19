using System.Numerics;
using SkateMotion.Core.Collision;
using Xunit;

namespace SboxSkateMotion.Tests;

public class TriangleSweepQueryTests
{
	private static readonly TriangleContact Seed = new( new( 7f, 8f, 9f ), new( 4f, 5f, 6f ), new( 1f, 2f, 3f ), 99f );
	private static TriangleSweepQuery Query() => new( 1e-8f, -.00001f, 2.938735877055719e-39f );

	[Theory]
	[InlineData( 1f )]
	[InlineData( -1f )]
	public void RoundedFaceAcceptsBothApproachDirections( float side )
	{
		var contact = Seed;
		Assert.True( Evaluate( new( .25f, .25f, side ), new( 0f, 0f, -2f * side ), .1f, 0f, ref contact ) );
		Assert.Equal( new Vector3( 0f, 0f, side ), contact.Normal );
		Assert.Equal( new Vector3( .25f, .25f, 0f ), contact.Barycentric );
		Assert.Equal( .44999998807907104f, contact.Fraction );
		Assert.Equal( new Vector3( .25f, .25f, side * 2.2351741790771484e-8f ), contact.Position );
	}

	[Fact]
	public void InitialOverlapReportsSquaredPenetrationWithoutAdvancingTime()
	{
		var contact = Seed;
		Assert.True( Evaluate( new( .25f, .25f, .05f ), new( 0f, 0f, -2f ), .1f, 0f, ref contact ) );
		Assert.Equal( 0f, contact.Fraction );
		Assert.Equal( new Vector3( .25f, .25f, -.05f ), contact.Position );
		Assert.Equal( new Vector3( .25f, .25f, .0075000003f ), contact.Barycentric );
		Assert.Equal( Vector3.UnitZ, contact.Normal );
	}

	[Fact]
	public void MarginMovesTheSweepBoundaryButOnlyRadiusOffsetsTheReturnedPosition()
	{
		var fullRadius = Seed;
		var withMargin = Seed;
		Assert.True( Evaluate( new( .25f, .25f, 1f ), new( 0f, 0f, -2f ), .1f, 0f, ref fullRadius ) );
		Assert.True( Evaluate( new( .25f, .25f, 1f ), new( 0f, 0f, -2f ), .05f, .05f, ref withMargin ) );
		Assert.Equal( fullRadius.Fraction, withMargin.Fraction );
		Assert.Equal( fullRadius.Normal, withMargin.Normal );
		Assert.Equal( fullRadius.Barycentric, withMargin.Barycentric );
		Assert.Equal( .05000002309679985f, withMargin.Position.Z );
	}

	[Theory]
	[InlineData( .5f, -.05f, .5f, 0f, 0f, -.5f )]
	[InlineData( -.05f, .5f, 0f, .5f, -.5f, 0f )]
	public void EdgeContactReturnsTheFeaturePointAndRadialNormal( float x, float y, float u, float v, float nx, float ny )
	{
		var contact = Seed;
		Assert.True( Evaluate( new( x, y, 1f ), new( 0f, 0f, -2f ), .1f, 0f, ref contact ) );
		Assert.Equal( new Vector3( u, v, 0f ), contact.Position );
		Assert.Equal( new Vector3( u, v, 0f ), contact.Barycentric );
		Assert.Equal( new Vector3( nx, ny, .8660253882408142f ), contact.Normal );
		Assert.Equal( .4566987156867981f, contact.Fraction );
	}

	[Fact]
	public void CornerContactUsesTheVertexRoot()
	{
		var contact = Seed;
		Assert.True( Evaluate( new( -.05f, -.05f, 1f ), new( 0f, 0f, -2f ), .1f, 0f, ref contact ) );
		Assert.Equal( Vector3.Zero, contact.Position );
		Assert.Equal( Vector3.Zero, contact.Barycentric );
		Assert.Equal( new Vector3( -.5f, -.5f, .7071068286895752f ), contact.Normal );
		Assert.Equal( .4646446704864502f, contact.Fraction );
	}

	[Theory]
	[InlineData( 0f, 0f )]
	[InlineData( .1f, -.1f )]
	public void ZeroCombinedRadiusUsesTheDirectedRayContract( float radius, float margin )
	{
		var contact = Seed;
		Assert.False( Evaluate( new( .25f, .25f, -1f ), new( 0f, 0f, 2f ), radius, margin, ref contact ) );
		Assert.Equal( Seed, contact );
		Assert.True( Evaluate( new( .25f, .25f, 1f ), new( 0f, 0f, -2f ), radius, margin, ref contact ) );
		Assert.Equal( new Vector3( .25f, .25f, 0f ), contact.Position );
		Assert.Equal( .5f, contact.Fraction );
	}

	[Fact]
	public void RoundedRejectionUpdatesNormalAndTimeButRetainsOtherOutputs()
	{
		var contact = Seed;
		Assert.False( Evaluate( new( 2f, 2f, 1f ), new( 0f, 0f, -2f ), .1f, 0f, ref contact ) );
		Assert.Equal( Seed with { Normal = Vector3.UnitZ, Fraction = 0f }, contact );
	}

	[Fact]
	public void StationaryFaceQueryRetainsPrimitiveBehaviorForTheDispatchGate()
	{
		var contact = Seed;
		Assert.True( Evaluate( new( .25f, .25f, 1f ), Vector3.Zero, .1f, 0f, ref contact ) );
		Assert.Equal( new Vector3( .25f, .25f, .9f ), contact.Position );
		Assert.Equal( new Vector3( .25f, .25f, 0f ), contact.Barycentric );
		Assert.Equal( 0f, contact.Fraction );
	}

	[Fact]
	public void VertexToEdgeRejectionRetainsTheAdvancedFraction()
	{
		var contact = Seed;
		Assert.False( Evaluate( new( 1.3455915589985201f, -.26855818418449506f, -.25824762695305753f ),
			new( -1.591209461381455f, 1.1984831313450073f, -.915007422919857f ), .3f, 0f, ref contact ) );
		Assert.Equal( Seed with { Normal = Vector3.UnitZ, Fraction = .21718797087669373f }, contact );
	}

	[Fact]
	public void EdgeToVertexRejectionRetainsTheAdvancedFraction()
	{
		var contact = Seed;
		Assert.False( Evaluate( new( 1.216664588616527f, .3047781088163737f, -.2348599899296513f ),
			new( 1.620378904421624f, -1.9750771988578588f, .6868284229781758f ), .3f, 0f, ref contact ) );
		Assert.Equal( Seed with { Normal = Vector3.UnitZ, Fraction = .024506917223334312f }, contact );
	}

	[Fact]
	public void ReversingTriangleWindingStillReturnsTheApproachNormal()
	{
		var contact = Seed;
		Assert.True( Query().Evaluate( new( .25f, .25f, 1f ), new( 0f, 0f, -2f ), Vector3.Zero,
			Vector3.UnitY, Vector3.UnitX, .1f, 0f, ref contact ) );
		Assert.Equal( Vector3.UnitZ, contact.Normal );
		Assert.Equal( .44999998807907104f, contact.Fraction );
	}

	private static bool Evaluate( Vector3 start, Vector3 delta, float radius, float margin, ref TriangleContact contact ) =>
		Query().Evaluate( start, delta, Vector3.Zero, Vector3.UnitX, Vector3.UnitY, radius, margin, ref contact );

	[Fact]
	public void DegenerateRejectedQueryRetainsCanonicalInvalidNormal()
	{
		var contact = Seed;
		Assert.False( Query().Evaluate( new( 2f, 2f, 1f ), new( 0f, 0f, -2f ),
			Vector3.Zero, Vector3.UnitX, Vector3.UnitX, .1f, 0f, ref contact ) );
		Assert.Equal( Seed.Position, contact.Position );
		Assert.Equal( Seed.Barycentric, contact.Barycentric );
		Assert.Equal( 0f, contact.Fraction );
		Assert.Equal( 0x7fc00000u, BitConverter.SingleToUInt32Bits( contact.Normal.X ) );
		Assert.Equal( 0x7fc00000u, BitConverter.SingleToUInt32Bits( contact.Normal.Y ) );
		Assert.Equal( 0x7fc00000u, BitConverter.SingleToUInt32Bits( contact.Normal.Z ) );
	}
}
