using SkateMotion.Core.Camera;
using Xunit;

namespace SboxSkateMotion.Tests;

public class CameraOrbitTests
{
	private const float Frame = 1f / 60f;
	private const float Degree = MathF.PI / 180f;

	private static void Rest( CameraOrbit orbit, float seconds, bool recentre = true )
	{
		for ( var time = 0f; time < seconds - 1e-4f; time += Frame )
			orbit.Update( 0f, 0f, recentre, -1f, 1f, Frame );
	}

	[Fact]
	public void LookInputTurnsTheCameraOneToOne()
	{
		var orbit = new CameraOrbit();
		orbit.Update( 3f * Degree, -2f * Degree, true, -1f, 1f, Frame );
		Assert.Equal( 3f * Degree, orbit.Yaw, 5 );
		Assert.Equal( -2f * Degree, orbit.Pitch, 5 );
		orbit.Update( 3f * Degree, 0f, true, -1f, 1f, Frame );
		Assert.Equal( 6f * Degree, orbit.Yaw, 5 );
	}

	[Fact]
	public void NoRecentreUntilTheMouseHasRested()
	{
		var orbit = new CameraOrbit();
		for ( var frame = 0; frame < 36; frame++ )
			orbit.Update( 3f * Degree, 0f, true, -1f, 1f, Frame );
		var turned = orbit.Yaw;
		Rest( orbit, orbit.RecentreDelay - 0.05f );
		Assert.Equal( turned, orbit.Yaw, 5 );
	}

	[Fact]
	public void RecentreStartsGentlyAndComesBackBehind()
	{
		var orbit = new CameraOrbit();
		for ( var frame = 0; frame < 36; frame++ )
			orbit.Update( 3f * Degree, 1f * Degree, true, -1f, 1f, Frame );
		Rest( orbit, orbit.RecentreDelay );
		var before = orbit.Yaw;
		orbit.Update( 0f, 0f, true, -1f, 1f, Frame );
		// The first frame of the recentre barely moves: the pull builds up instead of snapping on.
		Assert.True( MathF.Abs( before - orbit.Yaw ) < 0.05f * Degree, $"first frame moved {(before - orbit.Yaw) / Degree} deg" );
		Rest( orbit, 1.8f );
		Assert.True( MathF.Abs( orbit.Yaw ) < 5f * Degree, $"still {orbit.Yaw / Degree} deg off after 3 s" );
		Assert.True( MathF.Abs( orbit.Pitch ) < 2f * Degree );
		// It eases all the way back without swinging past behind.
		var lowest = orbit.Yaw;
		for ( var frame = 0; frame < 180; frame++ )
		{
			orbit.Update( 0f, 0f, true, -1f, 1f, Frame );
			lowest = MathF.Min( lowest, orbit.Yaw );
		}
		Assert.True( lowest >= -1e-3f, $"swung past: {lowest / Degree} deg" );
	}

	[Fact]
	public void MovingTheMouseStopsTheRecentreAtOnce()
	{
		var orbit = new CameraOrbit();
		for ( var frame = 0; frame < 36; frame++ )
			orbit.Update( 3f * Degree, 0f, true, -1f, 1f, Frame );
		Rest( orbit, orbit.RecentreDelay + 0.5f );
		orbit.Update( 0.5f * Degree, 0f, true, -1f, 1f, Frame );
		var held = orbit.Yaw;
		// No leftover recentre motion fights the player's input on the following frames.
		orbit.Update( 0f, 0f, true, -1f, 1f, Frame );
		orbit.Update( 0f, 0f, true, -1f, 1f, Frame );
		Assert.Equal( held, orbit.Yaw, 5 );
	}

	[Fact]
	public void OnFootTheCameraStaysWhereItWasTurned()
	{
		var orbit = new CameraOrbit();
		for ( var frame = 0; frame < 30; frame++ )
			orbit.Update( 4f * Degree, 0f, false, -1f, 1f, Frame );
		var turned = orbit.Yaw;
		Rest( orbit, 5f, recentre: false );
		Assert.Equal( turned, orbit.Yaw, 5 );
	}

	[Fact]
	public void PitchInputStopsAtTheLimits()
	{
		var orbit = new CameraOrbit();
		for ( var frame = 0; frame < 100; frame++ )
			orbit.Update( 0f, 2f * Degree, true, -0.2f, 0.5f, Frame );
		Assert.Equal( 0.5f, orbit.Pitch, 5 );
		for ( var frame = 0; frame < 100; frame++ )
			orbit.Update( 0f, -2f * Degree, true, -0.2f, 0.5f, Frame );
		Assert.Equal( -0.2f, orbit.Pitch, 5 );
	}

	[Fact]
	public void ShiftKeepsTheViewAndIsNotInput()
	{
		var orbit = new CameraOrbit();
		Rest( orbit, 2f );
		orbit.Shift( 1f );
		Assert.Equal( 1f, orbit.Yaw, 5 );
		Assert.True( orbit.Idle > orbit.RecentreDelay );
	}
}
