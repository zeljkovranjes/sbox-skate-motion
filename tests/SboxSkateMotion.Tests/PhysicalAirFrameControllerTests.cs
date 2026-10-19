using System;
using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class PhysicalAirFrameControllerTests
{
	[Fact]
	public void PlacementSubtractsOffsetBeforeNormalizingAndIgnoresFrameTranslations()
	{
		var state = new PhysicalRiderFrameState
		{
			LocalFrame = LimbRigidTransform.Identity with { Translation = new( 11, 12, 13 ) },
			Anchor = new( 7, 8, 9 ), PreviousBoardPosition = new( 4, 5, 6 ), PreserveAnchor = true
		};
		var source = new LimbRigidTransform(
			new( Vector3.UnitX * 2, Vector3.UnitY * 3, Vector3.UnitZ * 4 ), new( 100, 200, 300 ) );
		Create().Update( state, new( source, new( 10, 20, 30 ), new( 1, 2, 3 ), false, false, 0 ) );
		Assert.Equal( new Vector3( 8, 14, 18 ), state.ModelToWorld.Translation );
		Assert.Equal( LimbRigidTransform.Identity.Rotation, state.ModelToWorld.Rotation );
		Assert.Equal( new Vector3( -8, -14, -18 ), state.WorldToModel.Translation );
		Assert.Equal( new Vector3( 11, 12, 13 ), state.LocalFrame.Translation );
		Assert.Equal( new Vector3( 7, 8, 9 ), state.Anchor );
		Assert.Equal( new Vector3( 4, 5, 6 ), state.PreviousBoardPosition );
		Assert.True( state.PreserveAnchor );
		Assert.Equal( LimbRigidTransform.Identity, state.BoardFrame );
		Assert.Equal( LimbRigidTransform.Identity, state.WorldToBoard );
		Assert.Equal( LimbRigidTransform.Identity, state.RootFrame );
		Assert.Equal( LimbRigidTransform.Identity, state.RelativeFrame );
	}

	[Fact]
	public void ResetPreservesWorldOrientationThenRetainsItsRelationToTheSource()
	{
		var world = new LimbRigidTransform( new( Vector3.UnitZ, Vector3.UnitY, -Vector3.UnitX ), new( 3, 4, 5 ) );
		var source = new LimbRigidTransform( new( Vector3.UnitY, -Vector3.UnitX, Vector3.UnitZ ), new( 9, 8, 7 ) );
		var state = new PhysicalRiderFrameState { ModelToWorld = world, ResetHeading = true };
		var controller = Create();
		controller.Update( state, new( source, new( 1, 2, 3 ), Vector3.Zero, false, false, 0 ) );
		Assert.Equal( world.Rotation, state.ModelToWorld.Rotation );
		Assert.Equal( new Vector3( 1, 2, 3 ), state.ModelToWorld.Translation );
		Assert.Equal( Vector3.Zero, state.LocalFrame.Translation );
		Assert.False( state.ResetHeading );
		var retained = state.LocalFrame;
		controller.Update( state, new( LimbRigidTransform.Identity, Vector3.Zero, Vector3.Zero, false, false, 0 ) );
		Assert.Equal( retained, state.LocalFrame );
		Assert.Equal( retained.Rotation, state.ModelToWorld.Rotation );
	}

	[Fact]
	public void RotationAccumulatesInLocalSpaceAndStopsWhenRequestClears()
	{
		var state = new PhysicalRiderFrameState
		{
			LocalFrame = LimbRigidTransform.Identity with { Translation = new( 2, 3, 4 ) }
		};
		var controller = Create();
		var input = new PhysicalAirFrameInput( LimbRigidTransform.Identity, Vector3.Zero, Vector3.Zero, true, true, 2 );
		controller.Update( state, input );
		Assert.True( Vector3.Distance( -Vector3.UnitZ, state.LocalFrame.Rotation.X ) < 1e-6f );
		controller.Update( state, input );
		Assert.True( Vector3.Distance( -Vector3.UnitX, state.LocalFrame.Rotation.X ) < 1e-6f );
		Assert.Equal( new Vector3( 2, 3, 4 ), state.LocalFrame.Translation );
		var retained = state.LocalFrame;
		controller.Update( state, input with { AdvanceRotation = false, RotationSteps = 0 } );
		Assert.Equal( retained, state.LocalFrame );
	}

	[Fact]
	public void RotationDirectionAndUnsignedStepCountRemainDistinct()
	{
		var positive = new PhysicalRiderFrameState();
		var negative = new PhysicalRiderFrameState();
		var controller = Create();
		var input = new PhysicalAirFrameInput( LimbRigidTransform.Identity, Vector3.Zero, Vector3.Zero, true, true, uint.MaxValue );
		controller.Update( positive, input );
		controller.Update( negative, input with { PositiveRotation = false } );
		Assert.InRange( positive.LocalFrame.Rotation.X.Z, -1e-8f, -1e-12f );
		Assert.InRange( negative.LocalFrame.Rotation.X.Z, 1e-12f, 1e-8f );
	}

	private static PhysicalAirFrameController Create()
	{
		return new( new LimbSolverMath( [ -1f / 720f, 1f / 24f, -.5f ],
			[ -1f / 5040f, 1f / 120f, -1f / 6f ], [ 0, 0, 0, 0, 0, 0, 0, MathF.PI / 2f ],
			2f / MathF.PI, MathF.PI / 2f, 0f, MathF.PI ) );
	}
}
