using SkateMotion.Core.Camera;
using Xunit;

namespace SboxSkateMotion.Tests;

public class CameraSpringTests
{
	// A target that speeds up, cruises, and stops dead: the kind of path a board gives the camera.
	private static float Path( float time ) => time < 1f ? 2f * time * time : time < 2f ? 2f + 4f * (time - 1f) : 6f;

	private static float Run( float[] deltas, SpringTuning tuning, bool feedForward )
	{
		var spring = new CameraSpring();
		spring.Reset( 0f );
		var time = 0f;
		foreach ( var delta in deltas )
		{
			var before = Path( time );
			time += delta;
			var velocity = feedForward ? (Path( time ) - before) / delta : 0f;
			spring.Step( Path( time ), delta, tuning, velocity );
		}
		return spring.Value;
	}

	private static float[] Frames( float rate, float seconds ) =>
		Enumerable.Repeat( 1f / rate, (int)MathF.Round( seconds * rate ) ).ToArray();

	[Theory]
	[InlineData( false )]
	[InlineData( true )]
	public void SamePathAtAnyFrameRate( bool feedForward )
	{
		var tuning = SpringTuning.Critical( 8f );
		var reference = Run( Frames( 240f, 1.5f ), tuning, feedForward );
		foreach ( var rate in new[] { 30f, 60f, 144f } )
			Assert.Equal( reference, Run( Frames( rate, 1.5f ), tuning, feedForward ), 2 );
		// Uneven frames (a hitchy editor) land in the same place too.
		var uneven = new List<float>();
		var random = new Random( 7 );
		while ( uneven.Sum() < 1.5f - 0.05f )
			uneven.Add( 0.004f + (float)random.NextDouble() * 0.03f );
		uneven.Add( 1.5f - uneven.Sum() );
		Assert.Equal( reference, Run( uneven.ToArray(), tuning, feedForward ), 2 );
	}

	[Fact]
	public void CriticalStepNeverOvershoots()
	{
		var spring = new CameraSpring();
		spring.Reset( 0f );
		var highest = 0f;
		for ( var frame = 0; frame < 240; frame++ )
			highest = MathF.Max( highest, spring.Step( 1f, 1f / 60f, SpringTuning.Critical( 6f ) ) );
		Assert.True( highest <= 1f, $"overshot to {highest}" );
		Assert.Equal( 1f, spring.Value, 3 );
	}

	[Fact]
	public void FeedForwardFollowsSteadyMotionWithoutLag()
	{
		var spring = new CameraSpring();
		spring.Reset( 0f, 5f );
		var target = 0f;
		for ( var frame = 0; frame < 120; frame++ )
		{
			target += 5f / 60f;
			spring.Step( target, 1f / 60f, SpringTuning.Critical( 8f ), 5f );
		}
		Assert.Equal( target, spring.Value, 3 );
		// Without the feed-forward a critically damped follower trails a steady 5 m/s by 2 * 5 / 8 m.
		var plain = new CameraSpring();
		plain.Reset( 0f, 5f );
		target = 0f;
		for ( var frame = 0; frame < 240; frame++ )
		{
			target += 5f / 60f;
			plain.Step( target, 1f / 60f, SpringTuning.Critical( 8f ) );
		}
		Assert.Equal( 1.25f, target - plain.Value, 2 );
	}

	[Fact]
	public void VelocityStaysContinuousWhenTheTargetStopsDead()
	{
		var spring = new CameraSpring();
		spring.Reset( -1f, 4f );
		var target = 0f;
		var largest = 0f;
		var previous = spring.Velocity;
		for ( var frame = 0; frame < 180; frame++ )
		{
			if ( frame < 60 )
				target += 4f / 60f;
			spring.Step( target, 1f / 60f, SpringTuning.Critical( 8f ) );
			largest = MathF.Max( largest, MathF.Abs( spring.Velocity - previous ) );
			previous = spring.Velocity;
		}
		// A follower that copied the target would change speed by 4 m/s in one frame; this one by a fraction of it.
		Assert.True( largest < 0.6f, $"speed changed by {largest} m/s in a frame" );
	}

	[Fact]
	public void AngleTakesTheShortWayRound()
	{
		var spring = new CameraSpring( angle: true );
		spring.Reset( 3f );
		spring.Step( -3f, 0.5f, SpringTuning.Critical( 6f ) );
		// From 3 rad to -3 rad the short way crosses pi: the value grows past 3 instead of falling through zero.
		Assert.True( spring.Value > 3f || spring.Value < -3f, $"went the long way: {spring.Value}" );
		for ( var frame = 0; frame < 120; frame++ )
			spring.Step( -3f, 1f / 60f, SpringTuning.Critical( 6f ) );
		Assert.Equal( -3f, spring.Value, 2 );
	}

	[Fact]
	public void SpeedLimitHolds()
	{
		var spring = new CameraSpring();
		spring.Reset( 0f );
		var fastest = 0f;
		for ( var frame = 0; frame < 120; frame++ )
		{
			spring.Step( 10f, 1f / 60f, SpringTuning.Critical( 10f ).Limited( 2f ) );
			fastest = MathF.Max( fastest, spring.Velocity );
		}
		Assert.True( fastest <= 2f + 1e-4f, $"moved at {fastest}" );
	}
}
