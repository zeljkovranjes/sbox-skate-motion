using System.Numerics;
using System.Text.Json;
using SkateMotion.Core.Constraints;
using Xunit;
using Xunit.Abstractions;

namespace SboxSkateMotion.Tests;

public class ContactSolverTests( ITestOutputHelper output )
{
	[Theory]
	[InlineData( 0 )]
	[InlineData( 1 )]
	[InlineData( 2 )]
	[InlineData( 3 )]
	[InlineData( 4 )]
	[InlineData( 5 )]
	[InlineData( 6 )]
	[InlineData( 7 )]
	[InlineData( 8 )]
	[InlineData( 9 )]
	public void MatchesSyntheticSpuContactExecution( int index )
	{
		using var document = JsonDocument.Parse( File.ReadAllText(
			Path.Combine( AppContext.BaseDirectory, "Data", "contact-solver.json" ) ) );
		var fixture = document.RootElement.GetProperty( "cases" )[ index ];
		var bodyData = fixture.GetProperty( "bodies" );
		var bodies = new SolverBody[ bodyData.GetArrayLength() ];
		for ( var i = 0; i < bodies.Length; i++ )
		{
			var body = bodyData[ i ];
			bodies[ i ] = new()
			{
				InverseMass = body.GetProperty( "inverseMass" ).GetSingle(),
				LinearVelocity = Vector( body.GetProperty( "linear" ) ),
				AngularVelocity = Vector( body.GetProperty( "angular" ) ),
				CorrectionVelocity = Vector( body.GetProperty( "correction" ) ),
				CorrectionAngularVelocity = Vector( body.GetProperty( "correctionAngular" ) )
			};
		}

		var rows = fixture.GetProperty( "contacts" );
		var contacts = new ContactConstraint[ rows.GetArrayLength() ];
		for ( var i = 0; i < contacts.Length; i++ )
		{
			var row = rows[ i ];
			var coefficients = row.GetProperty( "coefficients" );
			var directions = row.GetProperty( "directions" );
			var angularA = row.GetProperty( "angularA" );
			var angularB = row.GetProperty( "angularB" );
			contacts[ i ] = new()
			{
				BodyA = row.GetProperty( "bodyA" ).GetInt32(),
				BodyB = row.GetProperty( "bodyB" ).GetInt32(),
				OffsetA = Vector( row.GetProperty( "offsetA" ) ),
				OffsetB = Vector( row.GetProperty( "offsetB" ) ),
				CoefficientX = Vector( coefficients[ 0 ] ),
				CoefficientY = Vector( coefficients[ 1 ] ),
				CoefficientZ = Vector( coefficients[ 2 ] ),
				Normal = Vector( directions[ 0 ] ),
				TangentA = Vector( directions[ 1 ] ),
				TangentB = Vector( directions[ 2 ] ),
				NormalAngularA = Vector( angularA[ 0 ] ),
				NormalAngularB = Vector( angularB[ 0 ] ),
				TangentAAngularA = Vector( angularA[ 1 ] ),
				TangentAAngularB = Vector( angularB[ 1 ] ),
				TangentBAngularA = Vector( angularA[ 2 ] ),
				TangentBAngularB = Vector( angularB[ 2 ] ),
				Bias = Impulse( row.GetProperty( "bias" ) ),
				Impulse = Impulse( row.GetProperty( "impulse" ) ),
				StaticFriction = row.GetProperty( "staticFriction" ).GetSingle(),
				SlidingFriction = row.GetProperty( "slidingFriction" ).GetSingle()
			};
		}

		ContactSolver.Solve( bodies, contacts, fixture.GetProperty( "passes" ).GetInt32() );
		var error = 0f;
		var expectedBodies = fixture.GetProperty( "expectedBodies" );
		for ( var i = 0; i < bodies.Length; i++ )
		{
			var expected = expectedBodies[ i ];
			error = Math.Max( error, Difference( bodies[ i ].LinearVelocity, Vector( expected.GetProperty( "linear" ) ) ) );
			error = Math.Max( error, Difference( bodies[ i ].AngularVelocity, Vector( expected.GetProperty( "angular" ) ) ) );
			error = Math.Max( error, Difference( bodies[ i ].CorrectionVelocity, Vector( expected.GetProperty( "correction" ) ) ) );
			error = Math.Max( error, Difference( bodies[ i ].CorrectionAngularVelocity, Vector( expected.GetProperty( "correctionAngular" ) ) ) );
		}
		var expectedImpulses = fixture.GetProperty( "expectedImpulses" );
		for ( var i = 0; i < contacts.Length; i++ )
		{
			var delta = Vector4.Abs( contacts[ i ].Impulse - Impulse( expectedImpulses[ i ] ) );
			error = Math.Max( error, Math.Max( Math.Max( delta.X, delta.Y ), Math.Max( delta.Z, delta.W ) ) );
		}

		output.WriteLine( $"{fixture.GetProperty( "name" ).GetString()}: maximum absolute component error {error:R}" );
		Assert.True( float.IsFinite( error ) && error <= 0.00001f, $"Synthetic comparison error {error:R}" );
	}

	private static Vector3 Vector( JsonElement value ) => new( value[ 0 ].GetSingle(), value[ 1 ].GetSingle(), value[ 2 ].GetSingle() );

	private static Vector4 Impulse( JsonElement value ) => new( Vector( value ), value[ 3 ].GetSingle() );

	private static float Difference( Vector3 actual, Vector3 expected )
	{
		var difference = Vector3.Abs( actual - expected );
		return Math.Max( difference.X, Math.Max( difference.Y, difference.Z ) );
	}
}
