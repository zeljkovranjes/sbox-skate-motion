using System;
using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class FootReachControllerTests
{
	private static readonly LimbRigidTransform Identity = LimbRigidTransform.Identity;
	private static readonly FootReachSettings Settings = new( -.25f, .25f, -.125f, .125f,
		-.5f, .0625f, .5f, .03125f, .75f, .125f, -.0625f );
	private static readonly FootReachState Initial = new( 0, .25f, 0f, Vector3.Zero );

	[Theory]
	[InlineData( 0, 7 )]
	[InlineData( 2, 7 )]
	[InlineData( 3, 7 )]
	[InlineData( 1, 8 )]
	public void InactiveTargetDecaysOffsetAndDoesNotRefreshContact( int mode, int targetId )
	{
		var states = States( Initial with { ContactState = 1, Smoothed = .25f } );
		var poses = Poses( mode );
		Run( states, poses, new FootReachInput( targetId, Vector3.One ) );
		Assert.Equal( new FootReachState( 0, 0f, .125f, Vector3.Zero ), states[ 0 ] );
		Assert.Equal( .125f, poses[ 0 ].AnimatedTarget.Translation.Y );
	}

	[Fact]
	public void ColdContactUsesOldRequestBeforeCachingFreshContact()
	{
		var states = States( Initial );
		var poses = Poses();
		Run( states, poses, new FootReachInput( 7, new Vector3( 0f, 9f, 0f ) ) );
		Assert.Equal( .125f, poses[ 0 ].AnimatedTarget.Translation.Y );
		Assert.Equal( new FootReachState( 1, .25f, .125f, new Vector3( 0f, 9f, 0f ) ), states[ 0 ] );
	}

	[Fact]
	public void MissingFreshContactKeepsOldPositionAndSetsMissingState()
	{
		var states = States( Initial with { CachedContact = Vector3.One } );
		Run( states, Poses(), new FootReachInput( 7, null ) );
		Assert.Equal( 2, states[ 0 ].ContactState );
		Assert.Equal( Vector3.One, states[ 0 ].CachedContact );
	}

	[Fact]
	public void UnknownContactStateOnlyRefreshesTheContact()
	{
		var states = States( Initial with { ContactState = 7, Requested = 4f } );
		var poses = Poses();
		var before = poses[ 0 ];
		Run( states, poses, new FootReachInput( 7, Vector3.One ) );
		Assert.Equal( before, poses[ 0 ] );
		Assert.Equal( new FootReachState( 1, 4f, 0f, Vector3.One ), states[ 0 ] );
	}

	[Theory]
	[InlineData( -.75f, 0f )]
	[InlineData( -.5f, 0f )]
	[InlineData( -.25f, .25f )]
	[InlineData( 0f, 0f )]
	[InlineData( .25f, 0f )]
	public void ClearanceCorrectionUsesAnOpenNegativeInterval( float distance, float correction )
	{
		var states = States( Initial with { ContactState = 1 } );
		var poses = Poses();
		poses[ 0 ] = poses[ 0 ] with
		{
			AnimatedTarget = Identity with { Translation = new Vector3( 0f, distance + Settings.Clearance, 0f ) }
		};
		Run( states, poses, new FootReachInput( 7, null ), suppressed: true );
		Assert.Equal( correction, states[ 0 ].Smoothed );
		Assert.Equal( distance + Settings.Clearance + correction, poses[ 0 ].AnimatedTarget.Translation.Y );
	}

	[Theory]
	[InlineData( false, .0625f )]
	[InlineData( true, 0f )]
	public void SuppressedRequestChangesHeightSmoothing( bool suppressed, float requested )
	{
		var states = States( Initial with { ContactState = 1 } );
		var poses = Poses();
		poses[ 0 ] = poses[ 0 ] with { AnimatedTarget = Identity with { Translation = Vector3.UnitY } };
		Run( states, poses, new FootReachInput( 7, null ), suppressed: suppressed );
		Assert.Equal( requested, states[ 0 ].Requested );
		Assert.Equal( requested, states[ 0 ].Smoothed );
	}

	[Theory]
	[InlineData( .75f, false )]
	[InlineData( .875f, true )]
	public void HighContactCanRaiseStatusFromReferenceHeight( float referenceHeight, bool expected )
	{
		var states = States( Initial with { ContactState = 1, CachedContact = Vector3.UnitY } );
		var flags = Run( states, Poses(), new FootReachInput( 7, null ), referenceHeight: referenceHeight );
		Assert.Equal( new FootReachFlags( expected, expected ), flags );
	}

	[Fact]
	public void MissingContactRaisesBothFlagsWhenReactionIsEnabled()
	{
		var states = States( Initial with { ContactState = 2 } );
		var flags = Run( states, Poses(), new FootReachInput( 7, null ), reaction: true );
		Assert.Equal( new FootReachFlags( true, true ), flags );
	}

	[Fact]
	public void FrameFlagClearsWhilePersistentFlagSurvives()
	{
		var flags = Run( States( Initial ), Poses( 0 ), new FootReachInput( 7, null ),
			flags: new FootReachFlags( true, true ) );
		Assert.Equal( new FootReachFlags( true, false ), flags );
	}

	[Fact]
	public void OffsetUsesSuppliedOrientationAndMovesBothCaches()
	{
		var states = States( Initial );
		var poses = Poses();
		var orientation = new LimbFrame( -Vector3.UnitY, Vector3.UnitX, Vector3.UnitZ );
		new FootReachController( Settings ).Update( Inputs( new FootReachInput( 7, null ) ), states, poses,
			7, Identity, orientation, 0f, false, false, default );
		Assert.Equal( new Vector3( .125f, 0f, 0f ), poses[ 0 ].AnimatedTarget.Translation );
		Assert.Equal( new Vector3( 3.125f, 4f, 5f ), poses[ 0 ].AnimatedReference.Translation );
	}

	[Fact]
	public void SuccessiveUpdatesAccumulateOnlyTheSmoothedOffset()
	{
		var states = States( Initial );
		Run( states, Poses(), new FootReachInput( 7, null ) );
		var poses = Poses();
		Run( states, poses, new FootReachInput( 7, null ) );
		Assert.Equal( .25f, states[ 0 ].Smoothed );
		Assert.Equal( .25f, poses[ 0 ].AnimatedTarget.Translation.Y );
	}

	[Fact]
	public void HandSlotsAndUnrelatedPoseStateArePreserved()
	{
		var poses = Poses();
		var before = poses[ 2 ];
		Run( States( Initial ), poses, new FootReachInput( 7, null ) );
		Assert.Equal( before, poses[ 2 ] );
		Assert.Equal( before, poses[ 3 ] );
		Assert.Equal( before.TargetState, poses[ 0 ].TargetState );
		Assert.Equal( before.PinnedTarget, poses[ 0 ].PinnedTarget );
	}

	[Fact]
	public void InvalidSettingsAndSlotCountsAreRejected()
	{
		Assert.Throws<ArgumentException>( () => new FootReachController( Settings with { MaximumStep = float.NaN } ) );
		Assert.Throws<ArgumentException>( () => new FootReachController( Settings ).Update( new FootReachInput[ 1 ], States( Initial ),
			Poses(), 7, Identity, Identity.Rotation, 0f, false, false, default ) );
	}

	private static FootReachFlags Run( FootReachState[] states, EffectorPoseState[] poses, FootReachInput input,
		bool reaction = false, bool suppressed = false, float referenceHeight = 0f, FootReachFlags flags = default ) =>
		new FootReachController( Settings ).Update( Inputs( input ), states, poses, 7, Identity,
			Identity.Rotation, referenceHeight, reaction, suppressed, flags );
	private static FootReachInput[] Inputs( FootReachInput input ) => new[] { input, input };
	private static FootReachState[] States( FootReachState state ) => new[] { state, state };

	private static EffectorPoseState[] Poses( int mode = 1 )
	{
		var state = new EffectorPoseState( new EffectorTargetState( mode, Vector3.One, Vector3.One,
			Vector3.One, Vector3.UnitY, .5f, .5f, true ), Identity, Identity, Identity, Identity,
			Identity with { Translation = new Vector3( 3f, 4f, 5f ) }, Identity, Vector3.One, true );
		return new[] { state, state, state, state };
	}
}
