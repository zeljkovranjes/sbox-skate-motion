using System;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using SkateMotion.Core.Animation;
using SkateMotion.Core.Physics;
using Xunit;

namespace SboxSkateMotion.Tests;

public class PhysicalRigSettingsTests
{
	[Theory]
	[InlineData( 0 )]
	[InlineData( 1 )]
	[InlineData( 2 )]
	[InlineData( 3 )]
	[InlineData( 4 )]
	public void LoadedSettingsMatchCapturedConstructionAndModeData( int index )
	{
		using var document = JsonDocument.Parse( PhysicalRigSettingsFixtures.Json );
		var sample = document.RootElement.GetProperty( "cases" )[ index ];
		var rig = new EffectorRigAsset( sample.GetProperty( "rig" ).Deserialize<EffectorRigDefinition>(), Clips() );
		var settings = rig.PhysicalSettings;
		var expected = sample.GetProperty( "expected" );
		var feedback = new PhysicalRigFeedbackState();
		settings.EnterPrimaryFeedback( feedback );
		var drives = Enumerable.Range( 0, 24 ).Reverse().Select( i => new PhysicalJointState { ChildBone = i } ).ToArray();
		settings.ApplyJointWeights( drives );
		for ( var i = 0; i < 24; ++i )
		{
			var row = expected.GetProperty( "bodies" )[ i ];
			var bone = settings.Bones[ i ];
			var actual = new float[] { bone.Body.PrimaryMassCoefficient, bone.Body.AlternateMassCoefficient,
				bone.FirstFlag, bone.SecondFlag, bone.Body.Shape, bone.Body.SizeScale, bone.BodyValue,
				bone.DriveCoefficients.X, bone.DriveCoefficients.Y, bone.DriveCoefficients.Z, bone.DriveCoefficients.W,
				bone.DriveValue, bone.FirstWeight, bone.SecondWeight, bone.FeedbackFlag, bone.FeedbackWeight };
			for ( var j = 0; j < actual.Length; ++j )
				SameBits( F( row[ j ] ), actual[ j ] );
			Assert.Equal( new Vector3( 1f, 2f, 3f ), bone.Body.Dimensions );
			Assert.Equal( i == 0, bone.Body.IsRoot );
			SameBits( F( row[ 12 ] ), drives[ 23 - i ].FirstWeight );
			SameBits( F( row[ 13 ] ), drives[ 23 - i ].SecondWeight );
			Assert.Equal( i == 0 ? (byte)1 : row[ 14 ].GetByte(), feedback.AuthoredFlags[ i ] );
			SameBits( i == 0 ? 0f : F( row[ 15 ] ), feedback.AuthoredWeights[ i ] );
		}
		var hierarchy = rig.InitializeHierarchy( Enumerable.Repeat( LimbRigidTransform.Identity, 25 ).ToArray() );
		var joints = settings.BuildJoints( rig.Structural, hierarchy, new( new( 2f, 3f, 4f, 5f ), 12f, 18f, false, false ), MathProfile() );
		Assert.Equal( 22, joints.Length );
		for ( var i = 0; i < 22; ++i )
		{
			var row = expected.GetProperty( "joints" )[ i ];
			var range = settings.Ranges[ i ];
			SameBits( F( row[ 1 ] ), range.PrimarySwing );
			SameBits( F( row[ 2 ] ), range.PrimaryTwist );
			SameBits( F( row[ 3 ] ), range.AlternateSwing );
			SameBits( F( row[ 4 ] ), range.AlternateTwist );
			SameBits( row[ 0 ].GetBoolean() ? 0f : .4f * F( row[ 1 ] ), joints[ i ].Limits.SwingAngle );
			SameBits( row[ 0 ].GetBoolean() ? 0f : .6f * F( row[ 2 ] ), joints[ i ].Limits.TwistAngle );
		}
		for ( var i = 0; i < 3; ++i )
		{
			var row = expected.GetProperty( "contacts" )[ i ];
			Assert.Equal( new Vector3( F( row[ 0 ] ), F( row[ 1 ] ), F( row[ 2 ] ) ), settings.Contacts[ i ] );
		}
	}

	[Fact]
	public void LoadedMassCoefficientsReachBodyConstruction()
	{
		var asset = Load();
		var definition = asset.PhysicalSettings.Bones[ 5 ].Body;
		var actual = PhysicalRigBodyConstruction.Create( definition, new( .2f, .1f, 2f, 1f, 1f, 1f, 0, 1f, .0001f ) );
		SameBits( 1f / (12f * definition.PrimaryMassCoefficient), actual.Primary.InverseMass );
		SameBits( 1f / (12f * definition.AlternateMassCoefficient), actual.Alternate.InverseMass );
	}

	[Fact]
	public void EachModeOwnerReceivesItsOwnContactValues()
	{
		var settings = Load().PhysicalSettings;
		var mass = new PhysicalBodyMass( Vector3.One, 1f );
		PhysicalRigModeBody[] Bodies() => Enumerable.Range( 0, 26 )
			.Select( _ => new PhysicalRigModeBody( new PhysicalBodyState { Mass = mass }, mass, mass ) ).ToArray();
		var first = settings.CreateBodyModes( Bodies(), false );
		var second = settings.CreateBodyModes( Bodies(), true );
		first.ContactProperties[ 1 ] = Vector3.Zero;
		Assert.Equal( settings.Contacts[ 1 ], second.ContactProperties[ 1 ] );
		Assert.NotEqual( Vector3.Zero, settings.Contacts[ 1 ] );
	}

