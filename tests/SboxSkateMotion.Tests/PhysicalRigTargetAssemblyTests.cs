using System;
using System.Numerics;
using SkateMotion.Core.Animation;
using SkateMotion.Core.Physics;
using Xunit;

namespace SboxSkateMotion.Tests;

public class PhysicalRigTargetAssemblyTests
{
	[Theory]
	[InlineData( 0f, 0f, 0f )]
	[InlineData( .1f, -9.8f, .3f )]
	public void TargetsMatchCapturedConstructionAndRemainNonresponding( float x, float y, float z )
	{
		var acceleration = new Vector3( x, y, z );
		var targets = new PhysicalRigTargetAssembly( .001f, acceleration, 9 );
		Assert.Equal( 4, targets.Bindings.Length );
		Assert.Null( targets.Descriptor.LocalOffset );
		Assert.Equal( .01f, targets.Descriptor.Mass.InverseMass );
		Assert.Equal( new Vector3( 1.4999998807907104f ), targets.Descriptor.Mass.InverseInertia );
		Assert.Equal( .6666667461395264f, targets.Descriptor.MaximumInertia );
		for ( var i = 0; i < targets.Bindings.Length; ++i )
		{
			var binding = targets.Bindings[ i ];
			var body = binding.Body;
			Assert.Null( binding.LocalOffset );
			Assert.Equal( LimbRigidTransform.Identity, binding.CachedPose );
			Assert.Equal( LimbRigidTransform.Identity, body.Pose );
			Assert.Equal( new Vector4( 0f, 0f, 0f, 1f ), body.Orientation );
			Assert.Equal( Vector3.Zero, body.Velocity );
			Assert.Equal( Vector3.Zero, body.AngularVelocity );
			Assert.Equal( 1, body.MotionMode );
			Assert.Null( body.Mass );
			Assert.Equal( 0f, body.InverseMass );
			Assert.Equal( Vector3.Zero, body.InverseInertiaDiagonal );
			Assert.Equal( Vector3.Zero, body.InverseInertiaOffDiagonal );
			Assert.Equal( acceleration, body.DefaultLinearAccumulator );
			Assert.Equal( Vector3.Zero, body.LinearAccumulator );
			Assert.Equal( Vector3.Zero, body.AngularAccumulator );
			Assert.Equal( 0f, body.MotionMetric );
			Assert.Equal( 9, body.AccumulatorState );
			for ( var j = 0; j < i; ++j )
				Assert.NotSame( targets.Bindings[ j ].Body, body );
		}
		var frame = new LimbRigidTransform( LimbRigidTransform.Identity.Rotation, new Vector3( 2f, 3f, 4f ) );
		targets.Bindings[ 0 ].Write( frame );
		Assert.Equal( frame, targets.Bindings[ 0 ].Read() );
		Assert.Equal( LimbRigidTransform.Identity, targets.Bindings[ 1 ].Read() );
		Assert.Equal( 0f, targets.Bindings[ 0 ].Body.InverseMass );
	}

	[Theory]
	[InlineData( 0f )]
	[InlineData( -1f )]
	[InlineData( float.NaN )]
	[InlineData( float.PositiveInfinity )]
	public void InvalidToleranceIsRejected( float tolerance ) =>
		Assert.Throws<ArgumentOutOfRangeException>( () => new PhysicalRigTargetAssembly( tolerance, Vector3.Zero, 0 ) );

	[Fact]
	public void InvalidDefaultAccelerationIsRejected() =>
		Assert.Throws<ArgumentException>( () => new PhysicalRigTargetAssembly( .001f, new Vector3( float.NaN ), 0 ) );
}
