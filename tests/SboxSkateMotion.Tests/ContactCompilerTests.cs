using System.Numerics;
using System.Text.Json;
using SkateMotion.Core.Constraints;
using Xunit;
using Xunit.Abstractions;

namespace SboxSkateMotion.Tests;

public class ContactCompilerTests( ITestOutputHelper output )
{
	public static IEnumerable<object[]> Cases => Enumerable.Range( 0, 36 ).Select( index => new object[] { index } );

	[Theory]
	[MemberData( nameof( Cases ) )]
	public void MatchesSyntheticSpuCompilerExecution( int index )
	{
		using var document = JsonDocument.Parse( File.ReadAllText(
			Path.Combine( AppContext.BaseDirectory, "Data", "contact-compiler.json" ) ) );
		var fixture = document.RootElement.GetProperty( "cases" )[ index ];
		var input = fixture.GetProperty( "input" );
		var a = Body( input, 0, fixture.GetProperty( "flagsA" ).GetInt32() );
		var b = Body( input, 1, fixture.GetProperty( "flagsB" ).GetInt32() );
		var geometry = new ContactGeometry( 0, 1, Vector( input[ 0 ] ), Vector( input[ 1 ] ),
			Vector( input[ 2 ] ), Vector( input[ 3 ] ), Vector( input[ 4 ] ), input[ 3 ][ 3 ].GetSingle(),
			input[ 4 ][ 3 ].GetSingle(), input[ 2 ][ 3 ].GetSingle(), Vector( input[ 5 ] ) );
		var result = ContactCompiler.Compile( a, b, geometry, fixture.GetProperty( "scale" ).GetSingle() );
		var expected = fixture.GetProperty( "expected" );
		Vector3[] actual =
		[
			result.OffsetA, result.OffsetB, result.CoefficientX, result.CoefficientY, result.CoefficientZ,
			new Vector3( result.Impulse.X, result.Impulse.Y, result.Impulse.Z ),
			new Vector3( result.Bias.X, result.Bias.Y, result.Bias.Z ),
			result.Normal, result.NormalAngularA, result.NormalAngularB,
			result.TangentA, result.TangentAAngularA, result.TangentAAngularB,
			result.TangentB, result.TangentBAngularA, result.TangentBAngularB
		];
		var error = 0f;
		for ( var i = 0; i < actual.Length; i++ )
		{
			var delta = Vector3.Abs( actual[ i ] - Vector( expected[ i ] ) );
			var rowError = Math.Max( delta.X, Math.Max( delta.Y, delta.Z ) );
			Assert.True( float.IsFinite( rowError ) && rowError <= 0.00001f,
				$"{index} row {i}: actual {actual[ i ]}, expected {Vector( expected[ i ] )}, error {rowError:R}" );
			error = Math.Max( error, rowError );
		}
		Assert.InRange( Math.Abs( result.Bias.W - expected[ 6 ][ 3 ].GetSingle() ), 0f, 0.00001f );
		Assert.Equal( expected[ 5 ][ 3 ].GetSingle(), result.Impulse.W );
		Assert.Equal( expected[ 3 ][ 3 ].GetSingle(), result.StaticFriction );
		Assert.Equal( expected[ 4 ][ 3 ].GetSingle(), result.SlidingFriction );
		Assert.Equal( expected[ 8 ][ 3 ].GetSingle(), a.EffectiveInverseMass );
		Assert.Equal( expected[ 9 ][ 3 ].GetSingle(), b.EffectiveInverseMass );
		Assert.Equal( 0, result.BodyA );
		Assert.Equal( 1, result.BodyB );
		error = Math.Max( error, Math.Abs( result.Bias.W - expected[ 6 ][ 3 ].GetSingle() ) );
		output.WriteLine( $"compiler-{index:00}: maximum absolute component error {error:R}" );
	}

	private static ContactBody Body( JsonElement input, int index, int flags )
	{
		var first = input[ 8 + index ];
		var second = input[ 10 + index ];
		var xx = first[ 0 ].GetSingle();
		var xy = first[ 1 ].GetSingle();
		var xz = first[ 2 ].GetSingle();
		var zz = second[ 0 ].GetSingle();
		var yy = second[ 1 ].GetSingle();
		var yz = second[ 2 ].GetSingle();
		var inertia = new Matrix4x4( xx, xy, xz, 0f, xy, yy, yz, 0f, xz, yz, zz, 0f, 0f, 0f, 0f, 1f );
		return new ContactBody( Vector( input[ 6 + index ] ), first[ 3 ].GetSingle(), inertia,
			Vector( input[ 12 + index ] ), Vector( input[ 14 + index ] ), (flags & 4) != 0 );
	}

	private static Vector3 Vector( JsonElement value ) => new( value[ 0 ].GetSingle(), value[ 1 ].GetSingle(), value[ 2 ].GetSingle() );
}
