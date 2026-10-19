using System.Numerics;
using System.Text.Json;
using SkateMotion.Core.Animation;
using SkateMotion.Core.Physics;
using Xunit;

namespace SboxSkateMotion.Tests;

public class PhysicalCompoundRigBodyTests
{
	public static IEnumerable<object[]> Cases => Enumerable.Range( 0, 36 ).Select( index => new object[] { index } );

	[Theory]
	[MemberData( nameof( Cases ) )]
	public void BuildsCapturedCompoundMassVariantsAndIdentityOffset( int index )
	{
		using var document = JsonDocument.Parse( PhysicalCompoundRigBodyFixtures.Json );
		using var mathDocument = JsonDocument.Parse( PhysicalCompoundBodyGeometryFixtures.Json );
		var coefficients = mathDocument.RootElement.GetProperty( "math" );
		float[] Values( string name ) => coefficients.GetProperty( name ).EnumerateArray().Select( x => x.GetSingle() ).ToArray();
		float Scalar( string name ) => coefficients.GetProperty( name ).GetSingle();
		var math = new LimbSolverMath( Values( "cosine" ), Values( "sine" ), Values( "arcCosine" ),
			Scalar( "quadrantScale" ), Scalar( "quadrantHigh" ), Scalar( "quadrantLow" ), Scalar( "pi" ) );
		var fixture = document.RootElement.GetProperty( "cases" )[ index ];
		var input = fixture.GetProperty( "compound" );
		float F( string name ) => input.GetProperty( name ).GetSingle();
		var geometry = PhysicalCompoundBodyGeometry.Create(
			new( Vector( input.GetProperty( "angle" ) ), Vector( input.GetProperty( "position" ) ),
				Vector( input.GetProperty( "dimensions" ) ), F( "scale" ), F( "height" ), F( "radius" ), F( "massScale" ) ), math );
		var definition = fixture.GetProperty( "definition" );
		var tuning = fixture.GetProperty( "tuning" );
		float T( string name ) => tuning.GetProperty( name ).GetSingle();
		var result = PhysicalRigBodyConstruction.CreateCompound( geometry,
			definition.GetProperty( "primaryMassCoefficient" ).GetSingle(),
			definition.GetProperty( "alternateMassCoefficient" ).GetSingle(),
			new( T( "rootRadius" ), T( "rootHalfLength" ), T( "massScale" ), T( "alternateMassScale" ),
				T( "capsuleRadiusScale" ), T( "capsuleLengthScale" ), tuning.GetProperty( "inertiaMode" ).GetInt32(),
				T( "inertiaScale" ), .001f ) );
		var expected = fixture.GetProperty( "expected" );
		Assert.Equal( expected.GetProperty( "poseVolume" ).GetSingle(), geometry.PoseVolume );
		AssertMass( result.Primary, expected, "primary" );
		AssertMass( result.Alternate, expected, "alternate" );
		AssertMass( result.Descriptor.Mass, expected, "descriptor" );
		Assert.Equal( expected.GetProperty( "maximumInertia" ).GetSingle(), result.Descriptor.MaximumInertia );
		Assert.True( result.Descriptor.HasShapeProperties );
		var offset = fixture.GetProperty( "descriptor" ).GetProperty( "offset" );
		Assert.Equal( new LimbRigidTransform( new( Vector( offset[ 0 ] ), Vector( offset[ 1 ] ), Vector( offset[ 2 ] ) ),
			Vector( offset[ 3 ] ) ), result.Descriptor.LocalOffset );
		Assert.Equal( 1, fixture.GetProperty( "descriptor" ).GetProperty( "hasOffset" ).GetInt32() );
	}

	private static void AssertMass( PhysicalBodyMass actual, JsonElement expected, string prefix )
	{
		Assert.Equal( Vector( expected.GetProperty( prefix + "InverseInertia" ) ), actual.InverseInertia );
		Assert.Equal( expected.GetProperty( prefix + "InverseMass" ).GetSingle(), actual.InverseMass );
	}

	private static Vector3 Vector( JsonElement value ) => new( value[ 0 ].GetSingle(), value[ 1 ].GetSingle(), value[ 2 ].GetSingle() );
}
