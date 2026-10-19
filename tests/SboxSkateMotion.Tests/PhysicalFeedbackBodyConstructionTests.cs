using System.Numerics;
using SkateMotion.Core.Animation;
using SkateMotion.Core.Physics;
using Xunit;

namespace SboxSkateMotion.Tests;

public class PhysicalFeedbackBodyConstructionTests
{
	[Theory]
	[InlineData( 24, .23f, .1f, 10f, 72.54412078857422f, 94.51795959472656f, .013784714974462986f )]
	[InlineData( 25, .36f, .26f, 100f, 206.6638946533203f, 385.8024597167969f, .00483877444639802f )]
	public void MatchesCapturedFeedbackDescriptorsAndInitializesRetainedBodies(
		int index, float radius, float halfLength, float inverseMass,
		float transverseInertia, float axialInertia, float maximumInertia )
	{
		var result = PhysicalFeedbackBodyConstruction.Create( index, .001f );
		Assert.Equal( radius, result.Radius );
		Assert.Equal( halfLength, result.HalfLength );
		Assert.Equal( inverseMass, result.Descriptor.Mass.InverseMass );
		Assert.Equal( new Vector3( transverseInertia, transverseInertia, axialInertia ), result.Descriptor.Mass.InverseInertia );
		Assert.Equal( maximumInertia, result.Descriptor.MaximumInertia );
		Assert.Equal( 29.999998092651367f, result.LinearDamping );
		Assert.Equal( 29.999998092651367f, result.AngularDamping );
		Assert.False( result.CollisionEnabled );
		Assert.Null( result.Descriptor.LocalOffset );
		Assert.True( result.Descriptor.HasShapeProperties );

		var body = new PhysicalBodyState { DefaultLinearAccumulator = new Vector3( 0f, -9.8f, 0f ) };
		PhysicalBodyInitialization.Initialize( body, LimbRigidTransform.Identity, result.Descriptor.Mass, 4, 0 );
		Assert.Equal( result.Descriptor.Mass.InverseInertia, body.InverseInertiaDiagonal );
		Assert.Equal( inverseMass, body.InverseMass );
		Assert.Equal( body.DefaultLinearAccumulator, body.LinearAccumulator );
	}

	[Fact]
	public void RejectsCanonicalAndOutOfRangeSlots()
	{
		foreach ( var index in new[] { -1, 0, 23, 26 } )
			Assert.Throws<ArgumentOutOfRangeException>( () => PhysicalFeedbackBodyConstruction.Create( index, .001f ) );
	}
}
