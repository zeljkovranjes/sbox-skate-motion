using System.Numerics;
using SkateMotion.Core.Animation;
using SkateMotion.Core.Collision;
using Xunit;

namespace SboxSkateMotion.Tests;

public class PhysicalHandInteractionPipelineTests
{
	private static PhysicalHandRequestInput Input => new(
		Vector3.Zero, new Vector3( 1, 4, 0 ), Vector3.UnitZ, -Vector3.UnitZ, Vector3.UnitX, 0 );
	private static PhysicalHandInteractionContext Context => new( 1UL << 22, 1UL << 60, LimbRigidTransform.Identity );

	[Fact]
	public void OwnerActivationDefersConsumptionAndContactUntilEnabled()
	{
		var pipeline = Create();
		var state = new PhysicalHandCandidatePipelineState();
		var contact = Contact();
		var rig = Rig();
		var cache = new EffectorPoseCache( 24 );
		var pinning = new bool[ 4 ];
		Assert.True( pipeline.Prepare( [ Chunk() ], [], state, Input, Vector3.Zero, -1 ) );
		pipeline.Update( state, contact, rig, cache, pinning, Context with { ActionFlags = 0 } );
		Assert.Equal( (byte)1, state.Candidate.Pending );
		Assert.Equal( .5f, contact.Weight );
		Assert.All( pinning, pin => Assert.False( pin ) );
		pipeline.Update( state, contact, rig, cache, pinning, Context );
		Assert.Equal( (byte)0, state.Candidate.Pending );
		Assert.Equal( state.Request.Source.Point, state.Candidate.Point );
		Assert.True( pinning[ 2 ] );
		Assert.False( pinning[ 3 ] );
		Assert.False( state.Surface.TryConsume( out _ ) );
	}

	[Theory]
	[InlineData( false )]
	[InlineData( true )]
	public void RetainedCandidateContinuesPinningAndUsesCurrentOwnerContext( bool reset )
	{
		var pipeline = Create();
		var state = new PhysicalHandCandidatePipelineState();
		var contact = Contact();
		var rig = Rig();
		var cache = new EffectorPoseCache( 24 );
		var pinning = new bool[ 4 ];
		Assert.True( pipeline.Prepare( [ Chunk() ], [], state, Input, Vector3.Zero, -1 ) );
		pipeline.Update( state, contact, rig, cache, pinning, Context );
		Assert.False( state.Surface.TryConsume( out _ ) );
		Array.Clear( pinning );
		pipeline.Update( state, contact, rig, cache, pinning, Context with
		{
			ActionFlags = (1UL << 22) | (1UL << 2), RigFlags = reset ? 0 : 1UL << 60
		} );
		Assert.False( pinning[ 2 ] );
		Assert.True( pinning[ 3 ] );
		Assert.Equal( reset ? 1f / 6f : 5f / 6f, contact.Weight, 6 );
		Assert.Equal( state.Request.Source.Point, state.Candidate.Point );
	}

	[Fact]
	public void SourceMissAfterSuccessPreservesContactWithoutUpdatingPins()
	{
		var pipeline = Create();
		var state = new PhysicalHandCandidatePipelineState();
		var contact = Contact();
		var rig = Rig();
		var cache = new EffectorPoseCache( 24 );
		var pinning = new bool[ 4 ];
		Assert.True( pipeline.Prepare( [ Chunk() ], [], state, Input, Vector3.Zero, -1 ) );
		pipeline.Update( state, contact, rig, cache, pinning, Context );
		var weight = contact.Weight;
		var radius = contact.Radius;
		Array.Clear( pinning );
		Assert.False( pipeline.Prepare( [], [], state, Input, Vector3.Zero, -1 ) );
		pipeline.Update( state, contact, rig, cache, pinning, Context );
		Assert.Equal( weight, contact.Weight );
		Assert.Equal( radius, contact.Radius );
		Assert.All( pinning, pin => Assert.False( pin ) );
		Assert.Equal( 0UL, state.Candidate.Flags & (1UL << 31) );
	}

	[Fact]
	public void BlockedSurfacePreventsContactThenEmptyWorldAllowsRecovery()
	{
		var pipeline = Create();
		var state = new PhysicalHandCandidatePipelineState();
		var contact = Contact();
		var rig = Rig();
		var cache = new EffectorPoseCache( 24 );
		var pinning = new bool[ 4 ];
		Assert.True( pipeline.Prepare( [ Chunk() ], [ Plane() ], state, Input, Vector3.Zero, -1 ) );
		pipeline.Update( state, contact, rig, cache, pinning, Context );
		Assert.All( pinning, pin => Assert.False( pin ) );
		Assert.Equal( .5f, contact.Weight );
		Assert.True( pipeline.Prepare( [ Chunk() ], [], state, Input, Vector3.Zero, -1 ) );
		pipeline.Update( state, contact, rig, cache, pinning, Context );
		Assert.True( pinning[ 2 ] );
	}

