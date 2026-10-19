using System;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class PhysicalStructuralRigAssetTests
{
	[Fact]
	public void OlderRigMetadataKeepsTheStructuralExtensionAbsent()
	{
		var definition = Definition();
		definition.PhysicalJoints = null;
		Assert.Null( new EffectorRigAsset( definition, Clips() ).Structural );
	}

	[Fact]
	public void LoadedMetadataBuildsJointsThroughTheCanonicalHierarchy()
	{
		var definition = JsonSerializer.Deserialize<EffectorRigDefinition>( JsonSerializer.Serialize( Definition() ) );
		var asset = new EffectorRigAsset( definition, Clips() );
		var joints = Build( asset );
		Assert.Equal( 22, joints.Length );
		Assert.Equal( 1, joints[ 0 ].ChildBone );
		Assert.Equal( 2, joints[ 0 ].ParentBone );
		Assert.Equal( 22, joints[ 21 ].ChildBone );
		Assert.Equal( 23, joints[ 21 ].ParentBone );
		Assert.Equal( .4f, joints[ 0 ].Limits.SwingAngle );
		Assert.Equal( .6f, joints[ 0 ].Limits.TwistAngle );
		Assert.Equal( -new Vector3( .1f, .2f, .3f ), joints[ 0 ].Frame.ChildPosition );
	}

	[Theory]
	[InlineData( "version" )]
	[InlineData( "count" )]
	[InlineData( "name" )]
	[InlineData( "range" )]
	[InlineData( "quaternion" )]
	[InlineData( "missing" )]
	public void InvalidStructuralMetadataFailsDuringAssetLoading( string fault )
	{
		var definition = Definition();
		switch ( fault )
		{
			case "version": definition.PhysicalJoints.Version = 2; break;
			case "count": definition.PhysicalJoints.Bones = new PhysicalStructuralBoneDefinition[ 23 ]; break;
			case "name": definition.PhysicalJoints.Bones[ 1 ].ClipBone = "b2"; break;
			case "range": definition.PhysicalJoints.Bones[ 1 ].SwingAngle = float.NaN; break;
			case "quaternion": definition.PhysicalJoints.Bones[ 1 ].JointRotation = new float[ 3 ]; break;
			case "missing": definition.PhysicalJoints.Bones[ 1 ] = null; break;
		}
		Assert.Throws<ArgumentException>( () => new EffectorRigAsset( definition, Clips() ) );
	}

	[Fact]
	public void SharedStructuralDataDoesNotRetainMutableDefinitionArrays()
	{
		var definition = Definition();
		var asset = new EffectorRigAsset( definition, Clips() );
		var expected = Build( asset );
		definition.PhysicalJoints.Bones[ 1 ].JointRotation[ 2 ] = .5f;
		definition.PhysicalJoints.Bones[ 1 ].SwingAngle = 2f;
		definition.Bones[ 1 ].BindPosition[ 0 ] = 5f;
		Assert.Equal( expected, Build( asset ) );
	}

	[Fact]
	public void StructuralBuildRejectsAnotherCanonicalMapping()
	{
		var asset = new EffectorRigAsset( Definition(), Clips() );
		var hierarchy = asset.InitializeHierarchy( Poses() );
		hierarchy.SolverToClip[ 1 ] = hierarchy.SolverToClip[ 2 ];
		Assert.Throws<ArgumentException>( () => asset.Structural.Build( hierarchy, Overrides(), Tuning(), MathProfile() ) );
	}

	private static PhysicalStructuralJoint[] Build( EffectorRigAsset asset ) =>
		asset.Structural.Build( asset.InitializeHierarchy( Poses() ), Overrides(), Tuning(), MathProfile() );

	private static LimbRigidTransform[] Poses() => Enumerable.Repeat( LimbRigidTransform.Identity, 25 ).ToArray();

	private static PhysicalStructuralJointOverride[] Overrides() =>
		Enumerable.Repeat( new PhysicalStructuralJointOverride( false, 1f, 1f ), 22 ).ToArray();

	private static PhysicalStructuralJointTuning Tuning() => new( new( 2f, 3f, 4f, 5f ), 12f, 18f, false, false );

	private static LimbSolverMath MathProfile() => new( [ -1f / 720f, 1f / 24f, -.5f ],
		[ -1f / 5040f, 1f / 120f, -1f / 6f ], new float[ 8 ], 2f / MathF.PI, MathF.PI / 2f, 0f, MathF.PI );

	private static EffectorRigDefinition Definition() => new()
	{
		ClipAxes = [ 3, 1, 2 ],
		Bones = Enumerable.Range( 0, 24 ).Select( i => new EffectorRigBoneDefinition
		{
			ClipBone = $"b{i}", BindRotation = [ 0f, 0f, 0f, 1f ],
			BindPosition = [ .1f, .2f, .3f ], BodySize = [ 1f, 2f, 3f ]
		} ).ToArray(),
		Effectors = Enumerable.Range( 0, 4 ).Select( _ => new EffectorRigSlotDefinition
		{
			HelperBone = "helper", EndBone = "b1", ReferenceBone = ""
		} ).ToArray(),
		PhysicalJoints = new()
		{
			Bones = Enumerable.Range( 0, 24 ).Select( i => new PhysicalStructuralBoneDefinition
			{
				ClipBone = $"b{i}", ParentToBoneRotation = [ 0f, 0f, 0f, 1f ],
				JointRotation = [ 0f, 0f, 0f, 1f ], SwingAngle = .4f, TwistAngle = .6f
			} ).ToArray()
		}
	};

	private static ClipSkeleton Clips()
	{
		var names = Enumerable.Range( 0, 24 ).Reverse().Select( i => $"b{i}" ).Append( "helper" ).ToArray();
		var parents = Enumerable.Range( 0, 25 ).Select( i => i is 0 or 23 ? -1 : i == 24 ? 0 : i - 1 ).ToArray();
		return new( names, parents, new Vector3[ 25 ], Enumerable.Repeat( Quaternion.Identity, 25 ).ToArray() );
	}
}
