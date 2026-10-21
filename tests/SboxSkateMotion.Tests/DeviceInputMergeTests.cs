using System.Numerics;
using SkateMotion.Core.Input;
using Xunit;

namespace SboxSkateMotion.Tests;

public class DeviceInputMergeTests
{
	private static BoardControls Controls( float steer = 0f, float push = 0f, float brake = 0f, float flickX = 0f,
		float flickY = 0f, float lean = 0f, bool slide = false, bool pump = false, int grab = 0, bool hippy = false,
		int feetOff = 0, int pushFeet = 0 ) =>
		new( steer, push, brake, flickX, flickY, lean, slide, pump, grab, false, hippy, false, feetOff, pushFeet, false );

	private static DeviceActivity Active( ControllerReading reading ) =>
		DeviceInputMerge.Activity( DeviceInputMerge.Settle( reading ) );

	[Fact]
	public void DriftAndRestingTriggersReadAsNothing()
	{
		var drifting = new ControllerReading( 0.12f, -0.1f, 0.05f, 0.15f, 0.2f, 0.1f, ControllerButtons.None );
		var settled = DeviceInputMerge.Settle( drifting );
		Assert.Equal( new ControllerReading( 0f, 0f, 0f, 0f, 0f, 0f, ControllerButtons.None ), settled );
		Assert.Equal( new DeviceActivity( false, false, false ), DeviceInputMerge.Activity( settled ) );
		// Real use passes through unchanged, so the controller's own thresholds see the same deflections.
		var pushed = new ControllerReading( -0.6f, 0.3f, 0f, -0.9f, 0f, 0.8f, ControllerButtons.South );
		Assert.Equal( pushed, DeviceInputMerge.Settle( pushed ) );
		Assert.Equal( new DeviceActivity( true, true, true ), Active( pushed ) );
	}

	[Fact]
	public void AHeldSteeringKeyWinsOverTheStick()
	{
		var stick = Active( new ControllerReading( 0.9f, 0f, 0f, 0f, 0f, 0f, ControllerButtons.None ) );
		var merged = DeviceInputMerge.Board( Controls( steer: 0.4f ), true, Controls( steer: -0.9f ), stick );
		Assert.Equal( 0.4f, merged.Steer );
		// No key held: the pushed stick steers.
		merged = DeviceInputMerge.Board( Controls( steer: 0.1f ), false, Controls( steer: -0.9f ), stick );
		Assert.Equal( -0.9f, merged.Steer );
	}

	[Fact]
	public void AReleasedKeyCentresEvenWithADriftingStick()
	{
		// The steering key was let go: its ramp still eases the trucks back, and the drifting stick cannot take over
		// and keep the board turning.
		var drift = Active( new ControllerReading( -0.15f, 0.05f, 0f, 0f, 0f, 0f, ControllerButtons.None ) );
		var merged = DeviceInputMerge.Board( Controls( steer: -0.3f ), false, Controls( steer: 0.15f ), drift );
		Assert.Equal( -0.3f, merged.Steer );
		merged = DeviceInputMerge.Board( Controls(), false, Controls( steer: 0.15f ), drift );
		Assert.Equal( 0f, merged.Steer );
	}

	[Fact]
	public void PushesAndButtonsFromEitherDeviceCount()
	{
		var idle = new DeviceActivity( false, false, false );
		var merged = DeviceInputMerge.Board( Controls( push: 1f ), false, Controls( brake: 1f, slide: true, hippy: true,
			feetOff: 2, pushFeet: 1 ), idle );
		Assert.Equal( 1f, merged.Push );
		Assert.Equal( 1f, merged.Brake );
		Assert.True( merged.Slide && merged.HippyJump );
		Assert.Equal( 2, merged.FeetOff );
		Assert.Equal( 1, merged.PushFeet );
	}

	[Fact]
	public void TheFlickComesFromTheKeysOrMouseElseAPushedRightStick()
	{
		var right = new DeviceActivity( false, true, false );
		var merged = DeviceInputMerge.Board( Controls( flickY: -1f ), false, Controls( flickX: 0.8f, flickY: 0.8f ), right );
		Assert.Equal( (0f, -1f), (merged.FlickX, merged.FlickY) );
		merged = DeviceInputMerge.Board( Controls(), false, Controls( flickX: 0.8f, flickY: 0.8f ), right );
		Assert.Equal( (0.8f, 0.8f), (merged.FlickX, merged.FlickY) );
		merged = DeviceInputMerge.Board( Controls(), false, Controls( flickX: 0.8f ), new DeviceActivity( false, false, false ) );
		Assert.Equal( (0f, 0f), (merged.FlickX, merged.FlickY) );
	}

	[Fact]
	public void TriggersOnlyGrabAndCrouchWhenPulled()
	{
		var resting = new DeviceActivity( false, false, false );
		var merged = DeviceInputMerge.Board( Controls(), false, Controls( pump: true, grab: 3, lean: 1f ), resting );
		Assert.False( merged.Pump );
		Assert.Equal( 0, merged.Grab );
		Assert.Equal( 0f, merged.Lean );
		var pulled = new DeviceActivity( true, false, true );
		merged = DeviceInputMerge.Board( Controls(), false, Controls( pump: true, grab: 3, lean: 1f ), pulled );
		Assert.True( merged.Pump );
		Assert.Equal( 3, merged.Grab );
		Assert.Equal( 1f, merged.Lean );
		// A grab key wins over the trigger's grab.
		merged = DeviceInputMerge.Board( Controls( grab: 5 ), false, Controls( grab: 3 ), pulled );
		Assert.Equal( 5, merged.Grab );
	}

	[Fact]
	public void WalkingFollowsHeldKeysElseThePushedStick()
	{
		var stick = new DeviceActivity( true, false, false );
		var keys = new Vector3( 1f, 0f, 0f );
		var pad = new Vector3( 0f, -0.7f, 0f );
		Assert.Equal( keys, DeviceInputMerge.Walk( keys, true, pad, stick ) );
		Assert.Equal( pad, DeviceInputMerge.Walk( Vector3.Zero, false, pad, stick ) );
		Assert.Equal( Vector3.Zero, DeviceInputMerge.Walk( Vector3.Zero, false, pad, new DeviceActivity( false, false, false ) ) );
	}
}