	[Fact]
	public void InputMutationDoesNotChangeLoadedData()
	{
		var definition = Definition();
		var asset = new EffectorRigAsset( definition, Clips() );
		var before = asset.PhysicalSettings.Bones[ 1 ];
		var range = asset.PhysicalSettings.Ranges[ 0 ];
		var contact = asset.PhysicalSettings.Contacts[ 0 ];
		definition.PhysicalSettings.Bones[ 1 ].DriveCoefficients[ 0 ] = 500f;
		definition.PhysicalSettings.Bones[ 1 ].BodyFlags[ 0 ] = 99;
		definition.PhysicalSettings.Bones[ 1 ].FeedbackWeight = 500f;
		definition.PhysicalSettings.Joints[ 0 ].Ranges[ 0 ] = 500f;
		definition.PhysicalSettings.Contacts[ 0 ][ 0 ] = 500f;
		definition.Bones[ 1 ].BodySize[ 0 ] = 500f;
		Assert.Equal( before, asset.PhysicalSettings.Bones[ 1 ] );
		Assert.Equal( range, asset.PhysicalSettings.Ranges[ 0 ] );
		Assert.Equal( contact, asset.PhysicalSettings.Contacts[ 0 ] );
		var feedback = new PhysicalRigFeedbackState();
		asset.PhysicalSettings.EnterPrimaryFeedback( feedback );
		SameBits( before.FeedbackWeight, feedback.AuthoredWeights[ 1 ] );
	}

	[Fact]
	public void InvalidJointIndicesFailBeforeAnyWeightChanges()
	{
		var first = new PhysicalJointState { ChildBone = 1, FirstWeight = 123f };
		Assert.Throws<ArgumentException>( () => Load().PhysicalSettings.ApplyJointWeights( [ first, new() { ChildBone = 24 } ] ) );
		Assert.Equal( 123f, first.FirstWeight );
	}

	[Fact]
	public void OlderRigsKeepSettingsAbsent()
	{
		var definition = Definition();
		definition.PhysicalSettings = null;
		Assert.Null( new EffectorRigAsset( definition, Clips() ).PhysicalSettings );
	}

	[Theory]
	[InlineData( "version" )]
	[InlineData( "bones" )]
	[InlineData( "joints" )]
	[InlineData( "contacts" )]
	[InlineData( "name" )]
	[InlineData( "missing" )]
	[InlineData( "shape" )]
	[InlineData( "mass" )]
	[InlineData( "size" )]
	[InlineData( "flags" )]
	[InlineData( "coefficients" )]
	[InlineData( "weight" )]
	[InlineData( "range" )]
	[InlineData( "contact" )]
	public void MalformedSettingsFailDuringAssetLoading( string fault )
	{
		var definition = Definition();
		var settings = definition.PhysicalSettings;
		switch ( fault )
		{
			case "version": settings.Version = 2; break;
			case "bones": settings.Bones = new PhysicalRigBoneSettingsDefinition[ 23 ]; break;
			case "joints": settings.Joints = null; break;
			case "contacts": settings.Contacts = new float[ 2 ][]; break;
			case "name": settings.Bones[ 1 ].ClipBone = "b2"; break;
			case "missing": settings.Bones[ 1 ] = null; break;
			case "shape": settings.Bones[ 1 ].Shape = 4; break;
			case "mass": settings.Bones[ 1 ].PrimaryMassCoefficient = 0f; break;
			case "size": settings.Bones[ 1 ].SizeScale = float.PositiveInfinity; break;
			case "flags": settings.Bones[ 1 ].BodyFlags = new int[ 1 ]; break;
			case "coefficients": settings.Bones[ 1 ].DriveCoefficients = new float[ 3 ]; break;
			case "weight": settings.Bones[ 1 ].FirstWeight = float.NaN; break;
			case "range": settings.Joints[ 0 ].Ranges[ 1 ] = float.NaN; break;
			case "contact": settings.Contacts[ 0 ] = new float[ 2 ]; break;
		}
		Assert.Throws<ArgumentException>( () => new EffectorRigAsset( definition, Clips() ) );
	}

	private static EffectorRigAsset Load() => new( Definition(), Clips() );

	private static EffectorRigDefinition Definition()
	{
		using var document = JsonDocument.Parse( PhysicalRigSettingsFixtures.Json );
		return document.RootElement.GetProperty( "cases" )[ 0 ].GetProperty( "rig" ).Deserialize<EffectorRigDefinition>();
	}

	private static ClipSkeleton Clips() => new( Enumerable.Range( 0, 24 ).Reverse().Select( i => $"b{i}" ).Append( "helper" ).ToArray(),
		Enumerable.Range( 0, 25 ).Select( i => i is 0 or 23 ? -1 : i == 24 ? 0 : i - 1 ).ToArray(),
		new Vector3[ 25 ], Enumerable.Repeat( Quaternion.Identity, 25 ).ToArray() );

	private static LimbSolverMath MathProfile() => new( [ -1f / 720f, 1f / 24f, -.5f ],
		[ -1f / 5040f, 1f / 120f, -1f / 6f ], new float[ 8 ], 2f / MathF.PI, MathF.PI / 2f, 0f, MathF.PI );

	private static float F( JsonElement value ) => (float)value.GetDouble();
	private static void SameBits( float expected, float actual ) => Assert.Equal( BitConverter.SingleToInt32Bits( expected ), BitConverter.SingleToInt32Bits( actual ) );
}
