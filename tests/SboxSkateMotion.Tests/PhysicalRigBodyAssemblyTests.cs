using System;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using SkateMotion.Core.Animation;
using SkateMotion.Core.Physics;
using Xunit;

namespace SboxSkateMotion.Tests;

public class PhysicalRigBodyAssemblyTests
{
	[Theory]
	[InlineData( 0 )]
	[InlineData( 1 )]
	[InlineData( 2 )]
	[InlineData( 3 )]
	[InlineData( 4 )]
	[InlineData( 5 )]
	public void ConstructsAllCanonicalAndFeedbackBodiesThroughRegistration( int index )
	{
		using var document = JsonDocument.Parse( PhysicalRigBodyAssemblyFixtures.Json );
		var sample = document.RootElement.GetProperty( "cases" )[ index ];
		var asset = new EffectorRigAsset( sample.GetProperty( "rig" ).Deserialize<EffectorRigDefinition>(), Clips() );
		var compound = sample.GetProperty( "compound" ).GetBoolean();
		var acceleration = V( sample.GetProperty( "acceleration" ) );
		var assembly = asset.PhysicalSettings.CreateBodies( compound, MathProfile(), acceleration );
		Assert.Equal( 26, assembly.Bindings.Length );
		Assert.Equal( 26, assembly.ModeBodies.Length );
		Assert.Equal( compound, assembly.Compound.HasValue );
		for ( var i = 0; i < 26; ++i )
		{
			var binding = assembly.Bindings[ i ];
			var body = binding.Body;
			var mode = assembly.ModeBodies[ i ];
			var expected = sample.GetProperty( "bodies" )[ i ];
			var factory = sample.GetProperty( "factory" )[ i ];
			Assert.Same( body, mode.Body );
			for ( var prior = 0; prior < i; ++prior )
				Assert.NotSame( body, assembly.Bindings[ prior ].Body );
			Assert.Equal( expected.GetProperty( "offset" ).GetBoolean(), binding.LocalOffset.HasValue );
			Assert.Equal( LimbRigidTransform.Identity, binding.CachedPose );
			Assert.Equal( LimbRigidTransform.Identity, binding.Read() );
			if ( binding.LocalOffset is { } offset )
				Assert.Equal( LimbRigidTransform.Identity, offset );
			Assert.Equal( V( expected.GetProperty( "pose" )[ 0 ] ), body.Pose.Rotation.X );
			Assert.Equal( V( expected.GetProperty( "pose" )[ 1 ] ), body.Pose.Rotation.Y );
			Assert.Equal( V( expected.GetProperty( "pose" )[ 2 ] ), body.Pose.Rotation.Z );
			Assert.Equal( V( expected.GetProperty( "position" ) ), body.Pose.Translation );
			var q = expected.GetProperty( "orientation" );
			Assert.Equal( new Vector4( F( q[ 0 ] ), F( q[ 1 ] ), F( q[ 2 ] ), F( q[ 3 ] ) ), body.Orientation );
			Assert.Equal( V( expected.GetProperty( "linear" ) ), body.Velocity );
			Assert.Equal( V( expected.GetProperty( "angular" ) ), body.AngularVelocity );
			Assert.Equal( V( expected.GetProperty( "accumulator" ) ), body.LinearAccumulator );
			Assert.Equal( V( expected.GetProperty( "angularAccumulator" ) ), body.AngularAccumulator );
			Assert.Equal( acceleration, body.DefaultLinearAccumulator );
			Assert.Equal( expected.GetProperty( "mode" ).GetInt32(), body.MotionMode );
			Assert.Equal( expected.GetProperty( "state" ).GetInt32(), body.AccumulatorState );
			Same( F( expected.GetProperty( "metric" ) ), body.MotionMetric );
			var tensor = expected.GetProperty( "tensor" );
			Assert.Equal( new Vector3( F( tensor[ 0 ] ), F( tensor[ 5 ] ), F( tensor[ 4 ] ) ), body.InverseInertiaDiagonal );
			Assert.Equal( new Vector3( F( tensor[ 1 ] ), F( tensor[ 2 ] ), F( tensor[ 6 ] ) ), body.InverseInertiaOffDiagonal );
			Same( F( tensor[ 3 ] ), body.InverseMass );
			Assert.Equal( V( factory.GetProperty( "descriptorInverseInertia" ) ), body.Mass.Value.InverseInertia );
			Same( F( factory.GetProperty( "descriptorInverseMass" ) ), body.Mass.Value.InverseMass );
			Same( F( factory.GetProperty( "maximumInertia" ) ), mode.MaximumInertia );
			Same( F( factory.GetProperty( "linearDamping" ) ), mode.LinearDamping );
			Same( F( factory.GetProperty( "angularDamping" ) ), mode.AngularDamping );
			Assert.Equal( factory.GetProperty( "shapeFlags" ).GetUInt32(), mode.ShapeFlags );
			if ( i < 24 )
			{
				Assert.Equal( V( factory.GetProperty( "primaryInverseInertia" ) ), mode.Primary.InverseInertia );
				Assert.Equal( V( factory.GetProperty( "alternateInverseInertia" ) ), mode.Alternate.InverseInertia );
				Same( F( factory.GetProperty( "primaryInverseMass" ) ), mode.Primary.InverseMass );
				Same( F( factory.GetProperty( "alternateInverseMass" ) ), mode.Alternate.InverseMass );
				Same( F( factory.GetProperty( "poseVolume" ) ), assembly.PoseVolumes[ i ] );
			}
			else
			{
				Same( F( factory.GetProperty( "radius" ) ), assembly.Feedback[ i - 24 ].Radius );
				Same( F( factory.GetProperty( "halfLength" ) ), assembly.Feedback[ i - 24 ].HalfLength );
				Assert.False( assembly.Feedback[ i - 24 ].CollisionEnabled );
			}
		}
	}

