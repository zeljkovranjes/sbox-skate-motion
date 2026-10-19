using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class PhysicalPoseResetTests
{
	[Fact]
	public void BothTrailingTargetsUseTheSameAuthoredFrame()
	{
		var preparation = new PhysicalPosePreparationState
		{
			FirstFeedbackFrame = LimbRigidTransform.Identity with { Translation = new( 7, 8, 9 ) },
			SecondFeedbackFrame = LimbRigidTransform.Identity with { Translation = new( 2, 3, 4 ) }
		};
		var bodies = Bindings( 3 );
		var state = new PhysicalPoseResetState( 3 );
		PhysicalPoseReset.Update( state, preparation, new( 1 ), bodies, [ LimbRigidTransform.Identity ], LimbRigidTransform.Identity );
		Assert.Equal( preparation.SecondFeedbackFrame, bodies[ 1 ].CachedPose );
		Assert.Equal( bodies[ 1 ].CachedPose, bodies[ 2 ].CachedPose );
		Assert.Equal( new Vector3( 2, 3, 4 ), state.Positions[ 2 ] );
	}

	[Fact]
	public void ResetClearsMotionButRetainsAnimationCacheAndCorrectionState()
	{
		var bodies = Bindings( 3 );
		var cache = new EffectorPoseCache( 1 );
		var animated = LimbRigidTransform.Identity with { Translation = new( 2, 4, 6 ) };
		cache.Update( [ animated ], [ 1f ], LimbRigidTransform.Identity );
		cache.DeltaScale = 3f;
		var prep = new PhysicalPosePreparationState { SecondFeedbackFrame = LimbRigidTransform.Identity };
		var state = new PhysicalPoseResetState( 3 ) { MotionFlag = 7 };
		PhysicalPoseReset.Update( state, prep, cache, bodies, [ animated ], LimbRigidTransform.Identity );
		Assert.Equal( 0, state.MotionFlag );
		Assert.Equal( animated.Translation, state.WorldReference );
		Assert.Equal( state.WorldReference, state.PreviousWorldReference );
		Assert.Equal( Vector3.Zero, cache.WeightedPosition );
		Assert.Equal( Vector3.Zero, cache.WeightedPositionDelta );
		Assert.Equal( Vector3.Zero, cache.RootOffsetDelta );
		Assert.Equal( animated, cache.Bones[ 0 ] );
		Assert.Equal( 3f, cache.DeltaScale );
		foreach ( var binding in bodies )
		{
			Assert.Equal( Vector3.Zero, binding.Body.Velocity );
			Assert.Equal( Vector3.Zero, binding.Body.AngularVelocity );
			Assert.Equal( binding.Body.DefaultLinearAccumulator, binding.Body.LinearAccumulator );
			Assert.Equal( Vector3.Zero, binding.Body.AngularAccumulator );
			Assert.Equal( 123, binding.Body.AccumulatorState );
		}
	}

	[Fact]
	public void UnboundBodiesStillRefreshPhysicalPoseCache()
	{
		var bodies = new[] { new PhysicalBodyBinding(), new PhysicalBodyBinding(), new PhysicalBodyBinding() };
		var state = new PhysicalPoseResetState( 3 );
		var pose = new LimbRigidTransform( new( Vector3.UnitX * 2, Vector3.UnitY * 3, Vector3.UnitZ ), new( 3, 4, 5 ) );
		var prep = new PhysicalPosePreparationState { SecondFeedbackFrame = LimbRigidTransform.Identity };
		PhysicalPoseReset.Update( state, prep, new( 1 ), bodies, [ pose ], LimbRigidTransform.Identity );
		Assert.Equal( pose, state.Poses[ 0 ] );
		Assert.Equal( pose.Translation, state.Positions[ 0 ] );
		Assert.Equal( Vector3.Zero, state.Deltas[ 0 ] );
	}

	[Fact]
	public void CorrectionExcludesTrailingBodiesAndClearsOnlyCorrectedState()
	{
		var bodies = Bindings( 26 );
		PhysicalSkeletonCorrection.Apply( bodies, new( .01666666753590107f, 0, 0 ) );
		for ( var i = 0; i < bodies.Length; i++ )
		{
			Assert.Equal( i < 24 ? new Vector3( 1.5f, 2, 3 ) : new Vector3( 1, 2, 3 ), bodies[ i ].Body.LinearAccumulator );
			Assert.Equal( i < 24 ? 0 : 123, bodies[ i ].Body.AccumulatorState );
			Assert.Equal( Vector3.One, bodies[ i ].Body.AngularAccumulator );
		}
	}

	[Fact]
	public void CorrectionRejectsMissingBodiesBeforeChangingAnyAccumulator()
	{
		var bodies = Bindings( 26 );
		bodies[ 7 ].Body = null;
		Assert.Throws<ArgumentException>( () => PhysicalSkeletonCorrection.Apply( bodies, Vector3.One ) );
		Assert.Equal( new Vector3( 1, 2, 3 ), bodies[ 0 ].Body.LinearAccumulator );
		Assert.Equal( 123, bodies[ 0 ].Body.AccumulatorState );
	}

	private static PhysicalBodyBinding[] Bindings( int count )
	{
		var result = new PhysicalBodyBinding[ count ];
		for ( var i = 0; i < count; i++ )
			result[ i ] = new()
			{
				Body = new()
				{
					Pose = LimbRigidTransform.Identity, Velocity = Vector3.One, AngularVelocity = Vector3.One,
					LinearAccumulator = new( 1, 2, 3 ), AngularAccumulator = Vector3.One,
					DefaultLinearAccumulator = new( 4, 5, 6 ), AccumulatorState = 123, InverseMass = .5f
				}
			};
		return result;
	}
}
