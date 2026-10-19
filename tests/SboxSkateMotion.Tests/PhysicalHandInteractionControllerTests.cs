using System.Numerics;
using SkateMotion.Core.Animation;
using SkateMotion.Core.Grinding;
using Xunit;

namespace SboxSkateMotion.Tests;

public class PhysicalHandInteractionControllerTests
{
	[Fact]
	public void ActionGatePreservesPendingCandidateAndQueryUntilEnabled()
	{
		var controller = Controller();
		var candidate = new PhysicalHandCandidateState { Pending = 1 };
		var contact = Contact();
		var rig = Rig();
		var cache = new EffectorPoseCache( 24 );
		var pinning = new bool[ 4 ];
		var surface = Surface();
		controller.Update( candidate, contact, rig, cache, pinning, surface, Input() with { ActionFlags = 0 } );
		Assert.Equal( 1, candidate.Pending );
		Assert.Equal( .5f, contact.Weight );
		Assert.All( pinning, value => Assert.False( value ) );
		controller.Update( candidate, contact, rig, cache, pinning, surface, Input() );
		Assert.Equal( 0, candidate.Pending );
		Assert.Equal( Input().Candidate.Point, candidate.Point );
		Assert.True( pinning[ 2 ] );
		Assert.False( surface.TryConsume( out _ ) );
	}

	[Theory]
	[InlineData( false, false, true )]
	[InlineData( true, false, false )]
	[InlineData( false, true, true )]
	[InlineData( true, true, true )]
	public void OwnerRigFlagsSelectContactReset( bool retained, bool forced, bool reset )
	{
		var candidate = Candidate();
		var contact = Contact();
		var flags = (retained ? 1UL << 60 : 0) | (forced ? 1UL << 61 : 0);
		Controller().Update( candidate, contact, Rig(), new EffectorPoseCache( 24 ), new bool[ 4 ],
			Surface(), Input() with { RigFlags = flags } );
		Assert.Equal( reset ? 1f / 6f : 2f / 3f, contact.Weight, 6 );
	}

	[Theory]
	[InlineData( false, false, 2 )]
	[InlineData( true, false, 3 )]
	[InlineData( false, true, 3 )]
	[InlineData( true, true, 2 )]
	public void OwnerSideCombinesWithRetainedCandidateSide( bool ownerSide, bool candidateSide, int slot )
	{
		var candidate = Candidate();
		candidate.Flags |= candidateSide ? 1UL << 29 : 0;
		var rig = Rig();
		var pinning = new bool[ 4 ];
		Controller().Update( candidate, Contact(), rig, new EffectorPoseCache( 24 ), pinning, Surface(),
			Input() with { ActionFlags = (1UL << 22) | (ownerSide ? 1UL << 2 : 0) } );
		Assert.True( pinning[ slot ] );
		Assert.False( pinning[ slot == 2 ? 3 : 2 ] );
		Assert.Equal( candidate.Point, rig.Poses[ slot ].TargetState.Pin );
	}

	[Fact]
	public void MissingFreshQueryStillUpdatesContactFromTheRetainedCandidate()
	{
		var candidate = Candidate();
		candidate.Pending = 1;
		var contact = Contact();
		var rig = Rig();
		var pinning = new bool[ 4 ];
		Controller().Update( candidate, contact, rig, new EffectorPoseCache( 24 ), pinning,
			new GrindSurfaceQueryResult(), Input() );
		Assert.Equal( 0, candidate.Pending );
		Assert.Equal( new Vector3( .25f, 0, 0 ), candidate.Point );
		Assert.Equal( candidate.Point, rig.Poses[ 2 ].TargetState.Pin );
		Assert.Equal( 2f / 3f, contact.Weight, 6 );
		Assert.True( pinning[ 2 ] );
	}

	[Fact]
	public void InvalidRetainedCandidatePreservesExistingContactAndPinRequests()
	{
		var candidate = Candidate();
		candidate.Flags = 0;
		var contact = Contact();
		var pinning = new[] { false, true, false, true };
		Controller().Update( candidate, contact, Rig(), new EffectorPoseCache( 24 ), pinning, Surface(), Input() );
		Assert.Equal( .5f, contact.Weight );
		Assert.Equal( new Vector4( 2f ), contact.Radius );
		Assert.Equal( new[] { false, true, false, true }, pinning );
	}

	private static PhysicalHandCandidateState Candidate() => new()
	{
		Point = new Vector3( .25f, 0, 0 ),
		Metric = Vector4.One,
		Flags = 1UL << 31
	};

	private static PhysicalHandContactState Contact() => new()
	{
		Weight = .5f,
		Radius = new Vector4( 2f ),
		Flags = 1UL << 63
	};

	private static PhysicalHandInteractionInput Input() => new(
		1UL << 22, 1UL << 60, LimbRigidTransform.Identity,
		new PhysicalHandCandidateInput( true, new Vector3( 1, 3, 0 ),
			new GrindEdgeRecord( new RailSegment( new Vector3( -2, 0, 0 ), new Vector3( 2, 0, 0 ) ), 0, 0 ),
			Vector3.UnitY, Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, Vector3.UnitX, Vector3.Zero ) );

	private static GrindSurfaceQueryResult Surface()
	{
		var result = new GrindSurfaceQueryResult();
		result.Complete( default( GrindSurfaceClassification ) with { Kind = GrindSurfaceKind.ThinRail } );
		return result;
	}

	private static EffectorRigState Rig()
	{
		var bones = new LimbRigidTransform[ 24 ];
		Array.Fill( bones, LimbRigidTransform.Identity );
		var skeleton = new EffectorRigSkeleton( new EffectorBoneBinding[ 4 ], new int[ 24 ],
			new LimbRigidTransform[ 24 ], bones, new LimbRigidTransform[ 24 ] );
		return new EffectorRigState( new EffectorWeightState[ 4 ], new EffectorPoseState[ 4 ],
			new FootReachState[ 2 ], skeleton, default );
	}

	private static PhysicalHandInteractionController Controller()
	{
		var math = new LimbSolverMath( [ -1f / 720f, 1f / 24f, -.5f ],
			[ -1f / 5040f, 1f / 120f, -1f / 6f ], [ 0f, 0f, 0f, 0f, 0f, 0f, 0f, MathF.PI / 2f ],
			2f / MathF.PI, MathF.PI / 2f, 0f, MathF.PI );
		var curve = new GrindEdgeSelectionCurve( [ -3, -2, -1, 0, .1f, .2f, .5f, 1 ], new float[ 8 ] );
		var candidate = new PhysicalHandCandidateController( math, curve, .3f, 0f, .2f );
		var contact = new PhysicalHandContactController(
			new GrindHeightCorrectionCurve( [ 0f, .25f, .75f, 1.5f ], [ .05f, .15f, .25f, .4f ] ),
			.1f, .12f, .2f, .03f );
		return new PhysicalHandInteractionController( candidate, contact );
	}
}