	[Fact]
	public void SeparateOwnersNeverShareBodiesAndModesUseConstructedMasses()
	{
		var settings = new EffectorRigAsset( Definition(), Clips() ).PhysicalSettings;
		var first = settings.CreateBodies( false, MathProfile(), Vector3.Zero );
		var second = settings.CreateBodies( false, MathProfile(), Vector3.Zero );
		var modes = settings.CreateBodyModes( first.ModeBodies, false );
		modes.ApplyPrimary( true );
		for ( var i = 0; i < 26; ++i )
		{
			Assert.NotSame( first.Bindings[ i ].Body, second.Bindings[ i ].Body );
			if ( i is > 0 and < 24 )
				Assert.Equal( first.ModeBodies[ i ].Primary, first.Bindings[ i ].Body.Mass );
		}
	}

	[Fact]
	public void MissingConstructionDataCannotSilentlyUseEstimatedDefaults()
	{
		var definition = Definition();
		definition.PhysicalSettings.BodyConstruction = null;
		var settings = new EffectorRigAsset( definition, Clips() ).PhysicalSettings;
		Assert.Throws<ArgumentException>( () => settings.CreateBodies( false, MathProfile(), Vector3.Zero ) );
	}

	[Fact]
	public void CompoundBranchRequiresItsOwnAuthoredGeometry()
	{
		var settings = new EffectorRigAsset( Definition(), Clips() ).PhysicalSettings;
		Assert.Throws<ArgumentException>( () => settings.CreateBodies( true, MathProfile(), Vector3.Zero ) );
	}

	[Theory]
	[InlineData( "scale" )]
	[InlineData( "mode" )]
	[InlineData( "tolerance" )]
	[InlineData( "compound" )]
	[InlineData( "acceleration" )]
	public void InvalidConstructionInputsAreRejected( string fault )
	{
		var definition = Definition();
		var tuning = definition.PhysicalSettings.BodyConstruction.Value;
		switch ( fault )
		{
			case "scale": definition.PhysicalSettings.BodyConstruction = tuning with { MassScale = 0f }; break;
			case "mode": definition.PhysicalSettings.BodyConstruction = tuning with { InertiaMode = 3 }; break;
			case "tolerance": definition.PhysicalSettings.BodyConstruction = tuning with { AxisTolerance = float.NaN }; break;
			case "compound": definition.PhysicalSettings.CompoundBody = new() { Angles = new float[ 2 ] }; break;
		}
		Assert.Throws<ArgumentException>( () =>
		{
			var settings = new EffectorRigAsset( definition, Clips() ).PhysicalSettings;
			settings.CreateBodies( false, MathProfile(), fault == "acceleration" ? new Vector3( float.NaN ) : Vector3.Zero );
		} );
	}

	private static EffectorRigDefinition Definition()
	{
		using var document = JsonDocument.Parse( PhysicalRigBodyAssemblyFixtures.Json );
		return document.RootElement.GetProperty( "cases" )[ 0 ].GetProperty( "rig" ).Deserialize<EffectorRigDefinition>();
	}

	private static ClipSkeleton Clips() => new( Enumerable.Range( 0, 24 ).Select( i => $"b{i}" ).Append( "helper" ).ToArray(),
		Enumerable.Repeat( -1, 25 ).ToArray(), new Vector3[ 25 ], Enumerable.Repeat( Quaternion.Identity, 25 ).ToArray() );

	private static LimbSolverMath MathProfile() => new( [ -.0013602249091491103f, .04165669530630112f, -.4999990165233612f ],
		[ -.0001950727018993348f, .00833207555115223f, -.16666652262210846f ], new float[ 8 ],
		.6366197466850281f, 1.570796251296997f, 7.549790126404332e-8f, 3.1415927410125732f );

	private static float F( JsonElement v ) => (float)v.GetDouble();
	private static Vector3 V( JsonElement v ) => new( F( v[ 0 ] ), F( v[ 1 ] ), F( v[ 2 ] ) );
	private static void Same( float expected, float actual ) => Assert.Equal( BitConverter.SingleToInt32Bits( expected ), BitConverter.SingleToInt32Bits( actual ) );
}
