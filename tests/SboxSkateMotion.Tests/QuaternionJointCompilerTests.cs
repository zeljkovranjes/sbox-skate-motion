using System.Numerics;
using System.Text.Json;
using SkateMotion.Core.Constraints;
using Xunit;
using Xunit.Abstractions;

namespace SboxSkateMotion.Tests;

public class QuaternionJointCompilerTests( ITestOutputHelper output )
{
	public static IEnumerable<object[]> Cases => Enumerable.Range( 0, 48 ).Select( index => new object[] { index } );

	[Theory]
	[MemberData( nameof( Cases ) )]
	public void MatchesCompleteSyntheticSpuQuaternionJoint( int index )
	{
		using var document = JsonDocument.Parse( File.ReadAllText(
			Path.Combine( AppContext.BaseDirectory, "Data", "joint-twist.json" ) ) );
		var fixture = document.RootElement.GetProperty( "cases" )[ index ];
		var a = Body( fixture.GetProperty( "bodyA" ), fixture.GetProperty( "respondsA" ).GetBoolean() );
		var b = Body( fixture.GetProperty( "bodyB" ), fixture.GetProperty( "respondsB" ).GetBoolean() );
		var frames = fixture.GetProperty( "frames" );
		var settings = fixture.GetProperty( "settings" );
		var linear = new JointGeometry( 0, 1, Vector( frames[ 1 ] ), Vector( frames[ 3 ] ),
			Rotation( frames[ 4 ] ), Vector( settings[ 0 ] ), Vector( settings[ 1 ] ) );
		var angular = new QuaternionJointGeometry( Rotation( frames[ 0 ] ), Rotation( frames[ 2 ] ),
			fixture.GetProperty( "submode" ).GetInt32() == 0, settings[ 2 ][ 0 ].GetSingle() );
		var result = QuaternionJointCompiler.Compile( a, b, linear, angular, fixture.GetProperty( "step" ).GetSingle() );
		var pair = result.Pair;
		var expected = fixture.GetProperty( "expected" );
		Vector3[] actual =
		[
			pair.OffsetA, pair.OffsetB, pair.LinearImpulse, pair.AngularImpulse,
			Row( pair.LinearCoefficients, 0 ), Row( pair.AngularCoefficients, 0 ),
			Row( pair.LinearCoefficients, 1 ), Row( pair.AngularCoefficients, 1 ),
			Row( pair.LinearCoefficients, 2 ), Row( pair.AngularCoefficients, 2 ),
			result.PositiveLinearBias, result.NegativeLinearBias,
			Row( pair.LinearAxes, 0 ), Row( pair.LinearAxes, 1 ), Row( pair.LinearAxes, 2 ),
			Row( pair.AngularAxes, 0 ), Row( pair.AngularAxes, 1 ), Row( pair.AngularAxes, 2 ),
			Row( pair.InverseInertiaA, 0 ), Row( pair.InverseInertiaA, 1 ), Row( pair.InverseInertiaA, 2 ),
			Row( pair.InverseInertiaB, 0 ), Row( pair.InverseInertiaB, 1 ), Row( pair.InverseInertiaB, 2 )
		];
		var maximum = 0f;
		for ( var row = 0; row < actual.Length; row++ )
			maximum = Math.Max( maximum, Compare( actual[ row ], Vector( expected[ row ] ), $"{index} row {row}" ) );
		maximum = Math.Max( maximum, Compare( result.PositiveAngularBias,
			new Vector3( expected[ 6 ][ 3 ].GetSingle(), expected[ 7 ][ 3 ].GetSingle(), expected[ 10 ][ 3 ].GetSingle() ), "positive angular bias" ) );
		maximum = Math.Max( maximum, Compare( result.NegativeAngularBias,
			new Vector3( expected[ 8 ][ 3 ].GetSingle(), expected[ 9 ][ 3 ].GetSingle(), expected[ 11 ][ 3 ].GetSingle() ), "negative angular bias" ) );
		Assert.Equal( a.InverseMass, expected[ 18 ][ 3 ].GetSingle() );
		Assert.Equal( b.InverseMass, expected[ 21 ][ 3 ].GetSingle() );
		Assert.Equal( 0, pair.BodyA );
		Assert.Equal( 1, pair.BodyB );
		output.WriteLine( $"quaternion-joint-{index:00}: maximum absolute component error {maximum:R}" );
	}

	private static float Compare( Vector3 actual, Vector3 expected, string label )
	{
		var delta = Vector3.Abs( actual - expected );
		var error = Math.Max( delta.X, Math.Max( delta.Y, delta.Z ) );
		Assert.True( float.IsFinite( error ) && error <= 0.00001f,
			$"{label}: actual {actual}, expected {expected}, error {error:R}" );
		return error;
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
