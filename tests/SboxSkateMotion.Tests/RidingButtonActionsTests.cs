using SkateMotion.Core.Input;
using Xunit;

namespace SboxSkateMotion.Tests;

public class RidingButtonActionsTests
{
	private static readonly RidingButtonTuning Tuning = new( .25f, .125f, .5f, .75f, 4f );

	[Fact]
	public void PushFeetRemainIndependentWhileTheInitialWindowIsOpen()
	{
		var state = State( ControllerButtons.West | ControllerButtons.South );
		var result = Read( state );
		Assert.True( result.LeftPush );
		Assert.True( result.RightPush );
		Assert.True( result.NewPush );
		Assert.True( result.Pushing );
	}

	[Fact]
	public void PushPressEdgeSurvivesALongFirstFrame()
	{
		var state = State( ControllerButtons.West );
		state.WestHeld = 1f;
		Assert.True( Read( state ).NewPush );
		state.Previous = state.Current;
		Assert.False( Read( state ).NewPush );
		Assert.True( Read( state ).LeftPush );
	}

	[Fact]
	public void PushHoldWindowUsesStrictComparison()
	{
		var state = State( ControllerButtons.South );
		state.Previous = state.Current;
		state.SouthHeld = .25f;
		Assert.False( Read( state ).NewPush );
		state.SouthHeld = .249f;
		Assert.True( Read( state ).NewPush );
	}

	[Theory]
	[InlineData( false )]
	[InlineData( true )]
	public void AutomaticPushUsesTheStanceFoot( bool goofy )
	{
		var result = Read( State(), new() { AutoPush = true, Goofy = goofy }, 4f, 1f );
		Assert.Equal( !goofy, result.LeftPush );
		Assert.Equal( goofy, result.RightPush );
		Assert.True( result.NewPush );
	}

	[Theory]
	[InlineData( 3.5f, 1f )]
	[InlineData( -3.5f, 1f )]
	[InlineData( 4f, .75f )]
	public void AutomaticPushRequiresStrictAngleAndMagnitudeLimits( float angle, float magnitude )
	{
		Assert.False( Read( State(), new() { AutoPush = true }, angle, magnitude ).Pushing );
	}

	[Fact]
	public void ManualPushOverridesAutomaticFootSelection()
	{
		var result = Read( State( ControllerButtons.South ), new() { AutoPush = true }, 4f, 1f );
		Assert.False( result.LeftPush );
		Assert.True( result.RightPush );
	}

	[Fact]
	public void SimultaneousPushRestrictionCancelsBothFeet()
	{
		var result = Read( State( ControllerButtons.West | ControllerButtons.South ), new() { SuppressSimultaneousPush = true } );
		Assert.False( result.LeftPush );
		Assert.False( result.RightPush );
		Assert.False( result.NewPush );
	}

	[Fact]
	public void PushSuppressionRetainsExitGrindAndIndependentSprint()
	{
		var state = State( ControllerButtons.South );
		state.SouthHeld = .1f;
		var result = Read( state, new() { SuppressPush = true, AutoPush = true }, 4f, 1f );
		Assert.False( result.Pushing );
		Assert.True( result.ExitGrind );
		Assert.True( result.Sprint );
		Assert.True( result.NoInput );
	}

	[Fact]
	public void GrabRequestsDistinguishPositiveFromExactlyFullPressure()
	{
		var state = State();
		state.Current = state.Current with { LeftTrigger = .5f, RightTrigger = 1f };
		var result = Read( state );
		Assert.True( result.LeftAirGrab );
		Assert.False( result.LeftGroundGrab );
		Assert.True( result.RightAirGrab );
		Assert.True( result.RightGroundGrab );
		state.Current = state.Current with { LeftTrigger = -.25f, RightTrigger = 1.25f };
		result = Read( state );
		Assert.False( result.LeftAirGrab );
		Assert.True( result.RightAirGrab );
		Assert.False( result.RightGroundGrab );
	}

