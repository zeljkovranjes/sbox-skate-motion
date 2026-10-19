using System.Numerics;
using System.Text.Json;
using SkateMotion.Core.Animation;
using SkateMotion.Core.Physics;
using Xunit;

namespace SboxSkateMotion.Tests;

public class PhysicalRigBodyConstructionTests
{
	public static IEnumerable<object[]> Cases => Enumerable.Range( 0, 216 ).Select( index => new object[] { index } );

	[Theory]
	[MemberData( nameof( Cases ) )]
	public void BuildsCapturedPrimitiveGeometryAndBothMassVariants( int index )
	{
		using var document = JsonDocument.Parse( PhysicalRigBodyConstructionFixtures.Json );
		var fixture = document.RootElement.GetProperty( "cases" )[ index ];
		var definition = fixture.GetProperty( "definition" );
		var tuning = fixture.GetProperty( "tuning" );
		var expected = fixture.GetProperty( "expected" );
		float T( string name ) => tuning.GetProperty( name ).GetSingle();
		var result = PhysicalRigBodyConstruction.Create(
			new( definition.GetProperty( "kind" ).GetInt32(), Vector( definition.GetProperty( "dimensions" ) ),
				definition.GetProperty( "sizeScale" ).GetSingle(),
				definition.GetProperty( "primaryMassCoefficient" ).GetSingle(),
				definition.GetProperty( "alternateMassCoefficient" ).GetSingle(),
				definition.GetProperty( "index" ).GetInt32() == 0 ),
			new( T( "rootRadius" ), T( "rootHalfLength" ), T( "massScale" ), T( "alternateMassScale" ),
				T( "capsuleRadiusScale" ), T( "capsuleLengthScale" ), tuning.GetProperty( "inertiaMode" ).GetInt32(),
				T( "inertiaScale" ), .001f ) );

		Assert.Equal( expected.GetProperty( "shape" ).GetInt32(), result.Shape );
		Assert.Equal( Vector( expected.GetProperty( "halfExtents" ) ), result.HalfExtents );
		Assert.Equal( expected.GetProperty( "radius" ).GetSingle(), result.Radius );
		Assert.Equal( expected.GetProperty( "halfLength" ).GetSingle(), result.HalfLength );
		Assert.Equal( expected.GetProperty( "poseVolume" ).GetSingle(), result.PoseVolume );
		AssertMass( result.Primary, expected, "primary" );
		AssertMass( result.Alternate, expected, "alternate" );
		AssertMass( result.Descriptor.Mass, expected, "descriptor" );
		Assert.Equal( expected.GetProperty( "maximumInertia" ).GetSingle(), result.Descriptor.MaximumInertia );
		Assert.True( result.Descriptor.HasShapeProperties );
		Assert.Null( result.Descriptor.LocalOffset );

		// The constructed descriptor is usable by the retained body's frame update.
		var body = new PhysicalBodyState { DefaultLinearAccumulator = new Vector3( 0f, -9.8f, 0f ) };
		PhysicalBodyInitialization.Initialize( body, LimbRigidTransform.Identity, result.Descriptor.Mass, 4, 0 );
		Assert.Equal( result.Descriptor.Mass.InverseInertia, body.InverseInertiaDiagonal );
		Assert.Equal( result.Descriptor.Mass.InverseMass, body.InverseMass );
		Assert.Equal( body.DefaultLinearAccumulator, body.LinearAccumulator );
	}

	[Fact]
	public void RejectsUnsupportedPrimitiveWithoutPretendingItIsACompoundBody()
	{
		Assert.Throws<ArgumentOutOfRangeException>( () => PhysicalRigBodyConstruction.Create(
			new( 3, Vector3.One, 1f, 1f, 1f, false ),
			new( .1f, .2f, 1f, 1f, 1f, 1f, 0, 1f, .001f ) ) );
	}

	private static void AssertMass( PhysicalBodyMass actual, JsonElement expected, string prefix )
	{
		Assert.Equal( Vector( expected.GetProperty( prefix + "InverseInertia" ) ), actual.InverseInertia );
		Assert.Equal( expected.GetProperty( prefix + "InverseMass" ).GetSingle(), actual.InverseMass );
	}

	private static Vector3 Vector( JsonElement value ) => new( value[ 0 ].GetSingle(), value[ 1 ].GetSingle(), value[ 2 ].GetSingle() );
}
