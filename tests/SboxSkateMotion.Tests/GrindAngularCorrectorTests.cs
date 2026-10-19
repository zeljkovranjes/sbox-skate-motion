using System;
using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class GrindAngularCorrectorTests
{
	[Fact]
	public void SignedAngleRejectsVectorsAtTheSquaredLengthThreshold()
	{
		var angles = new LimbSignedAngle( MathProfile(), .25f, 2f * MathF.PI, 1f / (2f * MathF.PI) );
		Assert.Equal( 0f, angles.Evaluate( Vector3.UnitX * .5f, Vector3.UnitY, Vector3.UnitZ ) );
		Assert.Equal( 0f, angles.Evaluate( Vector3.UnitX, Vector3.Zero, Vector3.UnitZ ) );
	}

	[Fact]
	public void SignedAngleUsesTheNormalToChooseOrientation()
	{
		var angles = Angles();
		Assert.Equal( MathF.PI / 2f, angles.Evaluate( Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ ) );
		Assert.Equal( 2f * MathF.PI - MathF.PI / 2f, angles.Evaluate( Vector3.UnitX, Vector3.UnitY, -Vector3.UnitZ ) );
	}

	[Fact]
	public void ScalarAndFractionalWrapKeepDifferentHalfTurnEndpoints()
	{
		var angles = Angles();
		Assert.Equal( -MathF.PI, angles.Wrapped( Vector3.UnitX, -Vector3.UnitX, Vector3.UnitZ ) );
		Assert.Equal( MathF.PI, angles.WrappedFraction( Vector3.UnitX, -Vector3.UnitX, Vector3.UnitZ ) );
	}

	[Fact]
	public void NoSelectionPreservesAngularHistoryAndDiagnostics()
	{
		var state = State();
		state.HasCandidate = false;
		state.AngularVelocity = new Vector3( 1f, 2f, 3f );
		state.AngularPosition = new Vector3( 4f, 5f, 6f );
		state.PitchAngle = 7f;
		state.YawError = 8f;
		Create().Update( LimbRigidTransform.Identity.Rotation, Metadata(), state );
		Assert.Equal( new Vector3( 1f, 2f, 3f ), state.AngularVelocity );
		Assert.Equal( new Vector3( 4f, 5f, 6f ), state.AngularPosition );
		Assert.Equal( 7f, state.PitchAngle );
		Assert.Equal( 8f, state.YawError );
	}

	[Fact]
	public void YawCapsResponseBeforeAddingAndCappingThePriorRate()
	{
		var state = State( 5 );
		state.SelectedAngle = -10f;
		state.AngularVelocity = new Vector3( 0f, -.25f, 0f );
		Create().Update( LimbRigidTransform.Identity.Rotation, Metadata(), state );
		Assert.Equal( 8f, state.YawError );
		Assert.Equal( .25f, state.AngularVelocity.Y );
	}

	[Theory]
	[InlineData( 0f, -2f )]
	[InlineData( -1f, -1f )]
	[InlineData( 1f, 1f )]
	public void TargetSignTreatsZeroAsNonpositive( float current, float expectedError )
	{
		var state = State();
		state.SelectedAngle = current;
		Create().Update( LimbRigidTransform.Identity.Rotation, Metadata(), state );
		Assert.Equal( expectedError, state.YawError );
	}

	[Fact]
	public void RollReplacesOldRateButAddsToAngularPosition()
	{
		var state = State();
		state.AngularVelocity = new Vector3( 0f, 0f, 9f );
		state.AngularPosition = new Vector3( 0f, 0f, 4f );
		Create().Update( LimbRigidTransform.Identity.Rotation, Metadata(), state );
		Assert.Equal( 0f, state.AngularVelocity.Z );
		Assert.Equal( 4f, state.AngularPosition.Z );
	}

	[Theory]
	[InlineData( 4 )]
	[InlineData( 5 )]
	[InlineData( 6 )]
	public void AxleCandidatesClearRollAndPreservePitchRateWithinTheLimit( int candidate )
	{
		var state = State( candidate );
		state.AngularVelocity = new Vector3( .2f, 0f, 1f );
		Create().Update( LimbRigidTransform.Identity.Rotation, Metadata(), state );
		Assert.Equal( .2f, state.AngularVelocity.X );
		Assert.Equal( 0f, state.AngularVelocity.Z );
		Assert.Equal( 0f, state.PitchAngle );
	}

	[Fact]
	public void PitchUsesPredictedTipDirectionAndCapsTheResultingRate()
	{
		var state = State();
		Create().Update( LimbRigidTransform.Identity.Rotation, Metadata(), state );
		// Period subtraction rounds below negative half-pi, so the final fold adds pi.
		Assert.Equal( .5f, state.AngularVelocity.X );
		Assert.InRange( state.PitchAngle, 1.570f, 1.571f );
	}

	[Fact]
	public void CandidateOneReversesTipDirectionBeforeChoosingMetadataAxis()
	{
		var one = State( 1 );
		var two = State( 2 );
		var metadata = new LimbFrame( Vector3.UnitY, -Vector3.UnitZ, Vector3.UnitY );
		Create().Update( LimbRigidTransform.Identity.Rotation, metadata, one );
		Create().Update( LimbRigidTransform.Identity.Rotation, metadata, two );
		Assert.Equal( 0f, one.PitchAngle );
		Assert.InRange( two.PitchAngle, -1.571f, -1.570f );
	}

	[Fact]
	public void CorrectorOwnsItsCandidateParameters()
	{
		var candidates = Candidates();
		var corrector = Create( candidates );
		Array.Fill( candidates, new GrindApproachCandidate( 100f, 0f, 0f, 1f, 0 ) );
		var state = State();
		corrector.Update( LimbRigidTransform.Identity.Rotation, Metadata(), state );
		Assert.Equal( -2f, state.YawError );
	}

	[Fact]
	public void InvalidUnitsAndAnglePeriodsAreRejected()
	{
		Assert.Throws<ArgumentException>( () => new GrindAngularCorrector( MathProfile(), Settings() with { RadiansPerDegree = 0f }, Candidates() ) );
		Assert.Throws<ArgumentException>( () => new GrindAngularCorrector( MathProfile(), Settings() with { Period = float.NaN }, Candidates() ) );
		Assert.Throws<ArgumentException>( () => Create( new GrindApproachCandidate[ 6 ] ) );
	}

	private static GrindApproachState State( int candidate = 0 )
	{
		var state = new GrindApproachState { HasCandidate = true, SelectedCandidate = candidate,
			SelectedPoint = 0, EdgeFrame = new LimbFrame( -Vector3.UnitZ, Vector3.UnitY, Vector3.UnitX ) };
		state.Crossings[ 0 ] = 1f;
		state.Points[ 6 ] = Vector3.UnitZ;
		state.Points[ 9 ] = -Vector3.UnitZ;
		return state;
	}

	private static LimbFrame Metadata() => new( Vector3.UnitY, Vector3.UnitY, Vector3.UnitZ );

	private static GrindApproachCandidate[] Candidates()
	{
		var result = new GrindApproachCandidate[ 7 ];
		Array.Fill( result, new GrindApproachCandidate( 2f, 0f, 0f, 1f, 0 ) );
		return result;
	}

	private static GrindAngularSettings Settings() => new( 1f, 1f, .5f, .9f, 1e-6f, 1e-4f, 2f * MathF.PI, 1f / (2f * MathF.PI) );

	private static GrindAngularCorrector Create( GrindApproachCandidate[] candidates = null ) => new( MathProfile(), Settings(), candidates ?? Candidates() );

	private static LimbSignedAngle Angles() => new( MathProfile(), 1e-4f, 2f * MathF.PI, 1f / (2f * MathF.PI) );

	private static LimbSolverMath MathProfile() => new( new[] { -1f / 720f, 1f / 24f, -.5f },
		new[] { -1f / 5040f, 1f / 120f, -1f / 6f }, new[] { 0f, 0f, 0f, 0f, 0f, 0f, 0f, MathF.PI / 2f },
		2f / MathF.PI, MathF.PI / 2f, 0f, MathF.PI );
}
