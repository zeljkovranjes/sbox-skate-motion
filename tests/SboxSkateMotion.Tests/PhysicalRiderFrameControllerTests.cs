using System;
using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class PhysicalRiderFrameControllerTests
{
	[Fact]
	public void AnchorOverrideLastsOneUpdateThenPredictionUsesOffsetDeckPosition()
	{
		var controller = Create();
		var state = new PhysicalRiderFrameState
		{
			BoardFrame = LimbRigidTransform.Identity with { Translation = new( -1, -2, -3 ) },
			Anchor = new( 9, 8, 7 ), PreserveAnchor = true
		};
		var deck = new PhysicalBodyBinding
		{
			Body = new PhysicalBodyState
			{
				Pose = LimbRigidTransform.Identity with { Translation = new( 2, 3, 4 ) },
				Velocity = new( 4, 0, 0 )
			},
			LocalOffset = LimbRigidTransform.Identity with { Translation = Vector3.UnitX }
		};
		controller.Update( state, deck, LimbRigidTransform.Identity, .25f );
		Assert.Equal( new Vector3( 9, 8, 7 ), state.Anchor );
		Assert.Equal( new Vector3( -1, -2, -3 ), state.PreviousBoardPosition );
		Assert.False( state.PreserveAnchor );
		controller.Update( state, deck, LimbRigidTransform.Identity, .25f );
		Assert.Equal( new Vector3( 4, 3, 4 ), state.Anchor );
		Assert.Equal( new Vector3( 3, 3, 4 ), state.PreviousBoardPosition );
		Assert.Equal( new Vector3( -3, -3, -4 ), state.WorldToBoard.Translation );
	}

	[Fact]
	public void PlacementPreservesSourceOffsetsAndNormalizesOnlyTheWorldRotation()
	{
		var state = new PhysicalRiderFrameState
		{
			RootFrame = LimbRigidTransform.Identity with { Translation = new( 1, 2, 3 ) },
			LocalFrame = LimbRigidTransform.Identity with { Translation = new( 4, 5, 6 ) },
			Anchor = new( 10, 11, 12 ), PreserveAnchor = true
		};
		var source = new LimbRigidTransform( new( Vector3.UnitX * 2, Vector3.UnitY * 3, Vector3.UnitZ * 4 ), new( 7, 8, 9 ) );
		Create().Update( state, new PhysicalBodyBinding { CachedPose = LimbRigidTransform.Identity }, source, .1f );
		Assert.Equal( new Vector3( 13, 17, 21 ), state.RelativeFrame.Translation );
		Assert.Equal( new Vector3( 23, 28, 33 ), state.ModelToWorld.Translation );
		Assert.Equal( source.Rotation, state.RelativeFrame.Rotation );
		Assert.Equal( LimbRigidTransform.Identity.Rotation, state.ModelToWorld.Rotation );
		Assert.Equal( Vector3.Zero, state.WorldToModel.Transform( state.ModelToWorld.Translation ) );
	}

	[Fact]
	public void HeadingResetHandlesVerticalForwardAndDoesNotRepeatWithoutRequest()
	{
		var state = new PhysicalRiderFrameState
		{
			RootFrame = new( new( Vector3.UnitX, Vector3.UnitZ, -Vector3.UnitY ), Vector3.Zero ),
			LocalFrame = LimbRigidTransform.Identity with { Translation = Vector3.One },
			ResetHeading = true, PreserveAnchor = true
		};
		var deck = new PhysicalBodyBinding { Body = new PhysicalBodyState { Pose = LimbRigidTransform.Identity } };
		var controller = Create();
		controller.Update( state, deck, LimbRigidTransform.Identity, .1f );
		Assert.Equal( LimbRigidTransform.Identity, state.LocalFrame );
		Assert.False( state.ResetHeading );
		state.RootFrame = new( new( -Vector3.UnitZ, Vector3.UnitY, Vector3.UnitX ), Vector3.Zero );
		controller.Update( state, deck, LimbRigidTransform.Identity, .1f );
		Assert.Equal( LimbRigidTransform.Identity, state.LocalFrame );
	}

	[Fact]
	public void CachedDeckFrameWorksWithExplicitAnchorButPredictionRequiresABody()
	{
		var state = new PhysicalRiderFrameState { PreserveAnchor = true };
		var deck = new PhysicalBodyBinding { CachedPose = LimbRigidTransform.Identity with { Translation = Vector3.One } };
		var controller = Create();
		controller.Update( state, deck, LimbRigidTransform.Identity, .1f );
		Assert.Equal( Vector3.One, state.BoardFrame.Translation );
		Assert.Throws<InvalidOperationException>( () => controller.Update( state, deck, LimbRigidTransform.Identity, .1f ) );
	}

	private static PhysicalRiderFrameController Create()
	{
		var math = new LimbSolverMath( [ -1f / 720f, 1f / 24f, -.5f ],
			[ -1f / 5040f, 1f / 120f, -1f / 6f ], [ 0, 0, 0, 0, 0, 0, 0, MathF.PI / 2f ],
			2f / MathF.PI, MathF.PI / 2f, 0f, MathF.PI );
		return new( math, new LimbSignedAngle( math, 1e-8f, MathF.PI * 2f, 1f / (MathF.PI * 2f) ) );
	}
}
