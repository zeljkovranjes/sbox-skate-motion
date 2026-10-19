using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class PhysicalPoseReadbackTests
{
	[Fact]
	public void BoardOverridesSlotZeroAndMotionRetainsPreviousSample()
	{
		var state = new PhysicalPoseResetState( 26 );
		var bodies = Bindings();
		bodies[ 0 ] = null;
		var board = Pose( new( 2, 3, 4 ) );
		PhysicalPoseReadback.Update( state, bodies, board, new float[ 24 ] );
		var firstVelocity = state.Deltas[ 0 ];
		Assert.Equal( board, state.Poses[ 0 ] );
		Assert.Equal( board.Translation * 59.999996185302734f, firstVelocity );
		Assert.Equal( firstVelocity, state.VelocityChanges[ 0 ] );
		PhysicalPoseReadback.Update( state, bodies, board, new float[ 24 ] );
		Assert.Equal( Vector3.Zero, state.Deltas[ 0 ] );
		Assert.Equal( -firstVelocity, state.VelocityChanges[ 0 ] );
		Assert.Equal( .01666666753590107f, state.SampleInterval );
	}

	[Fact]
	public void WeightedMotionUsesCanonicalBodiesAndDoesNotNormalizeWeights()
	{
		var state = new PhysicalPoseResetState( 26 );
		var bodies = Bindings();
		bodies[ 3 ].Body = new() { Pose = Pose( new( 3, 2, 1 ) ) };
		bodies[ 3 ].LocalOffset = Pose( new( 2, 0, 0 ) );
		bodies[ 24 ].CachedPose = Pose( new( 1000, 1000, 1000 ) );
		bodies[ 25 ].CachedPose = bodies[ 24 ].CachedPose;
		var weights = new float[ 24 ];
		weights[ 3 ] = 2f;
		PhysicalPoseReadback.Update( state, bodies, LimbRigidTransform.Identity, weights );
		Assert.Equal( new Vector3( 5, 2, 1 ), state.Positions[ 3 ] );
		Assert.Equal( state.Positions[ 3 ] * 2f, state.WeightedPhysicalPosition );
		Assert.Equal( state.Deltas[ 3 ] * 2f, state.WeightedPhysicalVelocity );
		Assert.Equal( new Vector3( 1000 ), state.Positions[ 24 ] );
	}

	[Fact]
	public void ErrorsUseSolvedPoseAndWorldTargetsWithoutChangingMotion()
	{
		var state = new PhysicalPoseResetState( 26 );
		var targets = Enumerable.Repeat( LimbRigidTransform.Identity, 24 ).ToArray();
		for ( var i = 0; i < 26; i++ )
			state.Poses[ i ] = Pose( new( i, 0, 0 ) );
		state.AnimationErrors[ 0 ] = Vector3.One;
		state.Deltas[ 1 ] = new( 7, 8, 9 );
		var preparation = new PhysicalPosePreparationState
		{
			FirstFeedbackPosition = new( 20, 0, 0 ),
			SecondFeedbackPosition = new( 30, 0, 0 )
		};
		PhysicalPoseReadback.UpdateErrors( state, preparation, Pose( new( 10, 0, 0 ) ), targets );
		Assert.Equal( Vector3.Zero, state.AnimationErrors[ 0 ] );
		Assert.Equal( new Vector3( -9, 0, 0 ), state.AnimationErrors[ 1 ] );
		Assert.Equal( new Vector3( 13, 0, 0 ), state.AnimationErrors[ 23 ] );
		Assert.Equal( new Vector3( 4, 0, 0 ), state.FirstFeedbackError );
		Assert.Equal( new Vector3( -5, 0, 0 ), state.SecondFeedbackError );
		Assert.Equal( new Vector3( 7, 8, 9 ), state.Deltas[ 1 ] );
	}

	[Fact]
	public void MissingBindingFailsBeforePublishingPartialReadback()
	{
		var state = new PhysicalPoseResetState( 26 );
		var bodies = Bindings();
		bodies[ 25 ] = null;
		Assert.Throws<ArgumentException>( () => PhysicalPoseReadback.Update( state, bodies, Pose( Vector3.One ), new float[ 24 ] ) );
		Assert.Equal( Vector3.Zero, state.Positions[ 0 ] );
		Assert.Equal( 0f, state.SampleInterval );
	}

	[Fact]
	public void WrongShapeFailsBeforeReplacingExistingErrors()
	{
		var state = new PhysicalPoseResetState( 26 );
		state.AnimationErrors[ 0 ] = Vector3.One;
		Assert.Throws<ArgumentException>( () => PhysicalPoseReadback.UpdateErrors( state,
			new(), LimbRigidTransform.Identity, new LimbRigidTransform[ 23 ] ) );
		Assert.Equal( Vector3.One, state.AnimationErrors[ 0 ] );
	}

	private static LimbRigidTransform Pose( Vector3 translation ) => LimbRigidTransform.Identity with { Translation = translation };

	private static PhysicalBodyBinding[] Bindings() => Enumerable.Range( 0, 26 )
		.Select( i => new PhysicalBodyBinding { CachedPose = LimbRigidTransform.Identity } ).ToArray();
}
