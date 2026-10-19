using System.Numerics;
using System.Text.Json;
using SkateMotion.Core.Constraints;
using Xunit;
using Xunit.Abstractions;

namespace SboxSkateMotion.Tests;

public class ConstraintPairPrecisionTests( ITestOutputHelper output )
{
	public static IEnumerable<object[]> Cases => Enumerable.Range( 0, 12 ).Select( index => new object[] { index } );

	[Theory]
	[MemberData( nameof( Cases ) )]
	public void MatchesExactResponsesAndRetainedImpulses( int index )
	{
		using var document = JsonDocument.Parse( ConstraintPairPrecisionFixtures.Json );
		var fixture = document.RootElement.GetProperty( "cases" )[ index ];
		var bodies = fixture.GetProperty( "bodies" ).EnumerateArray().Select( Body ).ToArray();
		var joints = fixture.GetProperty( "joints" ).EnumerateArray().Select( Joint ).ToArray();
		var drives = fixture.GetProperty( "drives" ).EnumerateArray().Select( Drive ).ToArray();
		ConstraintSolver.Solve( bodies, [], joints, drives, fixture.GetProperty( "passes" ).GetInt32() );
		var error = 0f;
		var expected = fixture.GetProperty( "expectedBodies" );
		for ( var i = 0; i < bodies.Length; i++ )
		{
			error = Math.Max( error, Difference( bodies[ i ].LinearVelocity, Vector( expected[ i ].GetProperty( "linear" ) ) ) );
			error = Math.Max( error, Difference( bodies[ i ].AngularVelocity, Vector( expected[ i ].GetProperty( "angular" ) ) ) );
			error = Math.Max( error, Difference( bodies[ i ].CorrectionVelocity, Vector( expected[ i ].GetProperty( "correction" ) ) ) );
			error = Math.Max( error, Difference( bodies[ i ].CorrectionAngularVelocity, Vector( expected[ i ].GetProperty( "correctionAngular" ) ) ) );
		}
		for ( var i = 0; i < joints.Length; i++ )
			error = Math.Max( error, PairDifference( joints[ i ].Pair, fixture.GetProperty( "joints" )[ i ] ) );
		for ( var i = 0; i < drives.Length; i++ )
			error = Math.Max( error, PairDifference( drives[ i ].Pair, fixture.GetProperty( "drives" )[ i ] ) );
		output.WriteLine( $"pairs-{index:00}: maximum absolute component error {error:R}" );
		Assert.True( float.IsFinite( error ) && error == 0f, $"Mixed constraint comparison error {error:R}" );
	}

	private static SolverBody Body( JsonElement data ) => new()
	{
		InverseMass = data.GetProperty( "inverseMass" ).GetSingle(),
		LinearVelocity = Vector( data.GetProperty( "linear" ) ),
		AngularVelocity = Vector( data.GetProperty( "angular" ) ),
		CorrectionVelocity = Vector( data.GetProperty( "correction" ) ),
		CorrectionAngularVelocity = Vector( data.GetProperty( "correctionAngular" ) )
	};

	private static ConstraintPair Pair( JsonElement data )
	{
		var rows = data.GetProperty( "rows" );
		return new ConstraintPair
		{
			BodyA = data.GetProperty( "bodyA" ).GetInt32(), BodyB = data.GetProperty( "bodyB" ).GetInt32(),
			OffsetA = Vector( rows[ 0 ] ), OffsetB = Vector( rows[ 1 ] ),
			LinearImpulse = Vector( rows[ 2 ] ), AngularImpulse = Vector( rows[ 3 ] ),
			LinearCoefficients = Matrix( rows, 4, 2 ), AngularCoefficients = Matrix( rows, 5, 2 ),
			LinearAxes = Matrix( rows, 12 ), AngularAxes = Matrix( rows, 15 ),
			InverseInertiaA = Matrix( rows, 18 ), InverseInertiaB = Matrix( rows, 21 )
		};
	}

	private static JointConstraint Joint( JsonElement data )
	{
		var rows = data.GetProperty( "rows" );
		return new JointConstraint
		{
			Pair = Pair( data ), PositiveLinearBias = Vector( rows[ 10 ] ), NegativeLinearBias = Vector( rows[ 11 ] ),
			PositiveAngularBias = new Vector3( rows[ 6 ][ 3 ].GetSingle(), rows[ 7 ][ 3 ].GetSingle(), rows[ 10 ][ 3 ].GetSingle() ),
			NegativeAngularBias = new Vector3( rows[ 8 ][ 3 ].GetSingle(), rows[ 9 ][ 3 ].GetSingle(), rows[ 11 ][ 3 ].GetSingle() )
		};
	}

	private static DriveConstraint Drive( JsonElement data )
	{
		var rows = data.GetProperty( "rows" );
		return new DriveConstraint
		{
			Pair = Pair( data ), LinearBias = Vector( rows[ 10 ] ), AngularBias = Vector( rows[ 11 ] ),
			LinearGain = rows[ 2 ][ 3 ].GetSingle(), AngularGain = rows[ 3 ][ 3 ].GetSingle(),
			LinearLimit = new Vector3( rows[ 6 ][ 3 ].GetSingle(), rows[ 7 ][ 3 ].GetSingle(), rows[ 10 ][ 3 ].GetSingle() ),
			AngularLimit = new Vector3( rows[ 8 ][ 3 ].GetSingle(), rows[ 9 ][ 3 ].GetSingle(), rows[ 11 ][ 3 ].GetSingle() )
		};
	}

	private static Matrix4x4 Matrix( JsonElement rows, int start, int stride = 1 )
	{
		var x = Vector( rows[ start ] );
		var y = Vector( rows[ start + stride ] );
		var z = Vector( rows[ start + stride * 2 ] );
		return new Matrix4x4( x.X, x.Y, x.Z, 0, y.X, y.Y, y.Z, 0, z.X, z.Y, z.Z, 0, 0, 0, 0, 1 );
	}

	private static float PairDifference( ConstraintPair actual, JsonElement data ) => Math.Max(
		Difference( actual.LinearImpulse, Vector( data.GetProperty( "expectedLinear" ) ) ),
		Difference( actual.AngularImpulse, Vector( data.GetProperty( "expectedAngular" ) ) ) );

	private static float Difference( Vector3 actual, Vector3 expected )
	{
		var delta = Vector3.Abs( actual - expected );
		return Math.Max( delta.X, Math.Max( delta.Y, delta.Z ) );
	}

	private static Vector3 Vector( JsonElement data ) => new( data[ 0 ].GetSingle(), data[ 1 ].GetSingle(), data[ 2 ].GetSingle() );
}
