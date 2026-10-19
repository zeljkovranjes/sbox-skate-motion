using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class PhysicalContactFeedbackTests
{
	[Fact]
	public void HigherPriorityReplacesDirectionsButNotEarlierImpactChannels()
	{
		var state = State();
		state.Feedback.AuthoredWeights[ 2 ] = 2f;
		Step( state, [Contact( 1, Vector3.UnitX ), Contact( 2, Vector3.UnitZ )] );
		Assert.Equal( 1, state.Feedback.Counter );
		Assert.Equal( 2, state.Bones[ 0 ] );
		Assert.Equal( Vector3.UnitZ, state.Normals[ 0 ] );
		Assert.True( state.Feedback.BoneScalars[ 2 ] > 0f );
		Assert.Equal( 1, state.Feedback.BoneFlags[ 1 ] );
	}

	[Fact]
	public void FullSelectionRejectsEvenALaterHigherPriorityContact()
	{
		var state = State();
		state.Feedback.AuthoredWeights[ 2 ] = 2f;
		var contacts = Enumerable.Repeat( Contact( 1, Vector3.UnitX ), 20 )
			.Append( Contact( 2, Vector3.UnitZ ) ).ToArray();
		Step( state, contacts );
		Assert.Equal( 20, state.Feedback.Counter );
		Assert.All( state.Bones, bone => Assert.Equal( 1, bone ) );
		Assert.Equal( 0, state.Feedback.BoneFlags[ 2 ] );
		Assert.Equal( 1f, state.Feedback.ControlScalars[ 2 ] );
	}

	[Fact]
	public void RejectedContactsStillPublishSurfacePresence()
	{
		var state = State();
		var contact = Contact( 15, Vector3.UnitY ) with { SurfaceFlags = 12 << 7, Point = new( 0f, -.5f, 0f ) };
		Step( state, [contact], Input() with { ActorGate = false, SpecialState = true } );
		Assert.True( state.AnyContact );
		Assert.Equal( 1, state.Feedback.ControlFlags[ 13 ] );
		Assert.Equal( -.5f, state.Feedback.ControlScalars[ 9 ] );
		Assert.Equal( 0, state.Feedback.Counter );
		Assert.All( state.Feedback.BoneFlags, flag => Assert.Equal( 0, flag ) );
	}

	[Fact]
	public void PinRetentionSurvivesSecondaryModeButClearsInPrimaryMode()
	{
		var state = State();
		state.Pins[ 0 ].Bone = 15;
		state.Pins[ 0 ].RadiusSquared = .04f;
		var input = Input() with { SecondaryMode = true };
		Step( state, [Contact( 15, Vector3.UnitX )], input );
		Assert.True( state.Pins[ 0 ].Near );
		Assert.True( state.Pins[ 0 ].RetainedNear );
		Assert.Equal( 0f, state.Feedback.BoneScalars[ 30 ] );
		Assert.True( state.Feedback.BoneScalars[ 31 ] > 0f );
		Step( state, [], input );
		Assert.False( state.Pins[ 0 ].Near );
		Assert.True( state.Pins[ 0 ].RetainedNear );
		Step( state, [] );
		Assert.False( state.Pins[ 0 ].RetainedNear );
	}

	[Fact]
	public void PinRadiusComparisonIsStrict()
	{
		var state = State();
		state.Pins[ 0 ].Bone = 15;
		state.Pins[ 0 ].RadiusSquared = 1f;
		Step( state, [Contact( 15, Vector3.UnitX ) with { Point = Vector3.UnitX }] );
		Assert.False( state.Pins[ 0 ].Near );
		Assert.True( state.Feedback.BoneScalars[ 30 ] > 0f );
		Assert.Equal( 0f, state.Feedback.BoneScalars[ 31 ] );
	}

	[Fact]
	public void RootContactDoesNotRestartTheImpactTimer()
	{
		var state = State();
		state.Feedback.ControlScalars[ 0 ] = .5f;
		Step( state, [Contact( 0, Vector3.UnitX )], Input() with { DeltaTime = .25f } );
		Assert.Equal( .25f, state.Feedback.ControlScalars[ 0 ] );
		Assert.Equal( .75f, state.Feedback.Blend );
	}

	[Fact]
	public void MissingContactsClearCurrentGroupsButPreserveTheirMaterialValues()
	{
		var state = State();
		Step( state, [Contact( 1, Vector3.UnitX ) with { SurfaceFlags = 37 }] );
		Assert.Equal( 37, state.Feedback.GroupValues[ 0 ] );
		Step( state, [] );
		Assert.Equal( 37, state.Feedback.GroupValues[ 0 ] );
		Assert.Equal( -1, state.Feedback.GroupIndices[ 0 ] );
		Assert.Equal( Vector4.Zero, state.Feedback.GroupVectors[ 0 ] );
		Assert.Equal( 0, state.Feedback.Counter );
		Assert.False( state.AnyContact );
	}

	[Theory]
	[InlineData( -.02f, true )]
	[InlineData( .02f, false )]
	public void GroupPinchUsesSignedBodySeparation( float separation, bool expected )
	{
		var state = State();
		var poses = Poses();
		poses[ 17 ] = new( LimbRigidTransform.Identity.Rotation, new( separation, 0f, 0f ) );
		Step( state, [Contact( 2, Vector3.UnitX ), Contact( 17, -Vector3.UnitX )], bodyPoses: poses );
		Assert.Equal( expected ? 1 : 0, state.Feedback.ControlFlags[ 7 ] );
	}

	[Theory]
	[InlineData( .02f, true )]
	[InlineData( .04f, false )]
	public void PointPinchUsesContactSeparation( float separation, bool expected )
	{
		var state = State();
		Step( state, [Contact( 2, Vector3.UnitX ), Contact( 17, -Vector3.UnitX ) with { Point = new( separation, 0f, 0f ) }],
			Input() with { SecondaryMode = true } );
		Assert.Equal( expected ? 1 : 0, state.Feedback.ControlFlags[ 8 ] );
	}

	[Fact]
	public void ObjectIdentifierTracksTheLastSuppliedIdentifierIndependentlyOfPeakSpeed()
	{
		var state = State();
		Step( state, [Contact( 1, Vector3.UnitX ) with { OtherKind = 5, OtherIdentifier = 17 },
			Contact( 1, Vector3.UnitZ ) with { OtherKind = 5, OtherIdentifier = 31 }] );
		Assert.Equal( 31, state.Feedback.SelectedIndex );
		Assert.True( state.Feedback.ControlScalars[ 6 ] > 0f );
		Assert.Equal( 1, state.Feedback.BoneControlFlags[ 6 ] );
	}

	[Fact]
	public void InvalidBoneFailsBeforeMutatingHistory()
	{
		var state = State();
		state.Feedback.ControlScalars[ 0 ] = .5f;
		Assert.Throws<ArgumentOutOfRangeException>( () => Step( state, [Contact( 24, Vector3.UnitX )] ) );
		Assert.Equal( .5f, state.Feedback.ControlScalars[ 0 ] );
	}

	private static PhysicalContactFeedbackState State()
	{
		var feedback = new PhysicalRigFeedbackState();
		Array.Fill( feedback.AuthoredWeights, 1f );
		Array.Fill( feedback.AuthoredFlags, (byte)1 );
		return new( feedback );
	}

	private static PhysicalContactFeedbackInput Input() => new(
		Vector3.Zero, Vector3.UnitY, Vector3.Zero, Vector3.Zero, 1f / 60f,
		false, true, false, false, false, false, false );

	private static PhysicalRiderContact Contact( int bone, Vector3 normal ) => new(
		bone, normal, Vector3.Zero, 0, 0, false, new( new( 3f, 2f, 1f ), 0f, true ),
		new( Vector3.Zero, 0f, true ), Vector3.One );

	private static LimbRigidTransform[] Poses() => Enumerable.Repeat( LimbRigidTransform.Identity, 24 ).ToArray();

	private static void Step( PhysicalContactFeedbackState state, PhysicalRiderContact[] contacts,
		PhysicalContactFeedbackInput? input = null, LimbRigidTransform[]? bodyPoses = null )
	{
		var readback = new PhysicalPoseResetState( 26 );
		Array.Fill( readback.Poses, LimbRigidTransform.Identity );
		PhysicalContactFeedback.Update( state, readback, bodyPoses ?? Poses(), Enumerable.Repeat( .02f, 24 ).ToArray(),
			new( 1f, 10f, .25f, .6f, .75f, 1.5f ), input ?? Input(), contacts );
	}
}
