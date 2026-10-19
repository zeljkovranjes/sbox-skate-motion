using System.Numerics;
using System.Text.Json;
using SkateMotion.Core.Animation;
using SkateMotion.Core.Physics;
using Xunit;

namespace SboxSkateMotion.Tests;

public class PhysicalCompoundBodyGeometryTests
{
	public static IEnumerable<object[]> Cases => Enumerable.Range( 0, 12 ).Select( index => new object[] { index } );

	[Theory]
	[MemberData( nameof( Cases ) )]
	public void MatchesCapturedCylinderFrameAndSphereGeometry( int index )
	{
		using var document = JsonDocument.Parse( PhysicalCompoundBodyGeometryFixtures.Json );
		var coefficients = document.RootElement.GetProperty( "math" );
		float[] Values( string name ) => coefficients.GetProperty( name ).EnumerateArray().Select( x => x.GetSingle() ).ToArray();
		float Scalar( string name ) => coefficients.GetProperty( name ).GetSingle();
		var math = new LimbSolverMath( Values( "cosine" ), Values( "sine" ), Values( "arcCosine" ),
			Scalar( "quadrantScale" ), Scalar( "quadrantHigh" ), Scalar( "quadrantLow" ), Scalar( "pi" ) );
		var fixture = document.RootElement.GetProperty( "cases" )[ index ];
		var input = fixture.GetProperty( "input" );
		float F( string name ) => input.GetProperty( name ).GetSingle();
		var result = PhysicalCompoundBodyGeometry.Create(
			new( Vector( input.GetProperty( "angle" ) ), Vector( input.GetProperty( "position" ) ),
				Vector( input.GetProperty( "dimensions" ) ), F( "scale" ), F( "height" ), F( "radius" ), F( "massScale" ) ), math );
		var cylinder = fixture.GetProperty( "children" )[ 0 ];
		var sphere = fixture.GetProperty( "children" )[ 1 ];
		var frame = cylinder.GetProperty( "frame" );
		Assert.Equal( Vector( frame[ 0 ] ), result.CylinderFrame.Rotation.X );
		Assert.Equal( Vector( frame[ 1 ] ), result.CylinderFrame.Rotation.Y );
		Assert.Equal( Vector( frame[ 2 ] ), result.CylinderFrame.Rotation.Z );
		Assert.Equal( Vector( frame[ 3 ] ), result.CylinderFrame.Translation );
		Assert.Equal( cylinder.GetProperty( "dimensions" )[ 1 ].GetSingle(), result.CylinderRadius );
		Assert.Equal( cylinder.GetProperty( "dimensions" )[ 0 ].GetSingle(), result.CylinderHalfLength );
		Assert.Equal( sphere.GetProperty( "radius" ).GetSingle(), result.SphereRadius );
		Assert.Equal( fixture.GetProperty( "poseVolume" ).GetSingle(), result.PoseVolume );
		Assert.Equal( fixture.GetProperty( "massBasis" ).GetSingle(), result.MassBasis );
	}

	private static Vector3 Vector( JsonElement value ) => new( value[ 0 ].GetSingle(), value[ 1 ].GetSingle(), value[ 2 ].GetSingle() );
}
