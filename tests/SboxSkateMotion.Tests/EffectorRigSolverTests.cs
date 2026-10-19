using System;
using System.Linq;
using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class EffectorRigSolverTests
{
	[Theory]
	[InlineData( -1 )]
	[InlineData( 0 )]
	[InlineData( 4 )]
	public void InactiveModesPreserveBonesAndCaches( int mode )
	{
		var (bindings, states, parents, bind, animated, mutable) = Fixture( mode );
		var previousStates = states.ToArray();
		var previousBones = mutable.ToArray();
		bindings[ 0 ] = new EffectorBoneBinding( -100, -100, float.NaN );
		Update( bindings, states, parents, bind, animated, mutable );
		Assert.Equal( previousStates, states );
		Assert.Equal( previousBones, mutable );
	}

	[Theory]
	[InlineData( 1 )]
	[InlineData( 2 )]
	[InlineData( 3 )]
	public void ActiveModesApplyTheSolvedEndAndResidual( int mode )
	{
		var (bindings, states, parents, bind, animated, mutable) = Fixture( mode );
		Update( bindings, states, parents, bind, animated, mutable );
		Assert.Equal( -Vector3.UnitY, mutable[ 2 ].Translation );
		Assert.InRange( Vector3.Distance( -.5f * Vector3.UnitY, mutable[ 1 ].Translation ), 0f, 1e-7f );
		Assert.Equal( -Vector3.UnitY, states[ 0 ].AnimatedTarget.Translation );
		Assert.Equal( At( new Vector3( 5f, 6f, 7f ) ), states[ 0 ].AnimatedReference );
	}

	[Fact]
	public void FailedPositionSolvePreservesEveryBoneAndCache()
	{
		var (bindings, states, parents, bind, animated, mutable) = Fixture();
		animated[ 1 ] = animated[ 0 ];
		var previousStates = states.ToArray();
		var previousBones = mutable.ToArray();
		Update( bindings, states, parents, bind, animated, mutable );
		Assert.Equal( previousStates, states );
		Assert.Equal( previousBones, mutable );
	}

	[Fact]
	public void FailedRotationSolveDoesNotApplyItsNonzeroResidual()
	{
		var (bindings, states, parents, bind, animated, mutable) = Fixture();
		animated[ 1 ] = At( -.5f * Vector3.UnitY );
		animated[ 2 ] = At( -Vector3.UnitY );
		bindings[ 0 ] = bindings[ 0 ] with { MaximumReach = .7f };
		var previousStates = states.ToArray();
		var previousBones = mutable.ToArray();
		Update( bindings, states, parents, bind, animated, mutable );
		Assert.Equal( previousStates, states );
		Assert.Equal( previousBones, mutable );
	}

	[Fact]
	public void LinkedTargetSelectsItsOwnChainAndUpdatesBothEnds()
	{
		var (bindings, states, parents, bind, animated, mutable) = Fixture();
		bindings[ 0 ] = bindings[ 0 ] with { ReferenceBone = 3 };
		parents[ 2 ] = 3;
		animated[ 2 ] = At( Vector3.One * 100f );
		states[ 0 ] = states[ 0 ] with
		{
			AnimatedTarget = At( new Vector3( 2f, -3f, 4f ) ),
			AnimatedReference = At( -2f * Vector3.UnitY )
		};
		Update( bindings, states, parents, bind, animated, mutable );
		Assert.Equal( -Vector3.UnitY, mutable[ 3 ].Translation );
		Assert.Equal( new Vector3( 2f, -2f, 4f ), mutable[ 2 ].Translation );
		Assert.Equal( mutable[ 2 ], states[ 0 ].AnimatedTarget );
		Assert.Equal( mutable[ 3 ], states[ 0 ].AnimatedReference );
	}

	[Fact]
	public void BindOffsetContributesToResidualBeforeEndPlacement()
	{
		var (bindings, states, parents, bind, animated, mutable) = Fixture();
		bind[ 2 ] = At( -.5f * Vector3.UnitY );
		Update( bindings, states, parents, bind, animated, mutable );
		Assert.Equal( -.5f * Vector3.UnitY, mutable[ 2 ].Translation );
		Assert.Equal( mutable[ 2 ], states[ 0 ].AnimatedTarget );
	}

	[Fact]
	public void ModelConversionAndCacheCorrectionRemainSeparate()
	{
		var (bindings, states, parents, bind, animated, mutable) = Fixture();
		states[ 0 ] = states[ 0 ] with { AnimatedTarget = At( -3f * Vector3.UnitY ) };
		Update( bindings, states, parents, bind, animated, mutable, At( Vector3.UnitY ) );
		Assert.Equal( -Vector3.UnitY, mutable[ 2 ].Translation );
		Assert.Equal( -2f * Vector3.UnitY, states[ 0 ].AnimatedTarget.Translation );
	}

	[Fact]
	public void EndOrientationComesFromConvertedDesiredPose()
	{
		var (bindings, states, parents, bind, animated, mutable) = Fixture();
		var rotation = new LimbFrame( Vector3.UnitZ, Vector3.UnitY, -Vector3.UnitX );
		Update( bindings, states, parents, bind, animated, mutable, new LimbRigidTransform( rotation, Vector3.Zero ) );
		Assert.Equal( rotation, mutable[ 2 ].Rotation );
		Assert.Equal( LimbRigidTransform.Identity.Rotation, states[ 0 ].AnimatedTarget.Rotation );
	}

	[Fact]
	public void BindingReachControlsTheEndLimit()
	{
		var (bindings, states, parents, bind, animated, mutable) = Fixture();
		bindings[ 0 ] = bindings[ 0 ] with { MaximumReach = .5f };
		Update( bindings, states, parents, bind, animated, mutable );
		Assert.Equal( -.5f * Vector3.UnitY, mutable[ 2 ].Translation );
	}

	[Fact]
	public void SourceSkeletonAndUnusedCachesArePreserved()
	{
		var (bindings, states, parents, bind, animated, mutable) = Fixture();
		var previousAnimated = animated.ToArray();
		var previousBind = bind.ToArray();
		var previous = states[ 0 ];
		Update( bindings, states, parents, bind, animated, mutable );
		Assert.Equal( previousAnimated, animated );
		Assert.Equal( previousBind, bind );
		Assert.Equal( previous with { AnimatedTarget = states[ 0 ].AnimatedTarget }, states[ 0 ] );
	}

	[Fact]
	public void SelectedSlotUsesItsBindingWithoutChangingOtherCaches()
	{
		var (bindings, states, parents, bind, animated, mutable) = Fixture();
		states[ 3 ] = states[ 0 ];
		states[ 0 ] = states[ 0 ] with { TargetState = states[ 0 ].TargetState with { Mode = 0 } };
		bindings[ 3 ] = bindings[ 0 ];
		var previous = states.Take( 3 ).ToArray();
		Update( bindings, states, parents, bind, animated, mutable );
		Assert.Equal( previous, states.Take( 3 ) );
		Assert.Equal( -Vector3.UnitY, states[ 3 ].AnimatedTarget.Translation );
	}

	[Theory]
	[InlineData( -1 )]
	[InlineData( 4 )]
	public void InvalidActiveEndIsRejected( int end )
	{
		var (bindings, states, parents, bind, animated, mutable) = Fixture();
		bindings[ 0 ] = bindings[ 0 ] with { EndBone = end };
		Assert.Throws<ArgumentException>( () => Update( bindings, states, parents, bind, animated, mutable ) );
	}

	[Fact]
	public void InvalidParentIsRejected()
	{
		var (bindings, states, parents, bind, animated, mutable) = Fixture();
		parents[ 1 ] = 10;
		Assert.Throws<ArgumentException>( () => Update( bindings, states, parents, bind, animated, mutable ) );
	}

	private static LimbRigidTransform At( Vector3 position ) => new( LimbRigidTransform.Identity.Rotation, position );

	private static (EffectorBoneBinding[], EffectorPoseState[], int[], LimbRigidTransform[], LimbRigidTransform[], LimbRigidTransform[]) Fixture( int mode = 1 )
	{
		var bindings = Enumerable.Repeat( new EffectorBoneBinding( 2, -1, 10f ), 4 ).ToArray();
		var states = new EffectorPoseState[ 4 ];
		states[ 0 ] = new EffectorPoseState( new EffectorTargetState( mode, default, default, default, default, 0f, 0f, false ),
			At( Vector3.One * 10f ), At( Vector3.One * 20f ), At( -2f * Vector3.UnitY ), At( Vector3.One * 30f ),
			At( new Vector3( 5f, 6f, 7f ) ), At( Vector3.One * 40f ), Vector3.One * 50f, true );
		var animated = new[] { At( Vector3.Zero ), At( new Vector3( .3f, -.4f, 0f ) ), At( -.8f * Vector3.UnitY ), At( -.8f * Vector3.UnitY ) };
		return (bindings, states, new[] { 0, 0, 1, 1 }, Enumerable.Repeat( LimbRigidTransform.Identity, 4 ).ToArray(), animated, animated.ToArray());
	}

	private static void Update( EffectorBoneBinding[] bindings, EffectorPoseState[] states, int[] parents,
		LimbRigidTransform[] bind, LimbRigidTransform[] animated, LimbRigidTransform[] mutable, LimbRigidTransform? worldToModel = null )
	{
		var math = new LimbSolverMath( new[] { -1f / 720f, 1f / 24f, -.5f },
			new[] { -1f / 5040f, 1f / 120f, -1f / 6f }, new[] { 0f, 0f, 0f, 0f, 0f, 0f, 0f, MathF.PI / 2f },
			2f / MathF.PI, MathF.PI / 2f, 0f, MathF.PI );
		var positions = new LimbPositionSolver( math, .0005f, MathF.PI / 180f, 180f / MathF.PI );
		var solver = new EffectorRigSolver( new LimbChainSolver( positions, new LimbRotationSolver( .0001f, .001f ), .0001f ) );
		solver.Update( bindings, states, parents, bind, animated, mutable, worldToModel ?? LimbRigidTransform.Identity, 0f, 180f );
	}
}
