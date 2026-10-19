using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class PhysicalRigPlacementTests
{
	[Fact]
	public void PlacementAnchorsWeightedPoseAtSuppliedReference()
	{
		var source = new LimbRigidTransform( new( Vector3.UnitY, -Vector3.UnitX, Vector3.UnitZ ), new( 100, 200, 300 ) );
		var weighted = new Vector3( 2, 3, 4 );
		var reference = new Vector3( 5, 6, 7 );
		var placement = PhysicalRigPlacement.Create( source, LimbRigidTransform.Identity, reference, weighted );
		Assert.Equal( reference, placement.ModelToWorld.Transform( weighted ) );
		Assert.Equal( weighted, placement.WorldToModel.Transform( reference ) );
	}

	[Fact]
	public void SourceTranslationsDoNotMoveTheReferenceAnchor()
	{
		var identity = LimbRigidTransform.Identity;
		var first = PhysicalRigPlacement.Create( identity, identity, Vector3.One, Vector3.UnitY );
		var second = PhysicalRigPlacement.Create( identity with { Translation = new( 100, 200, 300 ) },
			identity with { Translation = new( -200, 300, 400 ) }, Vector3.One, Vector3.UnitY );
		Assert.Equal( first, second );
		Assert.Equal( identity, second.RelativeFrame );
		Assert.Equal( identity, second.FirstHelperFrame );
		Assert.Equal( identity, second.SecondHelperFrame );
	}

	[Fact]
	public void PlacementPreservesSuppliedAxisScale()
	{
		var source = new LimbRigidTransform( new( Vector3.UnitX * 2f, Vector3.UnitY * 3f, Vector3.UnitZ * 4f ), Vector3.Zero );
		var placement = PhysicalRigPlacement.Create( source, LimbRigidTransform.Identity, Vector3.Zero, Vector3.One );
		Assert.Equal( source.Rotation, placement.ModelToWorld.Rotation );
		Assert.Equal( new Vector3( -2, -3, -4 ), placement.ModelToWorld.Translation );
	}
}
