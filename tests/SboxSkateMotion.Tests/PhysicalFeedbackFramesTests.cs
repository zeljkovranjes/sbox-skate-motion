using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class PhysicalFeedbackFramesTests
{
	[Fact]
	public void HeightApproachesRequestAcrossUpdatesWithoutOvershooting()
	{
		var state = new PhysicalPosePreparationState { ModelToWorld = LimbRigidTransform.Identity };
		PhysicalFeedbackFrames.Update( state, Vector3.Zero, .1f );
		Assert.Equal( .04f, state.FeedbackHeight );
		PhysicalFeedbackFrames.Update( state, Vector3.Zero, .1f );
		Assert.Equal( .08f, state.FeedbackHeight );
		PhysicalFeedbackFrames.Update( state, Vector3.Zero, .1f );
		Assert.Equal( .1f, state.FeedbackHeight );
		PhysicalFeedbackFrames.Update( state, Vector3.Zero, .1f );
		Assert.Equal( .1f, state.FeedbackHeight );
		PhysicalFeedbackFrames.Update( state, Vector3.Zero, -.1f );
		Assert.Equal( .1f - .04f, state.FeedbackHeight );
	}

	[Fact]
	public void TargetsUseSuppliedReferenceAndNormalizedLocalYAxis()
	{
		var state = new PhysicalPosePreparationState
		{
			ModelToWorld = new( new( Vector3.UnitY * 2f, -Vector3.UnitX * 3f, Vector3.UnitZ * 4f ), new( 100, 200, 300 ) )
		};
		var reference = new Vector3( 1, 2, 3 );
		PhysicalFeedbackFrames.Update( state, reference, 0f );
		Assert.Equal( reference, state.SecondFeedbackFrame.Translation );
		Assert.Equal( reference - Vector3.UnitX * .3f, state.FirstFeedbackFrame.Translation );
		Assert.Equal( state.SecondFeedbackFrame.Rotation, state.FirstFeedbackFrame.Rotation );
		Assert.Equal( -Vector3.UnitX, state.FirstFeedbackFrame.Rotation.Y );
	}

	[Fact]
	public void BuildingTargetsPreservesPreparedFeedbackAndMotionGate()
	{
		var state = new PhysicalPosePreparationState
		{
			ModelToWorld = LimbRigidTransform.Identity, SuppressionFlag = 7,
			FirstFeedbackPosition = new( 1, 2, 3 ), SecondFeedbackPosition = new( 4, 5, 6 )
		};
		PhysicalFeedbackFrames.Update( state, Vector3.Zero, 0f );
		Assert.Equal( 7, state.SuppressionFlag );
		Assert.Equal( new Vector3( 1, 2, 3 ), state.FirstFeedbackPosition );
		Assert.Equal( new Vector3( 4, 5, 6 ), state.SecondFeedbackPosition );
	}
}