	private static PhysicalHandContactState Contact() => new()
	{
		Weight = .5f, Radius = new Vector4( 2f ), Flags = 1UL << 63
	};

	private static EffectorRigState Rig()
	{
		var bones = new LimbRigidTransform[ 24 ];
		Array.Fill( bones, LimbRigidTransform.Identity );
		var skeleton = new EffectorRigSkeleton( new EffectorBoneBinding[ 4 ], new int[ 24 ],
			new LimbRigidTransform[ 24 ], bones, new LimbRigidTransform[ 24 ] );
		return new( new EffectorWeightState[ 4 ], new EffectorPoseState[ 4 ], new FootReachState[ 2 ], skeleton, default );
	}

	private static PhysicalHandInteractionPipeline Create()
	{
		var math = new LimbSolverMath( [ -1f / 720f, 1f / 24f, -.5f ],
			[ -1f / 5040f, 1f / 120f, -1f / 6f ], [ 0, 0, 0, 0, 0, 0, 0, MathF.PI / 2 ],
			2 / MathF.PI, MathF.PI / 2, 0, MathF.PI );
		var reach = new PhysicalHandReachBuilder( Flat( .1f ), Flat( 12f ), Flat( 0f ), Flat( 1f ),
			Flat( 2f ), Flat( .5f ), Flat( 1f ), .25f );
		var source = new PhysicalHandSourceController( reach,
			new PhysicalHandSourceSelection( new LimbSignedAngle( math, .0001f, MathF.PI * 2, 1 / (MathF.PI * 2) ) ), 1.5f );
		var gate = new PhysicalHandSourceEligibility( new LimbArcSine( -.5f, .9f, -5.5f, 5.6f, 1.57f ), 1f, 30f );
		var plan = new GrindSurfaceProbePlan( new( 1e-6f, 1e-5f, .09f, .04f, 1.06f, .001f, 6 ) );
		var request = new PhysicalHandRequestController( gate, source, plan, .3f );
		var classifier = new GrindSurfaceClassifier( math, new( .65f, 1e-6f, .9f, .99f,
			-.035f, .035f, -.24f, -.2f, MathF.PI / 2, MathF.PI * 2, .01f ) );
		var query = new MeshWorldQuery( 1e-8f, -.00001f, 2.938735877055719e-39f, 1.52587890625e-5f, 1e-6f );
		var response = new GrindEdgeSelectionCurve( [ -3, -2, -1, 0, .1f, .2f, .5f, 1 ], [ .5f, .4f, .3f, .2f, .1f, 0, -.1f, -.2f ] );
		var candidate = new PhysicalHandCandidateController( math, response, .3f, 0f, .2f );
		var preparation = new PhysicalHandCandidatePipeline( request, classifier, query, candidate );
		var contact = new PhysicalHandContactController( Flat( .2f ), .1f, .12f, .2f, .03f );
		return new( preparation, new PhysicalHandInteractionController( candidate, contact ) );
	}

	private static GrindHeightCorrectionCurve Flat( float value ) => new( [ 0f, 2f, 5f, 10f ], [ value, value, value, value ] );

	private static GrindEdgeChunk Chunk()
	{
		var a = new Vector3( -2f, .5f, -.25f );
		var b = new Vector3( 2f, .5f, -.25f );
		return new( new( new Vector3( -10 ), new Vector3( 10 ) ),
			[ new( 0, 255, [ -1, -1, -1, -1, -1, -1, -1, -1 ] ) ], [ -1 ],
			[ new( Vector3.Min( a, b ), Vector3.Max( a, b ) ) ], [ new( new( a, b ), 7, 0x80000000u ) ] );
	}

	private static MeshQueryInstance Plane()
	{
		var a = new Vector3( -3, .5f, -3 );
		var b = new Vector3( -3, .5f, 3 );
		var c = new Vector3( 3, .5f, 3 );
		var d = new Vector3( 3, .5f, -3 );
		var geometry = new MeshQueryGeometry( [ new( a, b, c, 0x183 ), new( a, c, d, 0x183 ) ], a, c );
		var identity = Matrix4x4.Identity;
		identity.M44 = 0;
		return new( geometry, identity, identity, a, c, -1, 0, 1 );
	}
}
