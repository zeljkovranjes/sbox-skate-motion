using System.Numerics;
using SkateMotion.Core.Animation;
using SkateMotion.Core.Collision;
using Xunit;

namespace SboxSkateMotion.Tests;

public class PhysicalHandOwnerControllerTests
{
	private static PhysicalHandOwnerInput Input => new( 1UL << 22, 1UL << 60,
		Vector3.Zero, new Vector3( 1, 4, 0 ), Vector3.UnitZ, Vector3.UnitZ, Vector3.UnitX );

	[Fact]
	public void DeactivationResetsContactWithoutReplacingInputsOrConsumingQuery()
	{
		var state = new PhysicalHandCandidatePipelineState();
		state.Request.Source.Point = Vector3.One;
		state.Request.Source.Enabled = true;
		state.Request.PreviousPoint = new Vector3( 7 );
		state.Request.Input = new( Vector3.One, Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, Vector3.UnitX, 2 );
		var previous = state.Request.Input;
		state.Candidate.Pending = 1;
		state.Surface.Complete( default );
		var owner = new PhysicalHandOwnerState { RetryCount = 4, RequestedSide = 2 };
		var contact = new PhysicalHandContactState { Weight = .5f, Radius = Vector4.One, Flags = ulong.MaxValue };
		Assert.False( new PhysicalHandOwnerController( Create(), 2 ).Prepare( [], [], owner, state, contact,
			Input with { ActionFlags = 0 }, Vector3.Zero, -1 ) );
		Assert.Equal( 0f, contact.Weight );
		Assert.Equal( new Vector4( -1 ), contact.Radius );
		Assert.Equal( 0x3fffffffffffffffUL, contact.Flags );
		Assert.False( state.Request.Source.Enabled );
		Assert.Equal( Vector3.Zero, state.Request.Source.Point );
		Assert.Equal( previous, state.Request.Input );
		Assert.Equal( new Vector3( 7 ), state.Request.PreviousPoint );
		Assert.Equal( (byte)1, state.Candidate.Pending );
		Assert.True( state.Surface.TryConsume( out _ ) );
		Assert.Equal( 4, owner.RetryCount );
		Assert.Equal( 2, owner.RequestedSide );
	}

	[Fact]
	public void RetainsSideThroughLimitThenReleasesItOnTheNextMismatch()
	{
		var controller = new PhysicalHandOwnerController( Create(), 2 );
		var owner = new PhysicalHandOwnerState { RequestedSide = 1 };
		var state = new PhysicalHandCandidatePipelineState();
		var contact = new PhysicalHandContactState();
		for ( var i = 1; i <= 3; i++ )
		{
			controller.Prepare( [], [], owner, state, contact, Input with { Velocity = new Vector3( -1, 4, 0 ) }, Vector3.Zero, -1 );
			Assert.Equal( i, owner.RetryCount );
			Assert.Equal( i <= 2 ? 1 : 0, owner.RequestedSide );
			Assert.Equal( owner.RequestedSide, state.Request.Input.RequestedSide );
		}
	}

	[Fact]
	public void MatchingDirectionClearsPriorMismatchCount()
	{
		var owner = new PhysicalHandOwnerState { RequestedSide = 1, RetryCount = 7 };
		new PhysicalHandOwnerController( Create(), 2 ).Prepare( [], [], owner, new(), new(), Input, Vector3.Zero, -1 );
		Assert.Equal( 0, owner.RetryCount );
		Assert.Equal( 1, owner.RequestedSide );
	}

	[Theory]
	[InlineData( .9499999f, 8, 2 )]
	[InlineData( .95f, 7, 0 )]
	[InlineData( .9500001f, 7, 0 )]
	public void NormalThresholdClearsSideWithoutClearingRetryCount( float y, int count, int side )
	{
		var owner = new PhysicalHandOwnerState { RequestedSide = 2, RetryCount = 7 };
		new PhysicalHandOwnerController( Create(), 100 ).Prepare( [], [], owner, new(), new(),
			Input with { Normal = new Vector3( 0, y, 1 ) }, Vector3.Zero, -1 );
		Assert.Equal( count, owner.RetryCount );
		Assert.Equal( side, owner.RequestedSide );
	}

	[Theory]
	[InlineData( false, false, 0, 0 )]
	[InlineData( false, true, 0, 0 )]
	[InlineData( true, false, 6, 2 )]
	[InlineData( true, true, 0, 1 )]
	public void RigFlagsControlRetentionAndSourceSideAdoption( bool retain, bool force, int count, int side )
	{
		var owner = new PhysicalHandOwnerState { RequestedSide = 2, RetryCount = 5 };
		var state = new PhysicalHandCandidatePipelineState();
		state.Request.Source.Side = 1;
		var flags = (retain ? 1UL << 60 : 0) | (force ? 1UL << 61 : 0);
		new PhysicalHandOwnerController( Create(), 100 ).Prepare( [], [], owner, state, new(),
			Input with { RigFlags = flags }, Vector3.Zero, -1 );
		Assert.Equal( count, owner.RetryCount );
		Assert.Equal( side, owner.RequestedSide );
	}

	[Fact]
	public void RetryOverflowUsesSignedWraparound()
	{
		var owner = new PhysicalHandOwnerState { RequestedSide = 2, RetryCount = int.MaxValue };
		new PhysicalHandOwnerController( Create(), 1 ).Prepare( [], [], owner, new(), new(), Input, Vector3.Zero, -1 );
		Assert.Equal( int.MinValue, owner.RetryCount );
		Assert.Equal( 2, owner.RequestedSide );
	}

	[Fact]
	public void SideDetectionUsesTheSurfaceFrame()
	{
		var owner = new PhysicalHandOwnerState { RequestedSide = 1, RetryCount = 5 };
		new PhysicalHandOwnerController( Create(), 2 ).Prepare( [], [], owner, new(), new(),
			Input with { Normal = Vector3.UnitX, Velocity = new Vector3( 0, 4, -1 ) }, Vector3.Zero, -1 );
		Assert.Equal( 0, owner.RetryCount );
		Assert.Equal( 1, owner.RequestedSide );
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
