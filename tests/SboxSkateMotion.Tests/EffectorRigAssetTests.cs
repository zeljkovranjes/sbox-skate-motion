using System;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class EffectorRigAssetTests
{
	[Fact]
	public void ResolvesClipHelpersAndDistinctToeAnkleSolverIndices()
	{
		var asset = new EffectorRigAsset( Definition(), Clips() );
		Assert.Equal( new[] { 2, 0, 1 }, asset.SolverToClip );
		Assert.Equal( new[] { 3, 3, 3, 3 }, asset.HelperClipBones );
		Assert.Equal( new[] { 0, 0, 0, 0 }, asset.EndBones );
		Assert.Equal( new[] { 2, 2, -1, -1 }, asset.ReferenceBones );
	}

	[Fact]
	public void CopiesBindDataWithoutConvertingItsRigAxesOrNormalizingIt()
	{
		var definition = Definition();
		var asset = new EffectorRigAsset( definition, Clips() );
		definition.Bones[ 0 ].BindPosition[ 0 ] = 100f;
		definition.Bones[ 0 ].BindRotation[ 3 ] = 100f;
		Assert.Equal( new Vector3( 1f, 2f, 3f ), asset.BindPositions[ 0 ] );
		Assert.Equal( new Quaternion( 0f, 0f, 0f, 2f ), asset.BindRotations[ 0 ] );
		Assert.Equal( new Vector3( 4f, 5f, 6f ), asset.BodySizes[ 0 ] );
	}

	[Fact]
	public void InitializationConvertsClipPosesToRigSpaceAndUsesMappedParentage()
	{
		var asset = new EffectorRigAsset( Definition(), Clips() );
		var poses = Enumerable.Repeat( LimbRigidTransform.Identity, 4 ).ToArray();
		poses[ 2 ] = new LimbRigidTransform( LimbRigidTransform.Identity.Rotation, new Vector3( 2f, 3f, 5f ) );
		var result = asset.InitializeHierarchy( poses );
		Assert.Equal( new Vector3( 3f, 5f, 2f ), result.InitialBones[ 0 ].Translation );
		Assert.Equal( new[] { 2, -1, 1 }, result.Parents );
		Assert.Equal( new Vector3( 1f, 2f, 3f ), result.BindTransforms[ 0 ].Translation );
		Assert.Equal( new Vector3( 2f, 3f, 5f ), poses[ 2 ].Translation );
	}

	[Fact]
	public void GraphJsonRetainsRigMetadataAndOlderGraphsRemainValid()
	{
		var graph = new AnimationGraphDefinition { Rig = Definition() };
		var parsed = JsonSerializer.Deserialize<AnimationGraphDefinition>( JsonSerializer.Serialize( graph ) );
		Assert.Equal( new[] { 2, 0, 1 }, new EffectorRigAsset( parsed.Rig, Clips() ).SolverToClip );
		Assert.Null( JsonSerializer.Deserialize<AnimationGraphDefinition>( "{}" ).Rig );
	}

	[Fact]
	public void DuplicateCanonicalBonesAreRejected()
	{
		var definition = Definition();
		definition.Bones[ 1 ].ClipBone = "TOE";
		Assert.Throws<ArgumentException>( () => new EffectorRigAsset( definition, Clips() ) );
	}

	[Fact]
	public void MissingHelperIsRejected()
	{
		var definition = Definition();
		definition.Effectors[ 3 ].HelperBone = "missing";
		Assert.Throws<ArgumentException>( () => new EffectorRigAsset( definition, Clips() ) );
	}

	[Fact]
	public void LinkedReferenceMustBelongToSolverOrder()
	{
		var definition = Definition();
		definition.Effectors[ 0 ].ReferenceBone = "helper";
		Assert.Throws<ArgumentException>( () => new EffectorRigAsset( definition, Clips() ) );
	}

	[Fact]
	public void NonfiniteBindComponentsAreRejected()
	{
		var definition = Definition();
		definition.Bones[ 1 ].BindPosition[ 0 ] = float.NaN;
		Assert.Throws<ArgumentException>( () => new EffectorRigAsset( definition, Clips() ) );
	}

	[Fact]
	public void UnsupportedVersionIsRejected()
	{
		var definition = Definition();
		definition.Version = 2;
		Assert.Throws<ArgumentException>( () => new EffectorRigAsset( definition, Clips() ) );
	}

	[Fact]
	public void InitializationRejectsPoseCountMismatch()
	{
		var asset = new EffectorRigAsset( Definition(), Clips() );
		Assert.Throws<ArgumentException>( () => asset.InitializeHierarchy( new LimbRigidTransform[ 3 ] ) );
	}

	private static EffectorRigDefinition Definition() => new()
	{
		ClipAxes = new[] { 3, 1, 2 },
		Bones = new[] { "toe", "base", "ankle" }.Select( name => new EffectorRigBoneDefinition
		{
			ClipBone = name, BindRotation = new[] { 0f, 0f, 0f, 2f },
			BindPosition = new[] { 1f, 2f, 3f }, BodySize = new[] { 4f, 5f, 6f }
		} ).ToArray(),
		Effectors = Enumerable.Range( 0, 4 ).Select( i => new EffectorRigSlotDefinition
		{
			HelperBone = "helper", EndBone = "toe", ReferenceBone = i < 2 ? "ankle" : ""
		} ).ToArray()
	};

	private static ClipSkeleton Clips() => new( new[] { "base", "ankle", "toe", "helper" },
		new[] { -1, 0, 1, 0 }, new Vector3[ 4 ], Enumerable.Repeat( Quaternion.Identity, 4 ).ToArray() );
}
