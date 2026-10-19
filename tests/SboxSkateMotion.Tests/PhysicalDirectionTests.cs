using System;
using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class PhysicalDirectionTests
{
	[Fact]
	public void FilterRetainsItsErrorAndDeltaAcrossUpdates()
	{
		var state = new PhysicalDirectionFilterState
		{
			Coefficients = new( .25f, .125f, .5f, .5f ), Value = Vector3.UnitY, PreviousInput = Vector3.UnitY
		};
		Assert.Equal( new Vector3( .5625f, .4375f, 0f ), PhysicalDirectionFilter.Update( state, Vector3.UnitX ) );
		Assert.Equal( new Vector3( .5625f, -.5625f, 0f ), state.PreviousDelta );
		Assert.Equal( new Vector3( .57421875f, .42578125f, 0f ), PhysicalDirectionFilter.Update( state, Vector3.UnitX ) );
		Assert.Equal( new Vector3( .46875f, -.46875f, 0f ), state.SmoothedError );
		Assert.Equal( new Vector3( .25f, -.25f, 0f ), state.SmoothedDelta );
	}

	[Fact]
	public void FrameProductionRetainsLocalTranslationsAndComposesSourceBeforeInverse()
	{
		var state = State();
		state.WorldFrame = LimbRigidTransform.Identity with { Translation = new( 1f, 2f, 3f ) };
		state.SourceFrame = LimbRigidTransform.Identity with { Translation = new( 4f, 5f, 6f ) };
		state.FilteredFrame = LimbRigidTransform.Identity with { Translation = new( 9f, 8f, 7f ) };
		Create().Update( state, Input() );
		Assert.Equal( new Vector3( 1f, 2f, 3f ), state.LocalFrame.Translation );
		Assert.Equal( new Vector3( 5f, 7f, 9f ), state.WorldFrame.Translation );
		Assert.Equal( new Vector3( -5f, -7f, -9f ), state.InverseFrame.Translation );
		Assert.Equal( new Vector3( 9f, 8f, 7f ), state.FilteredFrame.Translation );
		Assert.Equal( LimbRigidTransform.Identity.Rotation, state.LocalFrame.Rotation );
	}

	[Fact]
	public void SmallTiltProjectionRetainsTheEntirePreviousTilt()
	{
		var state = State();
		state.Tilt = new( 2f, 3f, 4f );
		Create().Update( state, Input() );
		Assert.Equal( new Vector3( 2f, 3f, 4f ), state.Tilt );
	}

	[Fact]
	public void MirroringReversesCurveTiltWithoutChangingTheFrames()
	{
		var first = State();
		var second = State();
		var input = Input() with { Direction = Vector3.UnitX };
		Create().Update( first, input );
		Create().Update( second, input with { Mirrored = true } );
		Assert.Equal( .4f, MathF.Abs( first.Tilt.X ) );
		Assert.Equal( -first.Tilt.X, second.Tilt.X );
		Assert.Equal( first.WorldFrame, second.WorldFrame );
	}

	[Theory]
	[InlineData( -1f )]
	[InlineData( 1f )]
	public void InactiveControlClampsResponseAndWrapsTheRetainedHistory( float control )
	{
		var state = State();
		state.HistoryIndex = 29;
		state.AlternativeCurve = true;
		state.SteeringVelocity = 9f;
		state.SteeringTarget = 8f;
		state.SteeringMagnitude = 7f;
		state.SecondaryControl = 6f;
		Create().Update( state, Input() with { UseControl = true, Control = control } );
		Assert.Equal( control, state.ControlResponse );
		Assert.Equal( control, state.ControlHistory[ 29 ] );
		Assert.Equal( 0, state.HistoryIndex );
		Assert.Equal( control * .2f, state.SmoothedControl );
		Assert.False( state.AlternativeCurve );
		Assert.Equal( 0f, state.SteeringVelocity );
		Assert.Equal( 0f, state.SteeringTarget );
		Assert.Equal( 0f, state.SteeringMagnitude );
		Assert.Equal( 0f, state.SecondaryControl );
	}

	[Fact]
	public void DisabledInputStillDecaysControlAndAppendsAZeroResponse()
	{
		var state = State();
		state.Control = .75f;
		state.SmoothedControl = .25f;
		state.ControlHistory[ 0 ] = 1f;
		Create().Update( state, Input() with { Control = 1f } );
		Assert.Equal( 0f, state.Control );
		Assert.Equal( -1.125f, state.ControlDelta );
		Assert.Equal( 0f, state.ControlHistory[ 0 ] );
		Assert.Equal( .2f, state.SmoothedControl );
		Assert.Equal( 1, state.HistoryIndex );
	}

	[Fact]
	public void ZeroBlendedDirectionRetainsPublishedHeadingButUpdatesFilterMemory()
	{
		var state = State();
		state.FirstFilter.Coefficients = new( .25f, .125f, .5f, .5f );
		state.FirstFilter.PreviousInput = Vector3.UnitX;
		Create().Update( state, Input() with { Direction = -Vector3.UnitY, Blend = .5f } );
		Assert.Equal( Vector3.UnitY, state.Direction );
		Assert.Equal( Vector3.UnitY, state.PreviousDirection );
		Assert.Equal( Vector3.UnitY, state.PublishedDirection );
		Assert.Equal( Vector3.UnitY, state.FirstFilter.Value );
		Assert.Equal( new Vector3( -.5f, .5f, 0f ), state.FirstFilter.SmoothedDelta );
		Assert.Equal( -Vector3.UnitY, state.FilteredDirection );
	}

	private static PhysicalDirectionState State()
	{
		var state = new PhysicalDirectionState
		{
			Direction = Vector3.UnitY, WorldFrame = LimbRigidTransform.Identity,
			SourceFrame = LimbRigidTransform.Identity, FilteredFrame = LimbRigidTransform.Identity
		};
		foreach ( var filter in new[] { state.PrimaryFilter, state.FirstFilter, state.SecondFilter } )
		{
			filter.Value = Vector3.UnitY;
			filter.PreviousInput = Vector3.UnitY;
			filter.Coefficients = new( 1f, 0f, 0f, 1f );
		}
		return state;
	}

	private static PhysicalDirectionInput Input() => new( Vector3.UnitY, Vector3.UnitZ, 0f, false, false, 0f );

	private static PhysicalDirectionController Create()
	{
		var math = new LimbSolverMath( [ -1f / 720f, 1f / 24f, -.5f ],
			[ -1f / 5040f, 1f / 120f, -1f / 6f ], [ 0f, 0f, 0f, 0f, 0f, 0f, 0f, MathF.PI / 2f ],
			2f / MathF.PI, MathF.PI / 2f, 0f, MathF.PI );
		var points = new[] { 0f, .125f, .25f, .5f, .75f, 1f, 1.5f, 2f };
		return new( math, new( math, 1e-8f, MathF.PI * 2f, 1f / (MathF.PI * 2f) ),
			new( 0f, 0f, 0f, 1f, MathF.PI / 2f ),
			new( points, [ .5f, .5f, .5f, .5f, .5f, .5f, .5f, .5f ] ),
			new( points, [ .8f, .8f, .8f, .8f, .8f, .8f, .8f, .8f ] ), new( 1f, 0f, 0f, 1f ) );
	}
}
