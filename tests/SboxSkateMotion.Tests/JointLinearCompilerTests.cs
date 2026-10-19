using System.Numerics;
using System.Text.Json;
using SkateMotion.Core.Constraints;
using Xunit;
using Xunit.Abstractions;

namespace SboxSkateMotion.Tests;

public class JointLinearCompilerTests( ITestOutputHelper output )
{
	public static IEnumerable<object[]> Cases => Enumerable.Range( 0, 24 ).Select( index => new object[] { index } );

	[Theory]
	[MemberData( nameof( Cases ) )]
	public void MatchesLinearPortionOfSyntheticSpuJointCompiler( int index )
	{
		using var document = JsonDocument.Parse( File.ReadAllText(
			Path.Combine( AppContext.BaseDirectory, "Data", "joint-compiler.json" ) ) );
		var fixture = document.RootElement.GetProperty( "cases" )[ index ];
		var a = Body( fixture.GetProperty( "bodyA" ), fixture.GetProperty( "respondsA" ).GetBoolean() );
		var b = Body( fixture.GetProperty( "bodyB" ), fixture.GetProperty( "respondsB" ).GetBoolean() );
		var frames = fixture.GetProperty( "frames" );
		var settings = fixture.GetProperty( "settings" );
		var geometry = new JointGeometry( 0, 1, Vector( frames[ 1 ] ), Vector( frames[ 3 ] ),
			Rotation( frames[ 4 ] ), Vector( settings[ 0 ] ), Vector( settings[ 1 ] ) );
		var result = JointLinearCompiler.Compile( a, b, geometry, fixture.GetProperty( "step" ).GetSingle() );
		var pair = result.Pair;
		var expected = fixture.GetProperty( "expected" );
		(int Index, Vector3 Actual)[] comparisons =
		[
			(0, pair.OffsetA), (1, pair.OffsetB), (2, pair.LinearImpulse),
			(4, Row( pair.LinearCoefficients, 0 )), (6, Row( pair.LinearCoefficients, 1 )),
			(8, Row( pair.LinearCoefficients, 2 )), (10, result.PositiveLinearBias), (11, result.NegativeLinearBias),
			(12, Row( pair.LinearAxes, 0 )), (13, Row( pair.LinearAxes, 1 )), (14, Row( pair.LinearAxes, 2 )),
			(18, Row( pair.InverseInertiaA, 0 )), (19, Row( pair.InverseInertiaA, 1 )), (20, Row( pair.InverseInertiaA, 2 )),
			(21, Row( pair.InverseInertiaB, 0 )), (22, Row( pair.InverseInertiaB, 1 )), (23, Row( pair.InverseInertiaB, 2 ))
		];
		var error = 0f;
		foreach ( var comparison in comparisons )
		{
			var reference = Vector( expected[ comparison.Index ] );
			var delta = Vector3.Abs( comparison.Actual - reference );
			var rowError = Math.Max( delta.X, Math.Max( delta.Y, delta.Z ) );
			Assert.True( float.IsFinite( rowError ) && rowError <= 0.00001f,
				$"{index} row {comparison.Index}: actual {comparison.Actual}, expected {reference}, error {rowError:R}" );
			error = Math.Max( error, rowError );
		}
		Assert.Equal( a.InverseMass, expected[ 18 ][ 3 ].GetSingle() );
		Assert.Equal( b.InverseMass, expected[ 21 ][ 3 ].GetSingle() );
		Assert.Equal( 0, pair.BodyA );
		Assert.Equal( 1, pair.BodyB );
		output.WriteLine( $"joint-linear-{index:00}: maximum absolute component error {error:R}" );
	}

	private static DynamicBody Body( JsonElement data, bool responds )
	{
		var first = data[ 7 ];
		var second = data[ 8 ];
		var xx = first[ 0 ].GetSingle();
		var xy = first[ 1 ].GetSingle();
		var xz = first[ 2 ].GetSingle();
		var zz = second[ 0 ].GetSingle();
		var yy = second[ 1 ].GetSingle();
		var yz = second[ 2 ].GetSingle();
		return new DynamicBody
		{
			Position = Vector( data[ 1 ] ), Rotation = Rotation( data[ 0 ] ),
			Velocity = Vector( data[ 2 ] ), AngularVelocity = Vector( data[ 3 ] ),
			Acceleration = Vector( data[ 9 ] ), AngularAcceleration = Vector( data[ 10 ] ),
			InverseMass = responds ? first[ 3 ].GetSingle() : 0f,
			WorldInverseInertia = new Matrix4x4( xx, xy, xz, 0, xy, yy, yz, 0, xz, yz, zz, 0, 0, 0, 0, 1 )
		};
	}

	private static Vector3 Row( Matrix4x4 matrix, int row ) => row switch
	{
		0 => new Vector3( matrix.M11, matrix.M12, matrix.M13 ),
		1 => new Vector3( matrix.M21, matrix.M22, matrix.M23 ),
		_ => new Vector3( matrix.M31, matrix.M32, matrix.M33 )
	};

	private static Vector3 Vector( JsonElement value ) => new( value[ 0 ].GetSingle(), value[ 1 ].GetSingle(), value[ 2 ].GetSingle() );

	private static Quaternion Rotation( JsonElement value ) => new( Vector( value ), value[ 3 ].GetSingle() );
}
