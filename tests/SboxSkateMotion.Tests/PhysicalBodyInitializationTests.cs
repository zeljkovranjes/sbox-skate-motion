using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class PhysicalBodyInitializationTests
{
	[Fact]
	public void TargetInitializationClearsMassAndMotionFromReusedBody()
	{
		var body = ReusedBody();
		var frame = LimbRigidTransform.Identity with { Translation = new( 1, 2, 3 ) };
		PhysicalBodyInitialization.Initialize( body, frame, new( new( 2, 3, 4 ), .25f ), 1, 9 );
		Assert.Null( body.Mass );
		Assert.Equal( frame.Translation, body.Pose.Translation );
		Assert.Equal( Vector3.Zero, body.InverseInertiaDiagonal );
		Assert.Equal( Vector3.Zero, body.InverseInertiaOffDiagonal );
		Assert.Equal( 0f, body.InverseMass );
		Assert.Equal( Vector3.Zero, body.LinearAccumulator );
		Assert.Equal( Vector3.Zero, body.AngularAccumulator );
		Assert.Equal( Vector3.Zero, body.Velocity );
		Assert.Equal( Vector3.Zero, body.AngularVelocity );
		Assert.Equal( 9, body.AccumulatorState );
		Assert.Equal( 1, body.MotionMode );
		Assert.Equal( 0f, body.MotionMetric );
		Assert.Equal( new Vector3( 0, -9, 0 ), body.DefaultLinearAccumulator );
	}

	[Theory]
	[InlineData( 2, 2, 9 )]
	[InlineData( 4, 4, 0 )]
	[InlineData( 7, 4, 0 )]
	public void MassBodyInitializationUsesDefaultsAndModeSpecificState( int mode, int expectedMode, int expectedState )
	{
		var body = ReusedBody();
		var mass = new PhysicalBodyMass( new( 2, 3, 4 ), .25f );
		PhysicalBodyInitialization.Initialize( body, LimbRigidTransform.Identity, mass, mode, 9 );
		Assert.Equal( mass, body.Mass );
		Assert.Equal( mass.InverseInertia, body.InverseInertiaDiagonal );
		Assert.Equal( Vector3.Zero, body.InverseInertiaOffDiagonal );
		Assert.Equal( mass.InverseMass, body.InverseMass );
		Assert.Equal( body.DefaultLinearAccumulator, body.LinearAccumulator );
		Assert.Equal( Vector3.Zero, body.AngularAccumulator );
		Assert.Equal( Vector3.Zero, body.Velocity );
		Assert.Equal( Vector3.Zero, body.AngularVelocity );
		Assert.Equal( expectedMode, body.MotionMode );
		Assert.Equal( expectedState, body.AccumulatorState );
		Assert.Equal( float.MaxValue, body.MotionMetric );
	}

	private static PhysicalBodyState ReusedBody() => new()
	{
		Mass = new( Vector3.One, 1f ),
		InverseInertiaDiagonal = Vector3.One,
		InverseInertiaOffDiagonal = Vector3.One,
		InverseMass = 1f,
		Velocity = new( 4, 5, 6 ),
		AngularVelocity = new( 7, 8, 9 ),
		LinearAccumulator = Vector3.One,
		AngularAccumulator = Vector3.One,
		DefaultLinearAccumulator = new( 0, -9, 0 ),
		AccumulatorState = 99,
		MotionMetric = -1f,
		MotionMode = 99
	};
}
