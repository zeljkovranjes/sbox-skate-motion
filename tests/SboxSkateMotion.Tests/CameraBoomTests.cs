using SkateMotion.Core.Camera;
using Xunit;

namespace SboxSkateMotion.Tests;

public class CameraBoomTests
{
	private const float Frame = 1f / 60f;
	private const float Degree = MathF.PI / 180f;
	private const float Clear = float.PositiveInfinity;

	[Fact]
	public void NeverPassesTheHardLimitAndEasesBackOut()
	{
		var boom = new CameraBoom();
		boom.Reset( 3f );
		Assert.Equal( 1.2f, boom.UpdateDistance( 3f, Clear, 1.2f, Frame ), 4 );
		Assert.True( boom.Held );
		var previous = boom.Distance;
		var largest = 0f;
		for ( var frame = 0; frame < 180; frame++ )
		{
			boom.UpdateDistance( 3f, Clear, Clear, Frame );
			largest = MathF.Max( largest, boom.Distance - previous );
			previous = boom.Distance;
		}
		Assert.Equal( 3f, boom.Distance, 2 );
		// Out at no more than 4 m/s, starting from rest.
		Assert.True( largest <= 4f * Frame + 1e-4f, $"eased out {largest} m in a frame" );
	}

	[Fact]
	public void TheWiderProbePullsInSmoothlyBeforeAnythingIsTouched()
	{
		var boom = new CameraBoom();
		boom.Reset( 3f );
		var previous = boom.Distance;
		var largest = 0f;
		// A wall closing in: the wider probe meets it first, so the camera is already in when the hard limit arrives.
		for ( var frame = 0; frame < 60; frame++ )
		{
			var wall = 3.5f - frame * 0.04f;
			boom.UpdateDistance( 3f, wall - 0.3f, wall, Frame );
			Assert.True( boom.Distance <= wall + 1e-4f );
			largest = MathF.Max( largest, previous - boom.Distance );
			previous = boom.Distance;
		}
		Assert.False( boom.Held );
		Assert.True( largest < 0.1f, $"pulled in {largest} m in a frame" );
	}

	[Fact]
	public void CranesUpWhenTheUsualPlaceIsBlockedAndSettlesSlowly()
	{
		var boom = new CameraBoom();
		boom.Reset( 3f );
		for ( var frame = 0; frame < 60; frame++ )
			boom.UpdateCrane( 3f, 0.5f, false, Frame );
		// Blocked to half a metre of a 2.5 m range: four fifths of the full 50 degrees.
		Assert.Equal( 40f * Degree, boom.Crane, 1 );
		for ( var frame = 0; frame < 30; frame++ )
			boom.UpdateCrane( 3f, 3f, false, Frame );
		Assert.True( boom.Crane > 25f * Degree, "settled back too quickly" );
		for ( var frame = 0; frame < 600; frame++ )
			boom.UpdateCrane( 3f, 3f, false, Frame );
		Assert.Equal( 0f, boom.Crane, 2 );
	}

	[Fact]
	public void DropAheadLooksDownAndFlatGroundDoesNot()
	{
		var distances = new[] { 0.5f, 1f, 1.5f, 2f, 2.5f };
		var flat = new[] { 0f, 0f, 0f, 0f, 0f };
		var drop = new[] { 0f, 0f, -2f, -2f, -2f };
		var look = new DropLook();
		for ( var frame = 0; frame < 120; frame++ )
			look.Update( new CameraGround( distances, flat, 5, 0f ), 0f, false, 4f, Frame );
		Assert.Equal( 0f, look.Value, 4 );
		for ( var frame = 0; frame < 240; frame++ )
			look.Update( new CameraGround( distances, drop, 5, 0f ), 0f, false, 4f, Frame );
		Assert.Equal( MathF.Atan2( 2f, 1.5f ), look.Value, 2 );
		// Blocked probes and everything past them are ignored.
		var blocked = new[] { 0f, float.NaN, -2f, -2f, -2f };
		for ( var frame = 0; frame < 240; frame++ )
			look.Update( new CameraGround( distances, blocked, 5, 0f ), 0f, false, 4f, Frame );
		Assert.Equal( 0f, look.Value, 2 );
	}

	[Fact]
	public void LookDownIsCappedLowerOnFoot()
	{
		var distances = new[] { 0.2f, 0.4f, 0.6f, 0.8f, 1f };
		var cliff = new[] { -4f, -4f, -4f, -4f, -4f };
		var look = new DropLook();
		for ( var frame = 0; frame < 600; frame++ )
			look.Update( new CameraGround( distances, cliff, 5, 0f ), 0f, true, 1f, Frame );
		Assert.Equal( 40f * Degree, look.Value, 2 );
	}
}
