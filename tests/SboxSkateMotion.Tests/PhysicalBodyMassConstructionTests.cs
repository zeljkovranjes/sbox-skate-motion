using System;
using System.Numerics;
using SkateMotion.Core.Animation;
using SkateMotion.Core.Physics;
using Xunit;

namespace SboxSkateMotion.Tests;

public class PhysicalBodyMassConstructionTests
{
	[Fact]
	public void SphereDensityDeterminesBothMassAndAngularResponse()
	{
		var shape = PhysicalPrimitiveMassProperties.Sphere( .5f );
		var result = Create( shape, 0f, 3f );
		Assert.Equal( new Vector3( .1f ), shape.UnitInertia );
		Assert.InRange( shape.Volume, .52359f, .52361f );
		Assert.Equal( 1f / (shape.Volume * 3f), result.Mass.InverseMass );
		Assert.InRange( result.Mass.InverseInertia.X, 6.3661f, 6.3663f );
		Assert.Null( result.LocalOffset );
		Assert.True( result.HasShapeProperties );
	}

	[Fact]
	public void ExplicitMassOverridesDensityAndRetainsShapeVolume()
	{
		var shape = PhysicalPrimitiveMassProperties.Sphere( .5f );
		var result = Create( shape, 2f, 100f );
		Assert.Equal( .5f, result.Mass.InverseMass );
		Assert.Equal( new Vector3( 5f ), result.Mass.InverseInertia );
		Assert.Equal( .2f, result.MaximumInertia );
		Assert.Equal( shape.Volume, result.Volume );
	}

	[Fact]
	public void MissingShapeUsesFallbackMassPropertiesWithoutItsSuppliedFrame()
	{
		var frame = LimbRigidTransform.Identity with { Translation = new( 8f, 9f, 10f ) };
		var result = PhysicalBodyMassConstruction.Create( null, frame, -1f, 3f, .001f );
		Assert.False( result.HasShapeProperties );
		Assert.Null( result.LocalOffset );
		Assert.Equal( 1f, result.Volume );
		Assert.Equal( new Vector3( 2f ), result.Mass.InverseInertia );
		Assert.Equal( 1f / 3f, result.Mass.InverseMass );
	}

	[Fact]
	public void CapsuleRadiusFloorDependsOnItsLength()
	{
		var thin = PhysicalPrimitiveMassProperties.Capsule( .001f, .8f );
		var floor = PhysicalPrimitiveMassProperties.Capsule( .8f * .05f, .8f );
		Assert.Equal( floor, thin );
		Assert.True( thin.UnitInertia.X > thin.UnitInertia.Z );
		Assert.Equal( thin.UnitInertia.X, thin.UnitInertia.Y );
		Assert.Equal( PhysicalPrimitiveMassProperties.Sphere( 1e-7f ), PhysicalPrimitiveMassProperties.Sphere( -1f ) );
	}

	[Fact]
	public void CylinderMarginExpandsBothRadiusAndHalfLength()
	{
		var expanded = PhysicalPrimitiveMassProperties.Cylinder( .2f, .4f, .1f );
		var direct = PhysicalPrimitiveMassProperties.Cylinder( .2f + .1f, .4f + .1f, 0f );
		Assert.Equal( direct, expanded );
		Assert.True( expanded.Volume > PhysicalPrimitiveMassProperties.Cylinder( .2f, .4f, 0f ).Volume );
	}

	[Fact]
	public void BoxMassPropertiesKeepAxisOrderAndMinimumExtents()
	{
		var box = PhysicalPrimitiveMassProperties.RoundedBox( new( 1f, 2f, 3f ), 0f );
		Assert.Equal( 48f, box.Volume );
		Assert.InRange( box.UnitInertia.X, 4.33333f, 4.33334f );
		Assert.InRange( box.UnitInertia.Y, 3.33333f, 3.33334f );
		Assert.InRange( box.UnitInertia.Z, 1.66666f, 1.66667f );
		var thin = PhysicalPrimitiveMassProperties.RoundedBox( new( .001f, .002f, .5f ), 0f );
		var floor = PhysicalPrimitiveMassProperties.RoundedBox( new( .025f, .025f, .5f ), 0f );
		Assert.Equal( floor, thin );
	}

	[Theory]
	[InlineData( .0009f, false )]
	[InlineData( .001f, false )]
	[InlineData( .0011f, true )]
	public void AxisToleranceUsesAStrictBoundary( float difference, bool expectedOffset )
	{
		var frame = LimbRigidTransform.Identity with
		{
			Rotation = new( new( 1f, difference, 0f ), Vector3.UnitY, Vector3.UnitZ )
		};
		var result = PhysicalBodyMassConstruction.Create( PhysicalPrimitiveMassProperties.Sphere( .5f ), frame, 2f, 1f, .001f );
		Assert.Equal( expectedOffset, result.LocalOffset.HasValue );
	}

	[Fact]
	public void ConstructedOffsetAndMassFeedTheExistingPhysicalBodyBinding()
	{
		var shape = PhysicalPrimitiveMassProperties.RoundedBox( new( 1f, 2f, 3f ), 0f );
		var massFrame = new LimbRigidTransform( new( Vector3.UnitY, -Vector3.UnitX, Vector3.UnitZ ), new( .2f, .3f, .4f ) );
		var result = PhysicalBodyMassConstruction.Create( shape, massFrame, 2f, 1f, .001f );
		var body = new PhysicalBodyState();
		PhysicalBodyInitialization.Initialize( body, LimbRigidTransform.Identity, result.Mass, 4, 0 );
		var binding = new PhysicalBodyBinding { Body = body, LocalOffset = result.LocalOffset };
		var desired = LimbRigidTransform.Identity with { Translation = new( 5f, 7f, 11f ) };
		binding.Write( desired );
		Assert.InRange( Vector3.Distance( body.Pose.Translation, desired.Translation + massFrame.Translation ), 0f, 1e-5f );
		Assert.InRange( Vector3.Distance( binding.Read().Translation, desired.Translation ), 0f, 1e-5f );
		var expectedInertia = new Vector3( result.Mass.InverseInertia.Y, result.Mass.InverseInertia.X, result.Mass.InverseInertia.Z );
		Assert.InRange( Vector3.Distance( body.InverseInertiaDiagonal, expectedInertia ), 0f, 1e-5f );
		Assert.Equal( result.Mass.InverseMass, body.InverseMass );
	}

	private static PhysicalBodyMassResult Create( PhysicalShapeMassProperties shape, float mass, float density ) =>
		PhysicalBodyMassConstruction.Create( shape, LimbRigidTransform.Identity, mass, density, .001f );
}
