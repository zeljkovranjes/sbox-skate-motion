using System.Numerics;
using System.Text.Json;
using SkateMotion.Core.Constraints;
using Xunit;
using Xunit.Abstractions;

namespace SboxSkateMotion.Tests;

public class ContactInputTests( ITestOutputHelper output )
{
	public static IEnumerable<object[]> Cases => Enumerable.Range( 0, 24 ).Select( index => new object[] { index } );

	[Theory]
	[MemberData( nameof( Cases ) )]
	public void MatchesCollisionWriterBodyChannelsAndRelativePointVelocity( int index )
	{
		using var document = JsonDocument.Parse( File.ReadAllText(
			Path.Combine( AppContext.BaseDirectory, "Data", "contact-inputs.json" ) ) );
		var fixture = document.RootElement.GetProperty( "cases" )[ index ];
		var a = Body( fixture.GetProperty( "bodyA" ), fixture.GetProperty( "respondsA" ).GetBoolean() );
		var b = Body( fixture.GetProperty( "bodyB" ), fixture.GetProperty( "respondsB" ).GetBoolean() );
		var contact = new CollisionContact( 0, 1, Vector( fixture.GetProperty( "pointA" ) ),
			Vector( fixture.GetProperty( "pointB" ) ), Vector3.UnitZ, Vector3.UnitX, Vector3.UnitY, 0.6f, 0.4f, 0.2f );
		var inputs = ContactInputs.FromBodies( a, b, contact );
		var expected = fixture.GetProperty( "expected" );
		(int Row, Vector3 Actual)[] comparisons =
		[
			(0, inputs.Geometry.PointA), (1, inputs.Geometry.PointB), (5, inputs.Geometry.MotionTerm),
			(6, inputs.A.Center), (7, inputs.B.Center),
			(8, new Vector3( inputs.A.InverseInertia.M11, inputs.A.InverseInertia.M12, inputs.A.InverseInertia.M13 )),
			(9, new Vector3( inputs.B.InverseInertia.M11, inputs.B.InverseInertia.M12, inputs.B.InverseInertia.M13 )),
			(10, new Vector3( inputs.A.InverseInertia.M33, inputs.A.InverseInertia.M22, inputs.A.InverseInertia.M23 )),
			(11, new Vector3( inputs.B.InverseInertia.M33, inputs.B.InverseInertia.M22, inputs.B.InverseInertia.M23 )),
			(12, inputs.A.LinearMotion), (13, inputs.B.LinearMotion),
			(14, inputs.A.AngularMotion), (15, inputs.B.AngularMotion)
		];
		var maximum = 0f;
		foreach ( var comparison in comparisons )
		{
			var reference = Vector( expected[ comparison.Row ] );
			var delta = Vector3.Abs( comparison.Actual - reference );
			var error = Math.Max( delta.X, Math.Max( delta.Y, delta.Z ) );
			Assert.True( float.IsFinite( error ) && error <= 0.00001f,
				$"{index} row {comparison.Row}: actual {comparison.Actual}, expected {reference}, error {error:R}" );
			maximum = Math.Max( maximum, error );
		}
		Assert.Equal( a.InverseMass, inputs.A.EffectiveInverseMass );
		Assert.Equal( b.InverseMass, inputs.B.EffectiveInverseMass );
		Assert.Equal( fixture.GetProperty( "respondsA" ).GetBoolean(), inputs.A.Responds );
		Assert.Equal( fixture.GetProperty( "respondsB" ).GetBoolean(), inputs.B.Responds );
		output.WriteLine( $"contact-input-{index:00}: maximum absolute component error {maximum:R}" );
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
			Position = Vector( data[ 1 ] ), Rotation = new Quaternion( Vector( data[ 0 ] ), data[ 0 ][ 3 ].GetSingle() ),
			Velocity = Vector( data[ 2 ] ), AngularVelocity = Vector( data[ 3 ] ),
			Acceleration = Vector( data[ 9 ] ), AngularAcceleration = Vector( data[ 10 ] ),
			InverseMass = responds ? first[ 3 ].GetSingle() : 0f,
			WorldInverseInertia = new Matrix4x4( xx, xy, xz, 0, xy, yy, yz, 0, xz, yz, zz, 0, 0, 0, 0, 1 )
		};
	}

	private static Vector3 Vector( JsonElement value ) => new( value[ 0 ].GetSingle(), value[ 1 ].GetSingle(), value[ 2 ].GetSingle() );
}
