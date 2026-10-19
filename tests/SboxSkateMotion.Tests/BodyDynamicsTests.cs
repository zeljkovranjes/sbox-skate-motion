using System.Numerics;
using System.Text.Json;
using SkateMotion.Core.Constraints;
using Xunit;
using Xunit.Abstractions;

namespace SboxSkateMotion.Tests;

public class BodyDynamicsTests( ITestOutputHelper output )
{
	public static IEnumerable<object[]> Cases => Enumerable.Range( 0, 24 ).Select( index => new object[] { index } );

	[Theory]
	[MemberData( nameof( Cases ) )]
	public void MatchesSyntheticSpuDynamicUpdate( int index )
	{
		using var document = JsonDocument.Parse( File.ReadAllText(
			Path.Combine( AppContext.BaseDirectory, "Data", "body-update.json" ) ) );
		var fixture = document.RootElement.GetProperty( "cases" )[ index ];
		var data = fixture.GetProperty( "body" );
		var properties = fixture.GetProperty( "properties" );
		var workspaceData = fixture.GetProperty( "workspace" );
		var body = new DynamicBody
		{
			Rotation = Rotation( data[ 0 ] ), Position = Vector( data[ 1 ] ),
			Velocity = Vector( data[ 2 ] ), AngularVelocity = Vector( data[ 3 ] ),
			InverseMass = data[ 7 ][ 3 ].GetSingle(),
			Acceleration = Vector( data[ 9 ] ), AngularAcceleration = Vector( data[ 10 ] ),
			MotionMetric = data[ 9 ][ 3 ].GetSingle(), QuietSteps = fixture.GetProperty( "sleepCount" ).GetInt32()
		};
		var workspace = new SolverBody
		{
			InverseMass = body.InverseMass,
			LinearVelocity = Vector( workspaceData[ 0 ] ), CorrectionVelocity = Vector( workspaceData[ 1 ] ),
			AngularVelocity = Vector( workspaceData[ 2 ] ), CorrectionAngularVelocity = Vector( workspaceData[ 3 ] )
		};
		var settings = new BodyDynamicsSettings( Vector( properties[ 0 ] ), properties[ 2 ][ 0 ].GetSingle(),
			properties[ 2 ][ 1 ].GetSingle(), properties[ 1 ][ 2 ].GetSingle(), properties[ 1 ][ 3 ].GetSingle(),
			properties[ 1 ][ 1 ].GetSingle(), fixture.GetProperty( "sleepThreshold" ).GetSingle(),
			fixture.GetProperty( "sleepCap" ).GetInt32() );
		BodyDynamics.Step( ref body, ref workspace, settings,
			fixture.GetProperty( "step" ).GetSingle(), Vector( fixture.GetProperty( "gravity" ) ) );
		var expected = fixture.GetProperty( "expectedBody" );
		var basis = Matrix4x4.CreateFromQuaternion( body.Rotation );
		var inertia = body.WorldInverseInertia;
		Vector3[] actual =
		[
			new Vector3( body.Rotation.X, body.Rotation.Y, body.Rotation.Z ), body.Position, body.Velocity, body.AngularVelocity,
			new Vector3( basis.M11, basis.M12, basis.M13 ), new Vector3( basis.M21, basis.M22, basis.M23 ),
			new Vector3( basis.M31, basis.M32, basis.M33 ), new Vector3( inertia.M11, inertia.M12, inertia.M13 ),
			new Vector3( inertia.M33, inertia.M22, inertia.M23 ), body.Acceleration, body.AngularAcceleration
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
		Assert.InRange( Math.Abs( body.Rotation.W - expected[ 0 ][ 3 ].GetSingle() ), 0f, 0.00001f );
		Assert.InRange( Math.Abs( body.MotionMetric - expected[ 9 ][ 3 ].GetSingle() ), 0f, 0.00001f );
		Assert.Equal( fixture.GetProperty( "expectedSleepCount" ).GetInt32(), body.QuietSteps );
		Assert.Equal( Vector3.Zero, workspace.LinearVelocity );
		Assert.Equal( Vector3.Zero, workspace.AngularVelocity );
		Assert.Equal( Vector3.Zero, workspace.CorrectionVelocity );
		Assert.Equal( Vector3.Zero, workspace.CorrectionAngularVelocity );
		Assert.Equal( body.InverseMass, workspace.InverseMass );
		error = Math.Max( error, Math.Abs( body.Rotation.W - expected[ 0 ][ 3 ].GetSingle() ) );
		error = Math.Max( error, Math.Abs( body.MotionMetric - expected[ 9 ][ 3 ].GetSingle() ) );
		output.WriteLine( $"update-{index:00}: maximum absolute component error {error:R}" );
	}

	[Fact]
	public void SeparationCorrectionMovesPoseWithoutAddingKineticVelocity()
	{
		var body = new DynamicBody { Rotation = Quaternion.Identity, InverseMass = 1f };
		var workspace = new SolverBody
		{
			InverseMass = 1f, CorrectionVelocity = Vector3.UnitZ * 0.1f,
			CorrectionAngularVelocity = Vector3.UnitX * 0.2f
		};
		var settings = new BodyDynamicsSettings( Vector3.One, 0f, 0f, 100f, 100f, 1f, 0.01f, 10 );
		BodyDynamics.Step( ref body, ref workspace, settings, 1f / 60f, Vector3.Zero );
		Assert.Equal( Vector3.UnitZ * 0.1f, body.Position );
		Assert.True( body.Rotation.X > 0f );
		Assert.InRange( body.Rotation.Length(), 0.99999f, 1.00001f );
		Assert.Equal( Vector3.Zero, body.Velocity );
		Assert.Equal( Vector3.Zero, body.AngularVelocity );
		Assert.Equal( 0f, body.MotionMetric );
		Assert.Equal( 1, body.QuietSteps );
	}

	private static Vector3 Vector( JsonElement value ) => new( value[ 0 ].GetSingle(), value[ 1 ].GetSingle(), value[ 2 ].GetSingle() );

	private static Quaternion Rotation( JsonElement value ) => new( Vector( value ), value[ 3 ].GetSingle() );
}
