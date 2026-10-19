using System;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class PhysicalRigDrivePresetTests
{
	[Fact]
	public void LoadingCopiesBothDistinctPresetsAndConstructionReturnsIndependentArrays()
	{
		var definition = Definition();
		definition.PhysicalSettings.DrivePresets = [ new( .2f, .3f, .4f, .5f ), new( .6f, .7f, .8f, .9f ) ];
		var asset = Load( JsonSerializer.Deserialize<EffectorRigDefinition>( JsonSerializer.Serialize( definition ) ) );
		var copied = Load( definition );
		var expected = definition.PhysicalSettings.DrivePresets.ToArray();
		definition.PhysicalSettings.DrivePresets[ 0 ] = default;
		Assert.Equal( expected, asset.CreateDrivePresets() );
		Assert.Equal( expected, copied.CreateDrivePresets() );
		var first = asset.CreateDrivePresets();
		first[ 1 ] = default;
		Assert.Equal( expected, asset.CreateDrivePresets() );
	}

	[Fact]
	public void LegacyRigLoadsWithoutInventingPresetsOrTargetSettings()
	{
		var definition = Definition();
		definition.PhysicalSettings.DrivePresets = null;
		definition.PhysicalSettings.BodyConstruction = null;
		definition.PhysicalSettings.CompoundBody = null;
		var asset = Load( definition );
		Assert.Throws<InvalidOperationException>( () => asset.CreateDrivePresets() );
		Assert.Throws<InvalidOperationException>( () => asset.CreateTargets( Vector3.Zero, 0 ) );
	}

	[Theory]
	[InlineData( -1 )]
	[InlineData( 0 )]
	[InlineData( 1 )]
	[InlineData( 2 )]
	[InlineData( 3 )]
	public void MalformedPresetsFailBeforeUse( int lane )
	{
		var definition = Definition();
		var lanes = new[] { 1f, 2f, 3f, 4f };
		if ( lane >= 0 )
			lanes[ lane ] = float.NaN;
		definition.PhysicalSettings.DrivePresets = lane < 0 ? new PhysicalJointPreset[ 1 ]
			: [ new( 1f, 2f, 3f, 4f ), new( lanes[ 0 ], lanes[ 1 ], lanes[ 2 ], lanes[ 3 ] ) ];
		Assert.Throws<ArgumentException>( () => Load( definition ) );
	}

	private static EffectorRigDefinition Definition()
	{
		using var document = JsonDocument.Parse( PhysicalJointTuningFixtures.Json );
		return document.RootElement.GetProperty( "cases" )[ 0 ].GetProperty( "rig" ).Deserialize<EffectorRigDefinition>();
	}

	private static PhysicalRigSettingsAsset Load( EffectorRigDefinition definition )
	{
		var clips = new ClipSkeleton( Enumerable.Range( 0, 24 ).Reverse().Select( i => $"b{i}" ).Append( "helper" ).ToArray(),
			Enumerable.Range( 0, 25 ).Select( i => i is 0 or 23 ? -1 : i == 24 ? 0 : i - 1 ).ToArray(),
			new Vector3[ 25 ], Enumerable.Repeat( Quaternion.Identity, 25 ).ToArray() );
		return new EffectorRigAsset( definition, clips ).PhysicalSettings;
	}
}
