using System.Numerics;
using System.Text.Json;
using SkateMotion.Core.Constraints;
using Xunit;

namespace SboxSkateMotion.Tests;

public class DrivePrecisionTests
{
	private static readonly JsonElement Fixtures = LoadFixtures();

	public static IEnumerable<object[]> Cases => Enumerable.Range( 0, 2 ).Select( index => new object[] { index } );

	[Theory]
	[MemberData( nameof( Cases ) )]
	public void PreservesExactConnectedBodyPacketRounding( int index )
	{
		var fixture = Fixtures.GetProperty( "cases" )[ index ];
		var a = Body( fixture.GetProperty( "bodyA" ), fixture.GetProperty( "respondsA" ).GetBoolean() );
		var b = Body( fixture.GetProperty( "bodyB" ), fixture.GetProperty( "respondsB" ).GetBoolean() );
		var frames = fixture.GetProperty( "frames" );
		var settings = fixture.GetProperty( "settings" );
		var geometry = new DriveGeometry( 0, 1, Vector( frames[ 1 ] ), Vector( frames[ 3 ] ),
			Rotation( frames[ 0 ] ), Rotation( frames[ 2 ] ) );
		var linear = Settings( settings[ 0 ], fixture.GetProperty( "linearMode" ).GetInt32() );
		var angular = Settings( settings[ 1 ], fixture.GetProperty( "angularMode" ).GetInt32() );
		var result = DriveCompiler.Compile( a, b, geometry, linear, angular, fixture.GetProperty( "step" ).GetSingle() );
		var pair = result.Pair;
		var expected = fixture.GetProperty( "expected" );
		Vector3[] actual =
		[
			pair.OffsetA, pair.OffsetB, pair.LinearImpulse, pair.AngularImpulse,
			Row( pair.LinearCoefficients, 0 ), Row( pair.AngularCoefficients, 0 ),
			Row( pair.LinearCoefficients, 1 ), Row( pair.AngularCoefficients, 1 ),
			Row( pair.LinearCoefficients, 2 ), Row( pair.AngularCoefficients, 2 ),
			result.LinearBias, result.AngularBias,
			Row( pair.LinearAxes, 0 ), Row( pair.LinearAxes, 1 ), Row( pair.LinearAxes, 2 ),
			Row( pair.AngularAxes, 0 ), Row( pair.AngularAxes, 1 ), Row( pair.AngularAxes, 2 ),
			Row( pair.InverseInertiaA, 0 ), Row( pair.InverseInertiaA, 1 ), Row( pair.InverseInertiaA, 2 ),
			Row( pair.InverseInertiaB, 0 ), Row( pair.InverseInertiaB, 1 ), Row( pair.InverseInertiaB, 2 )
		];
		var maximum = 0f;
		for ( var row = 0; row < actual.Length; row++ )
			maximum = Math.Max( maximum, Compare( actual[ row ], Vector( expected[ row ] ), $"{index} row {row}" ) );
		maximum = Math.Max( maximum, Compare( result.LinearLimit,
			new Vector3( expected[ 6 ][ 3 ].GetSingle(), expected[ 7 ][ 3 ].GetSingle(), expected[ 10 ][ 3 ].GetSingle() ), "linear limit" ) );
		maximum = Math.Max( maximum, Compare( result.AngularLimit,
			new Vector3( expected[ 8 ][ 3 ].GetSingle(), expected[ 9 ][ 3 ].GetSingle(), expected[ 11 ][ 3 ].GetSingle() ), "angular limit" ) );
		maximum = Math.Max( maximum, Compare( new Vector3( result.LinearGain, result.AngularGain, 0f ),
			new Vector3( expected[ 2 ][ 3 ].GetSingle(), expected[ 3 ][ 3 ].GetSingle(), 0f ), "gains" ) );
		Assert.Equal( a.InverseMass, expected[ 18 ][ 3 ].GetSingle() );
		Assert.Equal( b.InverseMass, expected[ 21 ][ 3 ].GetSingle() );
		Assert.Equal( 0, pair.BodyA );
		Assert.Equal( 1, pair.BodyB );
		Assert.Equal( 0f, maximum );
	}

