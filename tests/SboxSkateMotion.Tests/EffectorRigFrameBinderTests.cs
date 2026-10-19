using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class EffectorRigFrameBinderTests
{
	private static readonly LimbFrame QuarterTurn = new( Vector3.UnitY, -Vector3.UnitX, Vector3.UnitZ );

	[Fact]
	public void SuppliedFramesKeepTheirIndependentValues()
	{
		var source = Source();
		var result = EffectorRigFrameBinder.Bind( source, false );
		Assert.Equal( source.ModelToWorld, result.ModelToWorld );
		Assert.Equal( source.WorldToModel, result.WorldToModel );
		Assert.Equal( source.Authored, result.Authored );
		Assert.Equal( source.Contact, result.Contact );
		Assert.Equal( source.Solver, result.Solver );
		Assert.Equal( source.Offset, result.Offset );
	}

	[Fact]
	public void AnimatedRootIsComposedIntoModelSpaceBeforeBinding()
	{
		var result = EffectorRigFrameBinder.Bind( Source(), true );
		Assert.Equal( QuarterTurn, result.Authored.Rotation );
		Assert.Equal( new Vector3( 8f, 21f, 33f ), result.Authored.Translation );
		Assert.Equal( result.Authored, result.Offset );
	}

	[Fact]
	public void ContactFrameIsTheOrderedRigidInverseOfTheComposedRoot()
	{
		var result = EffectorRigFrameBinder.Bind( Source(), true );
		Assert.Equal( new LimbFrame( -Vector3.UnitY, Vector3.UnitX, Vector3.UnitZ ), result.Contact.Rotation );
		Assert.Equal( new Vector3( -21f, 8f, -33f ), result.Contact.Translation );
	}

	[Fact]
	public void AnimatedRootPathPreservesSuppliedModelAndSolverFrames()
	{
		var source = Source();
		var result = EffectorRigFrameBinder.Bind( source, true );
		Assert.Equal( source.ModelToWorld, result.ModelToWorld );
		Assert.Equal( source.WorldToModel, result.WorldToModel );
		Assert.Equal( source.Solver, result.Solver );
	}

	[Fact]
	public void TransposePathDoesNotNormalizeAffineInput()
	{
		var source = Source() with
		{
			ModelToWorld = new LimbRigidTransform( new LimbFrame( 2f * Vector3.UnitX, 3f * Vector3.UnitY, 4f * Vector3.UnitZ ), new Vector3( 10f, 20f, 30f ) )
		};
		var result = EffectorRigFrameBinder.Bind( source, true );
		Assert.Equal( source.ModelToWorld.Rotation, result.Contact.Rotation );
		Assert.Equal( new Vector3( -24f, -78f, -168f ), result.Contact.Translation );
	}

	[Theory]
	[InlineData( false )]
	[InlineData( true )]
	public void ReferenceAndMotionVectorsAreCopiedWithoutFrameConversion( bool useAnimatedRoot )
	{
		var source = Source();
		var result = EffectorRigFrameBinder.Bind( source, useAnimatedRoot );
		Assert.Equal( source.ReferencePosition, result.ReferencePosition );
		Assert.Equal( source.MotionDelta, result.MotionDelta );
	}

	[Fact]
	public void ReturningToSuppliedFramesDoesNotRetainDerivedTransforms()
	{
		var source = Source();
		var derived = EffectorRigFrameBinder.Bind( source, true );
		var supplied = EffectorRigFrameBinder.Bind( source, false );
		Assert.NotEqual( derived.Authored, supplied.Authored );
		Assert.Equal( source.Authored, supplied.Authored );
		Assert.Equal( source.Contact, supplied.Contact );
		Assert.Equal( source.Offset, supplied.Offset );
	}

	private static EffectorRigFrameSources Source()
	{
		LimbRigidTransform At( float x ) => LimbRigidTransform.Identity with { Translation = new Vector3( x, 0f, 0f ) };
		return new EffectorRigFrameSources( new LimbRigidTransform( QuarterTurn, new Vector3( 10f, 20f, 30f ) ),
			At( 2f ), At( 3f ), At( 4f ), At( 5f ), At( 6f ),
			LimbRigidTransform.Identity with { Translation = new Vector3( 1f, 2f, 3f ) },
			new Vector3( 7f, 8f, 9f ), new Vector3( -1f, -2f, -3f ) );
	}
}
