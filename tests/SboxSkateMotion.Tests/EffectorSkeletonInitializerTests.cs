using System;
using System.Linq;
using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class EffectorSkeletonInitializerTests
{
	[Fact]
	public void SolverOrderCanDifferFromClipOrder()
	{
		var result = Create( new[] { -1, 0, 1 }, new[] { 2, 0, 1 } );
		Assert.Equal( new[] { 2, -1, 1 }, result.Parents );
		Assert.Equal( new[] { 1, 2, 0 }, result.ClipToSolver );
		Assert.Equal( new[] { 2f, 0f, 1f }, result.InitialBones.Select( b => b.Translation.X ) );
	}

	[Fact]
	public void ParentWalkSkipsUnmappedIntermediateBones()
	{
		var result = Create( new[] { -1, 0, 1, 2, 3 }, new[] { 0, 4 } );
		Assert.Equal( new[] { -1, 0 }, result.Parents );
		Assert.Equal( new[] { 0, -1, -1, -1, 1 }, result.ClipToSolver );
	}

	[Fact]
	public void UnmappedAncestorsCanLeadToAnIndependentRoot()
	{
		var result = Create( new[] { -1, 0, 1 }, new[] { 2 } );
		Assert.Equal( new[] { -1 }, result.Parents );
		Assert.Empty( result.EdgeChildren );
		Assert.Empty( result.Children[ 0 ] );
	}

	[Fact]
	public void ChildrenAndEdgesFollowSolverOrder()
	{
		var result = Create( new[] { -1, 0, 0, 1 }, new[] { 3, 2, 0, 1 } );
		Assert.Equal( new[] { 3, 2, -1, 2 }, result.Parents );
		Assert.Equal( new[] { 1, 3 }, result.Children[ 2 ] );
		Assert.Equal( new[] { 0 }, result.Children[ 3 ] );
		Assert.Equal( new[] { 0, 1, 3 }, result.EdgeChildren );
		Assert.Equal( new[] { 3, 2, 2 }, result.EdgeParents );
		Assert.Equal( new[] { 0, 1, 0, 2 }, result.EdgeIndices );
	}

	[Fact]
	public void IdentityBindProducesInverseTranslation()
	{
		var result = Create( new[] { -1 }, new[] { 0 }, position: new Vector3( 2f, -3f, 4f ) );
		Assert.Equal( new Vector3( -2f, 3f, -4f ), result.InverseBindTransforms[ 0 ].Translation );
		Assert.Equal( LimbRigidTransform.Identity.Rotation, result.BindTransforms[ 0 ].Rotation );
	}

	[Fact]
	public void NonUnitQuaternionIsPreservedAndUsesGeneralInverse()
	{
		var result = Create( new[] { -1 }, new[] { 0 }, new Quaternion( 0f, 0f, 1f, 1f ), new Vector3( 2f, 3f, 4f ) );
		var bind = result.BindTransforms[ 0 ];
		Assert.True( bind.Rotation.X.Length() > 2f );
		var point = new Vector3( 1f, 2f, 3f );
		Assert.True( Vector3.Distance( point, result.InverseBindTransforms[ 0 ].Transform( bind.Transform( point ) ) ) < 0.00001f );
	}

	[Fact]
	public void InitializationOwnsItsMappingAndGatheredPoseArrays()
	{
		var mapping = new[] { 1, 0 };
		var result = Create( new[] { -1, 0 }, mapping );
		mapping[ 0 ] = 0;
		Assert.Equal( new[] { 1, 0 }, result.SolverToClip );
		Assert.Equal( 1f, result.InitialBones[ 0 ].Translation.X );
	}

	[Theory]
	[InlineData( -1, 1 )]
	[InlineData( 2, 1 )]
	[InlineData( 1, 1 )]
	public void MissingOutOfRangeOrDuplicateMappingsAreRejected( int first, int second )
	{
		Assert.Throws<ArgumentException>( () => Create( new[] { -1, 0 }, new[] { first, second } ) );
	}

	[Theory]
	[InlineData( 1, 0 )]
	[InlineData( -2, 0 )]
	[InlineData( -1, 2 )]
	public void CyclicAndInvalidClipParentsAreRejected( int first, int second )
	{
		Assert.Throws<ArgumentException>( () => Create( new[] { first, second }, new[] { 0 } ) );
	}

	private static EffectorSkeletonHierarchy Create( int[] parents, int[] mapping,
		Quaternion? rotation = null, Vector3 position = default )
	{
		var poses = Enumerable.Range( 0, parents.Length ).Select( i =>
			LimbRigidTransform.Identity with { Translation = new Vector3( i, 0f, 0f ) } ).ToArray();
		return EffectorSkeletonInitializer.Create( parents, mapping,
			Enumerable.Repeat( rotation ?? Quaternion.Identity, mapping.Length ).ToArray(),
			Enumerable.Repeat( position, mapping.Length ).ToArray(), poses );
	}
}
