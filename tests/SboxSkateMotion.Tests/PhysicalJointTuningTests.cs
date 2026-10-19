using System;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class PhysicalJointTuningTests
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
	public void LoadedTuningBuildsCapturedJointFramesAndLimits( int index )
	{
		using var document = JsonDocument.Parse( PhysicalJointTuningFixtures.Json );
		var sample = document.RootElement.GetProperty( "cases" )[ index ];
		var asset = new EffectorRigAsset( sample.GetProperty( "rig" ).Deserialize<EffectorRigDefinition>(), Clips() );
		var poses = sample.GetProperty( "input" ).GetProperty( "clipPoses" ).EnumerateArray()
			.Select( p => asset.Space.ToClip( new LimbRigidTransform( new( V( p[ 0 ] ), V( p[ 1 ] ), V( p[ 2 ] ) ), V( p[ 3 ] ) ) ) ).ToArray();
		var hierarchy = asset.InitializeHierarchy( poses );
		var joints = asset.PhysicalSettings.BuildAuthoredJoints( asset.Structural, hierarchy, MathProfile() );
		var expected = sample.GetProperty( "outputs" );
		Assert.Equal( expected.GetArrayLength(), joints.Length );
		for ( var i = 0; i < joints.Length; ++i )
		{
			var joint = joints[ i ];
			var row = expected[ i ];
			Assert.Equal( row.GetProperty( "bodies" )[ 0 ].GetInt32(), joint.ParentBone );
			Assert.Equal( row.GetProperty( "bodies" )[ 1 ].GetInt32(), joint.ChildBone );
			var geometry = row.GetProperty( "geometry" );
			Near( joint.Frame.ChildRotation, geometry, 0 );
			Near( new Vector4( joint.Frame.ChildPosition, 0f ), geometry, 4 );
			Near( joint.Frame.BodyRotation, geometry, 8 );
			Near( new Vector4( joint.Frame.BodyPosition, 0f ), geometry, 12 );
			Near( joint.LinearRotation, geometry, 16 );
			var limits = joint.Limits;
			var settings = row.GetProperty( "settings" );
			var actual = new[] { limits.LinearSpeed.X, limits.LinearSpeed.Y, limits.LinearSpeed.Z, limits.LinearSpeed.W,
				limits.SwingSpeed, limits.TwistSpeed, limits.SwingAngle, limits.TwistAngle, limits.SwingCosine, limits.TwistCosine };
			for ( var j = 0; j < actual.Length; ++j )
				Assert.InRange( MathF.Abs( actual[ j ] - F( settings[ j + 4 ] ) ), 0f, 1e-5f );
			Assert.Equal( settings[ 14 ].GetInt32(), limits.SwingMode );
			Assert.Equal( settings[ 15 ].GetInt32(), limits.TwistMode );
		}
	}

	[Fact]
	public void LoadedTuningOwnsItsValuesAndBuildsIndependentJoints()
	{
		var definition = Definition();
		var asset = new EffectorRigAsset( definition, Clips() );
		var tuning = asset.PhysicalSettings.JointConstruction;
		definition.PhysicalSettings.JointConstruction.LinearSpeed[ 0 ] = 900f;
		definition.PhysicalSettings.JointConstruction.LockSwing = true;
		Assert.Equal( tuning, asset.PhysicalSettings.JointConstruction );
		var hierarchy = asset.InitializeHierarchy( Enumerable.Repeat( LimbRigidTransform.Identity, 25 ).ToArray() );
		var first = asset.PhysicalSettings.BuildAuthoredJoints( asset.Structural, hierarchy, MathProfile() );
		var second = asset.PhysicalSettings.BuildAuthoredJoints( asset.Structural, hierarchy, MathProfile() );
		first[ 0 ] = default;
		Assert.NotEqual( first[ 0 ], second[ 0 ] );
	}

	[Fact]
	public void OlderRigsLoadButCannotInventMissingJointTuning()
	{
		var definition = Definition();
		definition.PhysicalSettings.JointConstruction = null;
		var asset = new EffectorRigAsset( definition, Clips() );
		Assert.Null( asset.PhysicalSettings.JointConstruction );
		Assert.Throws<InvalidOperationException>( () => asset.PhysicalSettings.BuildAuthoredJoints( asset.Structural,
			asset.InitializeHierarchy( Enumerable.Repeat( LimbRigidTransform.Identity, 25 ).ToArray() ), MathProfile() ) );
	}

	[Theory]
	[InlineData( "length" )]
	[InlineData( "linear" )]
	[InlineData( "swing" )]
	[InlineData( "twist" )]
	public void InvalidTuningFailsDuringLoading( string fault )
	{
		var definition = Definition();
		var tuning = definition.PhysicalSettings.JointConstruction;
		switch ( fault )
		{
			case "length": tuning.LinearSpeed = new float[ 3 ]; break;
			case "linear": tuning.LinearSpeed[ 3 ] = float.NaN; break;
			case "swing": tuning.SwingSpeed = float.PositiveInfinity; break;
			case "twist": tuning.TwistSpeed = float.NegativeInfinity; break;
		}
		Assert.Throws<ArgumentException>( () => new EffectorRigAsset( definition, Clips() ) );
	}

	private static EffectorRigDefinition Definition()
	{
		using var document = JsonDocument.Parse( PhysicalJointTuningFixtures.Json );
		return document.RootElement.GetProperty( "cases" )[ 0 ].GetProperty( "rig" ).Deserialize<EffectorRigDefinition>();
	}

	private static ClipSkeleton Clips() => new( Enumerable.Range( 0, 24 ).Reverse().Select( i => $"b{i}" ).Append( "helper" ).ToArray(),
		Enumerable.Range( 0, 25 ).Select( i => i is 0 or 23 ? -1 : i == 24 ? 0 : i - 1 ).ToArray(),
		new Vector3[ 25 ], Enumerable.Repeat( Quaternion.Identity, 25 ).ToArray() );

	private static LimbSolverMath MathProfile() => new( [ -.0013602249091491103f, .04165669530630112f, -.4999990165233612f ],
		[ -.0001950727018993348f, .00833207555115223f, -.16666652262210846f ], new float[ 8 ],
		.6366197466850281f, 1.570796251296997f, 7.549790126404332e-8f, 3.1415927410125732f );

	private static float F( JsonElement v ) => (float)v.GetDouble();
	private static Vector3 V( JsonElement v ) => new( F( v[ 0 ] ), F( v[ 1 ] ), F( v[ 2 ] ) );
	private static void Near( Vector4 value, JsonElement row, int offset )
	{
		var expected = new Vector4( F( row[ offset ] ), F( row[ offset + 1 ] ), F( row[ offset + 2 ] ), F( row[ offset + 3 ] ) );
		Assert.InRange( Vector4.Distance( value, expected ), 0f, 1e-5f );
	}
}