	private static JsonElement LoadFixtures()
	{
		using var document = JsonDocument.Parse( """
{"cases":[{"bodyA":[[0,0,0.059964006479444595,0.9982005399352042],[0.2,0.03,0.1,0],[0.0,0.03,-0.02,0],[0,0.01,-0.03,0],[0.9928086358538663,0.11971220728891936,0,0],[-0.11971220728891936,0.9928086358538663,0,0],[0,0,1,0],[1.75,0,0,0.375],[1.75,1.75,0,0],[0,0,-9.81,10],[0,0,0,0]],"bodyB":[[0,0,0.02999550020249566,0.9995500337489875],[0.0,0.0,0.0,0],[-0.2,0.0,-0.0,0],[0,0.0,-0.0,0],[0.9982005399352042,0.059964006479444595,0,0],[-0.059964006479444595,0.9982005399352042,0,0],[0,0,1,0],[1.25,0,0,0.25],[1.25,1.25,0,0],[0,0,-9.81,10],[0,0,0,0]],"respondsA":true,"respondsB":true,"frames":[[0,0,0,1],[0,0,0,0],[0,0,0,1],[0.19,0.02,0.11,0]],"settings":[[4,0,3000,0],[60,3,1000,0]],"linearMode":2,"angularMode":1,"step":0.004166666666666667,"expected":[[0.0,0.0,0.0,9e-44],[0.18845883,0.03135717,0.11,0.0],[0.0,0.0,0.0,1.0],[0.0,0.0,0.0,0.013360741],[1.5581666,-0.087506756,0.0,0.0],[0.33198422,-0.029959511,0.0,0.0],[0.09360235,1.4566954,0.0,0.08130082],[0.029959511,0.33198422,0.0,0.07600633],[0.0,0.0,1.4911463,0.005787037],[0.0,0.0,0.33333334,0.005787037],[-0.019166209,0.002877749,0.0150357215,0.07766388],[-1.539543e-08,-1.7059826e-07,-2.0047279e-05,0.0057870382],[0.99820054,0.05996401,0.0,0.0],[-0.05996401,0.99820054,0.0,0.0],[0.0,0.0,1.0,0.25],[0.9959528,0.089878544,0.0,0.0],[-0.089878544,0.9959528,0.0,0.0],[0.0,0.0,1.0,0.0],[1.75,0.0,0.0,0.375],[0.0,1.75,0.0,0.375],[0.0,0.0,1.75,0.375],[1.25,0.0,0.0,0.25],[0.0,1.25,0.0,0.25],[0.0,0.0,1.25,0.25]]},{"bodyA":[[0,0,0.08987854919801104,0.9959527330119943],[0.4,0.06,0.2,0],[0.2,0.06,-0.04,0],[0,0.02,-0.06,0],[0.9838436927881214,0.17902957342582418,0,0],[-0.17902957342582418,0.9838436927881214,0,0],[0,0,1,0],[2.25,0,0,0.5],[2.25,2.25,0,0],[0,0,-9.81,10],[0,0,0,0]],"bodyB":[[0,0,0.059964006479444595,0.9982005399352042],[0.2,0.03,0.1,0],[0.0,0.03,-0.02,0],[0,0.01,-0.03,0],[0.9928086358538663,0.11971220728891936,0,0],[-0.11971220728891936,0.9928086358538663,0,0],[0,0,1,0],[1.75,0,0,0.375],[1.75,1.75,0,0],[0,0,-9.81,10],[0,0,0,0]],"respondsA":false,"respondsB":true,"frames":[[0,0,0,1],[0,0,0,0],[0,0,0,1],[0.19,0.02,0.11,0]],"settings":[[4,0,3000,0],[60,3,1000,0]],"linearMode":2,"angularMode":1,"step":0.004166666666666667,"expected":[[0.0,0.0,0.0,1.8e-43],[0.18623939,0.042601492,0.11,9e-44],[0.0,0.0,0.0,1.0],[0.0,0.0,0.0,0.013360741],[2.501565,-0.26061216,0.0,0.0],[0.56501204,-0.08539322,0.0,0.0],[0.30163702,2.1613338,0.0,0.13123362],[0.08539322,0.56501204,0.0,0.11338487],[0.0,0.0,2.2785532,0.009920636],[0.0,0.0,0.5714286,0.009920636],[-0.026213018,0.024160212,0.018111091,0.118674666],[-4.3881407e-08,-2.9034533e-07,-3.4366778e-05,0.009920638],[0.99280864,0.11971219,0.0,0.0],[-0.11971219,0.99280864,0.0,0.0],[0.0,0.0,1.0,0.375],[0.9887711,0.14943814,0.0,0.0],[-0.14943814,0.9887711,0.0,0.0],[0.0,0.0,0.99999994,0.0],[0.0,0.0,0.0,0.0],[0.0,0.0,0.0,0.0],[0.0,0.0,0.0,0.0],[1.75,0.0,0.0,0.375],[0.0,1.75,0.0,0.375],[0.0,0.0,1.75,0.375]]}]}
""" );
		return document.RootElement.Clone();
	}

	private static DriveSettings Settings( JsonElement value, int mode )
		=> new( (DriveMode)mode, value[ 0 ].GetSingle(), value[ 1 ].GetSingle(), value[ 2 ].GetSingle() );

	private static float Compare( Vector3 actual, Vector3 expected, string label )
	{
		var delta = Vector3.Abs( actual - expected );
		var error = Math.Max( delta.X, Math.Max( delta.Y, delta.Z ) );
		Assert.True( float.IsFinite( error ) && error == 0f,
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
			RotationBasis = new Matrix4x4(
				data[ 4 ][ 0 ].GetSingle(), data[ 4 ][ 1 ].GetSingle(), data[ 4 ][ 2 ].GetSingle(), 0f,
				data[ 5 ][ 0 ].GetSingle(), data[ 5 ][ 1 ].GetSingle(), data[ 5 ][ 2 ].GetSingle(), 0f,
				data[ 6 ][ 0 ].GetSingle(), data[ 6 ][ 1 ].GetSingle(), data[ 6 ][ 2 ].GetSingle(), 0f,
				0f, 0f, 0f, 1f ),
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
