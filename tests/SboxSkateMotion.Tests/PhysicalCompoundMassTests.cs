using System.Numerics;
using System.Text.Json;
using SkateMotion.Core.Animation;
using SkateMotion.Core.Physics;
using Xunit;

namespace SboxSkateMotion.Tests;

public class PhysicalCompoundMassTests
{
	public static IEnumerable<object[]> Cases => Enumerable.Range( 0, 24 ).Select( index => new object[] { index } );

	[Theory]
	[MemberData( nameof( Cases ) )]
	public void MatchesCapturedMomentsAxesAndMassDescriptor( int index )
	{
		using var document = JsonDocument.Parse( PhysicalCompoundMassFixtures.Json );
		var sample = document.RootElement.GetProperty( "cases" )[ index ];
		var children = sample.GetProperty( "geometry" );
		var frame = children[ 0 ].GetProperty( "frame" );
		var dimensions = children[ 0 ].GetProperty( "dimensions" );
		var geometry = new PhysicalCompoundBodyGeometry( Frame( frame ), dimensions[ 1 ].GetSingle(),
			dimensions[ 0 ].GetSingle(), children[ 1 ].GetProperty( "radius" ).GetSingle(), 0f, 0f );
		var stages = sample.GetProperty( "stages" );
		var moments = PhysicalCompoundMassProperties.CreateMoments( geometry );
		var expectedMoments = stages.GetProperty( "moments" );
		Assert.Equal( Vector4( expectedMoments[ 0 ] ), moments.X );
		Assert.Equal( Vector4( expectedMoments[ 1 ] ), moments.Y );
		Assert.Equal( Vector4( expectedMoments[ 2 ] ), moments.Z );
		Assert.Equal( Vector4( expectedMoments[ 3 ] ), moments.W );
		var center = new Vector3( moments.W.X, moments.W.Y, moments.W.Z ) * LimbSolverMath.Reciprocal( moments.W.W );
		Span<float> inertia = stackalloc float[ 9 ];
		Span<float> axes = stackalloc float[ 9 ];
		moments.Transform( new( LimbRigidTransform.Identity.Rotation, -center ) ).WriteInertia( inertia );
		CheckMatrix( stages.GetProperty( "inertiaBefore" ), inertia );
		var remaining = PhysicalInertiaDiagonalization.Solve( inertia, axes,
			stages.GetProperty( "tolerance" ).GetSingle(), stages.GetProperty( "passes" ).GetInt32() );
		Assert.Equal( stages.GetProperty( "passesRemaining" ).GetInt32(), remaining );
		CheckMatrix( stages.GetProperty( "inertiaAfter" ), inertia );
		CheckMatrix( stages.GetProperty( "axes" ), axes );
		var properties = PhysicalCompoundMassProperties.Create( geometry );
		Assert.Equal( Frame( stages.GetProperty( "massFrame" ) ), properties.Frame );
		Assert.Equal( stages.GetProperty( "volume" ).GetSingle(), properties.Properties.Volume );
		var expected = sample.GetProperty( "output" );
		var descriptor = PhysicalBodyMassConstruction.Create( properties.Properties, properties.Frame,
			expected.GetProperty( "requested" ).GetSingle(), expected.GetProperty( "density" ).GetSingle(), .001f );
		Assert.Equal( Vector( expected.GetProperty( "inverseInertia" ) ), descriptor.Mass.InverseInertia );
		Assert.Equal( expected.GetProperty( "inverseMass" ).GetSingle(), descriptor.Mass.InverseMass );
		Assert.Equal( expected.GetProperty( "maximumInertia" ).GetSingle(), descriptor.MaximumInertia );
		Assert.Equal( expected.GetProperty( "volume" ).GetSingle(), descriptor.Volume );
		Assert.Equal( Frame( expected.GetProperty( "offset" ) ), descriptor.LocalOffset );
		Assert.True( descriptor.HasShapeProperties );
	}

	private static void CheckMatrix( JsonElement expected, ReadOnlySpan<float> actual )
	{
		for ( var row = 0; row < 3; ++row )
			for ( var column = 0; column < 3; ++column )
				Assert.Equal( expected[ row ][ column ].GetSingle(), actual[ row * 3 + column ] );
	}

	private static Vector3 Vector( JsonElement value ) => new( value[ 0 ].GetSingle(), value[ 1 ].GetSingle(), value[ 2 ].GetSingle() );
	private static Vector4 Vector4( JsonElement value ) => new( Vector( value ), value[ 3 ].GetSingle() );
	private static LimbRigidTransform Frame( JsonElement value ) => new( new( Vector( value[ 0 ] ), Vector( value[ 1 ] ), Vector( value[ 2 ] ) ), Vector( value[ 3 ] ) );
}
