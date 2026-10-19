using System;
using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class EffectorTargetSolverTests
{
	private static readonly LimbRigidTransform Identity = LimbRigidTransform.Identity;
	private static readonly EffectorTargetSettings Settings = new( .0001f, .5f, .125f, .01f, .25f, -.6f, .6f );
	private static readonly EffectorTargetState Pinned = new( 2, Vector3.Zero, Vector3.Zero, Vector3.Zero, Vector3.UnitY, 1f, 0f, false );

	[Fact]
	public void PinUpdatesReleaseInModelSpaceAndPreservesOffset()
	{
		var world = Identity with { Translation = new Vector3( 3f, 2f, 1f ) };
		var inverse = Identity with { Translation = -world.Translation };
		var animated = Identity with { Translation = Vector3.UnitX };
		var state = Pinned with { Pin = new Vector3( 4.25f, 2f, 1f ), Offset = Vector3.One };
		var result = CreateSolver().Solve( state, animated, null, world, inverse );
		Assert.Equal( new Vector3( 1.25f, 0f, 0f ), result.State.Release );
		Assert.Equal( state.Pin, result.Target.Translation );
		Assert.Equal( Vector3.One, result.State.Offset );
		Assert.Null( result.Reference );
	}

	[Fact]
	public void PinDisplacementIsCappedWithoutChangingTheRequestedPin()
	{
		var state = Pinned with { Pin = new Vector3( 3f, 4f, 0f ) };
		var result = Solve( state );
		Assert.Equal( state.Pin, result.State.Pin );
		Assert.InRange( Vector3.Distance( new Vector3( .3f, .4f, 0f ), result.Target.Translation ), 0f, 1e-7f );
	}

	[Fact]
	public void ReleaseOffsetStepAndRequestedTargetHaveSeparateLimits()
	{
		var result = Solve( Pinned with { Mode = 3, Release = Vector3.UnitX } );
		Assert.Equal( new Vector3( .125f, 0f, 0f ), result.State.Offset );
		Assert.Equal( Vector3.UnitX, result.State.Release );
		Assert.Equal( Vector3.UnitX, result.State.Pin );
		Assert.Equal( new Vector3( .5f, 0f, 0f ), result.Target.Translation );
	}

	[Fact]
	public void ReleaseCarriesAccumulatedOffsetIntoTheNextInvocation()
	{
		var first = Solve( Pinned with { Mode = 3, Offset = Vector3.UnitX, Weight = .25f } );
		Assert.Equal( new Vector3( .75f, 0f, 0f ), first.State.Release );
		Assert.Equal( new Vector3( .875f, 0f, 0f ), first.State.Offset );
		var second = Solve( first.State );
		Assert.Equal( new Vector3( .84375f, 0f, 0f ), second.State.Release );
		Assert.Equal( second.State.Release, second.State.Offset );
	}

	[Fact]
	public void ReferenceRetainsItsAnimatedTransformRelativeToTheEndBone()
	{
		var animated = Identity with { Translation = Vector3.UnitX };
		var reference = Identity with { Translation = Vector3.UnitX + Vector3.UnitZ };
		var result = CreateSolver().Solve( Pinned with { Pin = new Vector3( 1.25f, 0f, 0f ) },
			animated, reference, Identity, Identity );
		Assert.True( result.Reference.HasValue );
		Assert.Equal( new Vector3( 1.25f, 0f, 1f ), result.Reference.Value.Translation );
		Assert.Equal( Identity.Rotation, result.Reference.Value.Rotation );
	}

	[Fact]
	public void OrientationUsesTheIncomingWeightThenDecays()
	{
		var state = Pinned with { Normal = new Vector3( .25f, 1f, 0f ), OrientationWeight = .5f };
		var result = Solve( state );
		Assert.Equal( .25f, result.State.OrientationWeight );
		Assert.InRange( MathF.Abs( result.Target.Rotation.Y.X - MathF.Sin( .125f ) ), 0f, 1e-6f );
		Assert.InRange( MathF.Abs( result.Target.Rotation.Y.Y - MathF.Cos( .125f ) ), 0f, 1e-6f );
		var held = Solve( state with { OrientationRequested = true } );
		Assert.Equal( .5f, held.State.OrientationWeight );
		Assert.Equal( result.Target, held.Target );
	}

	[Theory]
	[InlineData( 1f )]
	[InlineData( -1f )]
	public void ParallelNormalDoesNotInventAnOrientationAxis( float direction )
	{
		var result = Solve( Pinned with { Normal = Vector3.UnitY * direction, OrientationWeight = .1f } );
		Assert.Equal( Identity.Rotation, result.Target.Rotation );
		Assert.Equal( 0f, result.State.OrientationWeight );
	}

	[Fact]
	public void SmallDisplacementsBypassReciprocalScaling()
	{
		var pin = new Vector3( .00005f, 0f, 0f );
		var result = Solve( Pinned with { Pin = pin } );
		Assert.Equal( pin, result.Target.Translation );
		Assert.Equal( Identity.Rotation, result.Target.Rotation );
	}

	[Fact]
	public void ArcSineUsesSuppliedCoefficientsAndPreservesEndpointSigns()
	{
		var arc = CreateArcSine();
		Assert.Equal( .25f, arc.Evaluate( .25f ) );
		Assert.Equal( -.25f, arc.Evaluate( -.25f ) );
		Assert.Equal( MathF.PI / 2f, arc.Evaluate( 1f ) );
		Assert.Equal( -MathF.PI / 2f, arc.Evaluate( -1f ) );
		Assert.True( float.IsNaN( arc.Evaluate( 2f ) ) );
		Assert.Equal( int.MinValue, BitConverter.SingleToInt32Bits( arc.Evaluate( 0f ) ) );
	}

	[Fact]
	public void InvalidTargetLimitsAreRejected()
	{
		Assert.Throws<ArgumentException>( () => new EffectorTargetSolver( CreateMath(), CreateArcSine(), Settings with { MaximumReleaseStep = -1f } ) );
		Assert.Throws<ArgumentException>( () => new EffectorTargetSolver( CreateMath(), CreateArcSine(), Settings with { MinimumAngle = 1f } ) );
		Assert.Throws<ArgumentException>( () => new LimbArcSine( 0f, float.NaN, 0f, 1f, 1f ) );
	}

	private static EffectorTargetResult Solve( EffectorTargetState state ) => CreateSolver().Solve( state, Identity, null, Identity, Identity );

	private static EffectorTargetSolver CreateSolver() => new( CreateMath(), CreateArcSine(), Settings );

	private static LimbArcSine CreateArcSine() => new( 0f, 0f, 0f, 1f, MathF.PI / 2f );

	private static LimbSolverMath CreateMath() => new(
		new[] { -1f / 720f, 1f / 24f, -.5f }, new[] { -1f / 5040f, 1f / 120f, -1f / 6f },
		new[] { 0f, 0f, 0f, 0f, 0f, 0f, 0f, MathF.PI / 2f }, 2f / MathF.PI, MathF.PI / 2f, 0f, MathF.PI );
}