	[Fact]
	public void MountSuppressionCanRemoveOnlyTheNewRequest()
	{
		var result = Read( State( ControllerButtons.North ), new() { SuppressNewToggle = true } );
		Assert.True( result.ToggleOffBoard );
		Assert.False( result.NewToggleOffBoard );
		Assert.False( result.NoInput );
	}

	[Fact]
	public void PreparedMountAndLeftBumperBlockBothToggleRequests()
	{
		var prepared = Read( State( ControllerButtons.North ), new() { MountPrepared = true } );
		var bumper = Read( State( ControllerButtons.North | ControllerButtons.LeftBumper ) );
		Assert.False( prepared.ToggleOffBoard );
		Assert.False( prepared.NewToggleOffBoard );
		Assert.False( bumper.ToggleOffBoard );
		Assert.False( bumper.NewToggleOffBoard );
		Assert.True( bumper.NoInput );
	}

	[Fact]
	public void NewMountHasItsOwnWindowAndInitialEdge()
	{
		var state = State( ControllerButtons.North );
		state.NorthHeld = .125f;
		Assert.True( Read( state ).NewToggleOffBoard );
		state.Previous = state.Current;
		Assert.False( Read( state ).NewToggleOffBoard );
		state.NorthHeld = .124f;
		Assert.True( Read( state ).NewToggleOffBoard );
	}

	[Fact]
	public void DarkCatchAndBrakeHaveSeparateRestrictions()
	{
		var state = State( ControllerButtons.East );
		var brake = Read( state, new() { SuppressDarkCatch = true } );
		Assert.True( brake.Brake );
		Assert.False( brake.DarkCatch );
		Assert.True( brake.ExitGrind );
		var catchOnly = Read( state, new() { SuppressBrake = true } );
		Assert.False( catchOnly.Brake );
		Assert.True( catchOnly.DarkCatch );
		Assert.True( catchOnly.NewDarkCatch );
	}

	[Fact]
	public void RightBumperKeepsWorldGrabAfterCatchEdgeExpires()
	{
		var state = State( ControllerButtons.RightBumper );
		Assert.True( Read( state ).NewDarkCatch );
		state.Previous = state.Current;
		var result = Read( state );
		Assert.False( result.NewDarkCatch );
		Assert.True( result.DarkCatch );
		Assert.True( result.GrabWorld );
	}

	[Fact]
	public void NoInputRequestDoesNotCountDirectionalButtonsOrStickClicks()
	{
		Assert.True( Read( State( ControllerButtons.Up | ControllerButtons.LeftStick | ControllerButtons.RightStick ) ).NoInput );
		var state = State();
		state.Current = state.Current with { RightY = .00001f };
		Assert.False( Read( state ).NoInput );
	}

	[Fact]
	public void SprintDependsOnPositiveHeldTime()
	{
		var state = State( ControllerButtons.South );
		Assert.False( Read( state ).Sprint );
		state.SouthHeld = .01f;
		Assert.True( Read( state ).Sprint );
	}

	private static RidingButtonActions Read( ControllerInputState state, RidingButtonContext context = default,
		float angle = 0f, float magnitude = 0f ) => RidingButtonActions.Evaluate( state, context, Tuning, angle, magnitude );

	[Fact]
	public void BrakeHoldSuppressesSprintEvenWhenPushRemainsHeld()
	{
		var state = State( ControllerButtons.East );
		state.EastHeld = .01f;
		Assert.False( Read( state ).Sprint );
		state.Current = state.Current with { Buttons = ControllerButtons.East | ControllerButtons.South };
		state.SouthHeld = .2f;
		Assert.True( Read( state ).RightPush );
		Assert.False( Read( state ).Sprint );
		state.EastHeld = 0f;
		Assert.True( Read( state ).Sprint );
	}

	private static ControllerInputState State( ControllerButtons buttons = ControllerButtons.None ) => new()
	{
		Current = new( 0f, 0f, 0f, 0f, 0f, 0f, buttons )
	};
}
