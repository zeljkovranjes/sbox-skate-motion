using System.Numerics;
using SkateMotion.Core.Camera;
using Xunit;

namespace SboxSkateMotion.Tests;

public class CameraDirectorTests
{
	private const float Frame = 1f / 60f;
	private const float Degree = MathF.PI / 180f;
	private const float Focus = 1.05f;

	private static readonly CameraGround Flat = CameraGround.None;

	private static CameraSubject Riding( Vector3 board, Vector3 velocity, float facing, bool airborne = false ) =>
		new( CameraSubjectMode.Riding, board, board, velocity, facing, airborne, false, false, Focus );

	private static CameraSubject OnFoot( Vector3 rider, Vector3 velocity, Vector3 board ) =>
		new( CameraSubjectMode.OnFoot, board, rider, velocity, 0f, false, false, false, Focus );

	// A carve: straight, then a steady 60 deg/s turn, then straight again, at 5 m/s.
	private static (Vector3 Position, Vector3 Velocity, float Heading) Carve( float time )
	{
		const float speed = 5f;
		const float rate = 60f * Degree;
		var heading = Math.Clamp( time - 1f, 0f, 1.5f ) * rate;
		var position = new Vector3( speed * MathF.Min( time, 1f ), 0f, 0.1f );
		if ( time > 1f )
		{
			var turning = MathF.Min( time - 1f, 1.5f );
			var radius = speed / rate;
			position += new Vector3( radius * MathF.Sin( turning * rate ), radius * (1f - MathF.Cos( turning * rate )), 0f );
		}
		if ( time > 2.5f )
			position += new Vector3( MathF.Cos( heading ), MathF.Sin( heading ), 0f ) * speed * (time - 2.5f);
		return (position, new Vector3( MathF.Cos( heading ), MathF.Sin( heading ), 0f ) * speed, heading);
	}

	private static CameraAim RunCarve( float[] deltas )
	{
		var director = new CameraDirector();
		var time = 0f;
		var aim = default( CameraAim );
		var first = Carve( 0f );
		director.Update( Riding( first.Position, first.Velocity, first.Heading ), Flat, 0f, 0f, Frame );
		foreach ( var delta in deltas )
		{
			time += delta;
			var state = Carve( time );
			aim = director.Update( Riding( state.Position, state.Velocity, state.Heading ), Flat, 0f, 0f, delta );
		}
		return aim;
	}

	[Fact]
	public void SameAimAtAnyFrameRate()
	{
		CameraAim At( float rate ) => RunCarve( Enumerable.Repeat( 1f / rate, (int)MathF.Round( 3f * rate ) ).ToArray() );
		var reference = At( 240f );
		foreach ( var rate in new[] { 30f, 60f, 144f } )
		{
			var aim = At( rate );
			Assert.True( Vector3.Distance( reference.Focus, aim.Focus ) < 0.02f, $"{rate} fps focus off by {Vector3.Distance( reference.Focus, aim.Focus )} m" );
			Assert.Equal( reference.Yaw, aim.Yaw, 2 );
			Assert.Equal( reference.Elevation, aim.Elevation, 3 );
		}
	}

	[Fact]
	public void SitsBehindTheTravelAndFollowsSteadyRiding()
	{
		var director = new CameraDirector();
		var aim = default( CameraAim );
		for ( var frame = 0; frame < 240; frame++ )
		{
			var board = new Vector3( frame * Frame * 6f, 0f, 0.1f );
			aim = director.Update( Riding( board, new Vector3( 6f, 0f, 0f ), 0f ), Flat, 0f, 0f, Frame );
		}
		Assert.Equal( 0f, aim.Yaw, 4 );
		// Half the speed is fed forward: the follow trails steady riding by speed / frequency (6 / 12 m).
		Assert.Equal( 239 * Frame * 6f - 0.5f, aim.Base.X, 2 );
		Assert.Equal( 0.1f + Focus, aim.Focus.Z, 3 );
		// The camera position is behind (negative X) and above the focus.
		var camera = aim.At( aim.Elevation, aim.Distance );
		Assert.True( camera.X < aim.Focus.X - 2f && camera.Z > aim.Focus.Z );
	}

	[Fact]
	public void TurnsSmoothlyBehindACarve()
	{
		var director = new CameraDirector();
		var previous = 0f;
		var previousRate = 0f;
		var largestChange = 0f;
		var lagged = 0f;
		for ( var frame = 0; frame <= 300; frame++ )
		{
			var state = Carve( frame * Frame );
			var aim = director.Update( Riding( state.Position, state.Velocity, state.Heading ), Flat, 0f, 0f, Frame );
			var rate = CameraMath.Wrap( aim.Yaw - previous ) / Frame;
			if ( frame > 1 )
				largestChange = MathF.Max( largestChange, MathF.Abs( rate - previousRate ) );
			lagged = MathF.Max( lagged, MathF.Abs( CameraMath.Wrap( state.Heading - aim.Yaw ) ) );
			previous = aim.Yaw;
			previousRate = rate;
		}
		// The view trails the carve (the board turns ahead of it) and comes round behind it again.
		Assert.InRange( lagged, 20f * Degree, 70f * Degree );
		Assert.InRange( previous, 87f * Degree, 93f * Degree );
		// Its turn rate builds up and dies away instead of jumping (a copied heading would jump 60 deg/s at once).
		Assert.True( largestChange < 6f * Degree, $"turn rate jumped {largestChange / Degree} deg/s in a frame" );
	}

	[Fact]
	public void PowerslidesAndFakieNeverSwingTheView()
	{
		var director = new CameraDirector();
		var largest = 0f;
		for ( var frame = 0; frame < 240; frame++ )
		{
			var board = new Vector3( frame * Frame * 5f, 0f, 0.1f );
			// The board turns across its travel and on round to backwards while it keeps sliding the same way.
			var facing = MathF.Min( frame / 120f, 1f ) * MathF.PI;
			var aim = director.Update( Riding( board, new Vector3( 5f, 0f, 0f ), facing ), Flat, 0f, 0f, Frame );
			largest = MathF.Max( largest, MathF.Abs( aim.Yaw ) );
		}
		Assert.True( largest < 0.5f * Degree, $"view turned {largest / Degree} deg" );
	}

	[Fact]
	public void AnImpactEasesTheFollowInsteadOfJolting()
	{
		var director = new CameraDirector();
		var board = new Vector3( 0f, 0f, 0.1f );
		var previous = Vector3.Zero;
		var previousVelocity = Vector3.Zero;
		var largest = 0f;
		var overshoot = 0f;
		for ( var frame = 0; frame < 180; frame++ )
		{
			var moving = frame < 90;
			if ( moving )
				board.X += 6f * Frame;
			var aim = director.Update( Riding( board, new Vector3( moving ? 6f : 0f, 0f, 0f ), 0f ), Flat, 0f, 0f, Frame );
			var velocity = (aim.Base - previous) / Frame;
			if ( frame > 2 )
				largest = MathF.Max( largest, (velocity - previousVelocity).Length() );
			overshoot = MathF.Max( overshoot, aim.Base.X - board.X );
			previous = aim.Base;
			previousVelocity = velocity;
		}
		// The board stops dead (6 m/s in one frame); the camera sheds speed over many frames and barely runs past.
		Assert.True( largest < 1.2f, $"followed position changed speed by {largest} m/s in a frame" );
		Assert.True( overshoot < 0.01f, $"ran {overshoot} m past the stopped board" );
		Assert.Equal( board.X, previous.X, 2 );
	}

	[Fact]
	public void AnOllieDoesNotBobTheView()
	{
		var director = new CameraDirector();
		var lowest = float.PositiveInfinity;
		var highest = float.NegativeInfinity;
		for ( var frame = 0; frame < 150; frame++ )
		{
			var time = frame * Frame;
			var air = time > 1f && time < 1.6f;
			var lift = air ? 0.5f * MathF.Sin( MathF.PI * (time - 1f) / 0.6f ) : 0f;
			var board = new Vector3( time * 4f, 0f, 0.1f + lift );
			var aim = director.Update( Riding( board, new Vector3( 4f, 0f, 0f ), 0f, air ), Flat, 0f, 0f, Frame );
			if ( time > 0.9f )
			{
				lowest = MathF.Min( lowest, aim.Focus.Z );
				highest = MathF.Max( highest, aim.Focus.Z );
			}
		}
		Assert.True( highest - lowest < 0.15f, $"focus moved {highest - lowest} m for a 0.5 m ollie" );
	}

	[Fact]
	public void LooksDownIntoADropButNotForAnOllie()
	{
		var distances = new[] { 0.6f, 1.2f, 1.8f, 2.4f, 3f };
		var flat = new CameraGround( distances, new[] { 0f, 0f, 0f, 0f, 0f }, 5, 0f );
		var director = new CameraDirector();
		var resting = 0f;
		var highest = 0f;
		for ( var frame = 0; frame < 150; frame++ )
		{
			var time = frame * Frame;
			var air = time > 1f && time < 1.6f;
			var lift = air ? 0.5f * MathF.Sin( MathF.PI * (time - 1f) / 0.6f ) : 0f;
			var aim = director.Update( Riding( new Vector3( time * 4f, 0f, 0.1f + lift ), new Vector3( 4f, 0f, 0f ), 0f, air ),
				flat, 0f, 0f, Frame );
			if ( frame == 50 )
				resting = aim.Elevation;
			highest = MathF.Max( highest, aim.Elevation );
		}
		Assert.True( highest - resting < 1f * Degree, $"an ollie raised the view {(highest - resting) / Degree} deg" );

		// Rolling along a 2 m ledge whose edge is 1.5 m ahead: the view lifts to look down over it.
		var ledge = new CameraGround( distances, new[] { 2f, 2f, 0f, 0f, 0f }, 5, 2f );
		var edge = new CameraDirector();
		var start = edge.Update( Riding( new Vector3( 0f, 0f, 2.1f ), new Vector3( 4f, 0f, 0f ), 0f ), flat with { Below = 2f, Grounds = new[] { 2f, 2f, 2f, 2f, 2f } }, 0f, 0f, Frame ).Elevation;
		var aimed = default( CameraAim );
		for ( var frame = 0; frame < 90; frame++ )
			aimed = edge.Update( Riding( new Vector3( 0f, 0f, 2.1f ), new Vector3( 4f, 0f, 0f ), 0f ), ledge, 0f, 0f, Frame );
		Assert.True( aimed.Elevation - start > 25f * Degree, $"looked down only {(aimed.Elevation - start) / Degree} deg" );
	}

	[Fact]
	public void ABigAirOffFlatGroundDoesNotLookDown()
	{
		var flat = new CameraGround( new[] { 0.6f, 1.2f, 1.8f, 2.4f, 3f }, new[] { 0f, 0f, 0f, 0f, 0f }, 5, 0f );
		var director = new CameraDirector();
		var resting = 0f;
		var highest = 0f;
		for ( var frame = 0; frame < 200; frame++ )
		{
			var time = frame * Frame;
			// Thrown 2.5 m up at one second, landing on the same flat ground 1.4 s later.
			var flight = time - 1f;
			var lift = flight > 0f && flight < 1.43f ? 7f * flight - 4.9f * flight * flight : 0f;
			var aim = director.Update( Riding( new Vector3( time * 3f, 0f, 0.1f + lift ), new Vector3( 3f, 0f, 0f ), 0f,
				lift > 0f ), flat, 0f, 0f, Frame );
			if ( frame == 55 )
				resting = aim.Elevation;
			highest = MathF.Max( highest, aim.Elevation );
		}
		Assert.True( highest - resting < 1f * Degree, $"the air raised the view {(highest - resting) / Degree} deg" );
	}

	[Fact]
	public void PlacedHighInTheAirItLooksDownAtTheGround()
	{
		var distances = new[] { 0.6f, 1.2f, 1.8f, 2.4f, 3f };
		var floor = new CameraGround( distances, new[] { 0f, 0f, 0f, 0f, 0f }, 5, 0f );
		var director = new CameraDirector();
		var start = 0f;
		var aim = default( CameraAim );
		for ( var frame = 0; frame < 40; frame++ )
		{
			var time = frame * Frame;
			// A board placed 2.5 m up is reported on the ground for its first frame, then falls.
			var board = new Vector3( time * 5f, 0f, 2.5f - 4.9f * time * time );
			aim = director.Update( Riding( board, new Vector3( 5f, 0f, -9.8f * time ), 0f, frame > 0 ), floor, 0f, 0f,
				Frame );
			if ( frame == 0 )
				start = aim.Elevation;
		}
		Assert.True( aim.Elevation - start > 20f * Degree, $"looked down only {(aim.Elevation - start) / Degree} deg" );
	}

	[Fact]
	public void ElevationEasesIntoItsLimitInsteadOfStoppingDead()
	{
		const float knee = CameraDirector.ElevationKnee;
		var highest = CameraDirector.HighestElevation;
		var previous = CameraMath.SoftClamp( highest - 3f * knee, CameraDirector.LowestElevation, highest, knee );
		var previousSlope = 1f;
		for ( var step = 1; step <= 600; step++ )
		{
			var value = highest - 3f * knee + step * 0.01f * knee;
			var clamped = CameraMath.SoftClamp( value, CameraDirector.LowestElevation, highest, knee );
			var slope = (clamped - previous) / (0.01f * knee);
			Assert.True( clamped <= highest && slope >= 0f && MathF.Abs( slope - previousSlope ) < 0.02f,
				$"at {value / Degree} deg: {clamped / Degree} deg, slope {slope} after {previousSlope}" );
			previous = clamped;
			previousSlope = slope;
		}
	}

	[Fact]
	public void PlacingTheBoardElsewhereStartsAgainThere()
	{
		var director = new CameraDirector();
		for ( var frame = 0; frame < 60; frame++ )
			director.Update( Riding( new Vector3( frame * 0.1f, 0f, 0.1f ), new Vector3( 6f, 0f, 0f ), 0f ), Flat, 0.02f, 0f, Frame );
		var aim = director.Update( Riding( new Vector3( 40f, 25f, 2f ), Vector3.Zero, MathF.PI / 2f ), Flat, 0f, 0f, Frame );
		Assert.True( aim.Teleported );
		Assert.Equal( new Vector3( 40f, 25f, 2f + Focus ), aim.Focus );
		Assert.Equal( MathF.PI / 2f, aim.Yaw, 4 );
	}

	[Fact]
	public void PlacingTheBoardAShortWayUpStartsAgainToo()
	{
		var director = new CameraDirector();
		for ( var frame = 0; frame < 30; frame++ )
			director.Update( Riding( new Vector3( 0f, 0f, 0.1f ), Vector3.Zero, 0f ), Flat, 0f, 0f, Frame );
		// Lifted 2.4 m in a frame from rest: further than any motion at its speed, so it was placed.
		var aim = director.Update( Riding( new Vector3( 0f, 0f, 2.5f ), new Vector3( 5f, 0f, 0f ), 0f, true ), Flat, 0f, 0f,
			Frame );
		Assert.True( aim.Teleported );
		Assert.Equal( 2.5f + Focus, aim.Focus.Z, 3 );
		// A long editor hitch while riding fast is still motion, not a placement.
		var fast = new CameraDirector();
		fast.Update( Riding( new Vector3( 0f, 0f, 0.1f ), new Vector3( 10f, 0f, 0f ), 0f ), Flat, 0f, 0f, Frame );
		Assert.False( fast.Update( Riding( new Vector3( 4f, 0f, 0.1f ), new Vector3( 10f, 0f, 0f ), 0f ), Flat, 0f, 0f,
			0.4f ).Teleported );
	}

	[Fact]
	public void OnFootTheViewNeverTurnsByItself()
	{
		var director = new CameraDirector();
		var board = new Vector3( 0f, 0f, 0.1f );
		director.Update( OnFoot( Vector3.Zero, Vector3.Zero, board ), Flat, 0f, 0f, Frame );
		var start = director.Update( OnFoot( Vector3.Zero, Vector3.Zero, board ), Flat, 0.7f, 0f, Frame ).Yaw;
		var rider = Vector3.Zero;
		var largest = 0f;
		// Strafing and backing up relative to the view, for five seconds without touching the mouse.
		for ( var frame = 0; frame < 300; frame++ )
		{
			var walk = frame < 150 ? new Vector3( 0f, 2f, 0f ) : new Vector3( -1.5f, 0f, 0f );
			rider += walk * Frame;
			var aim = director.Update( OnFoot( rider, walk, board ), Flat, 0f, 0f, Frame );
			largest = MathF.Max( largest, MathF.Abs( CameraMath.Wrap( aim.Yaw - start ) ) );
		}
		Assert.True( largest < 1e-3f, $"view turned {largest / Degree} deg on foot" );
	}

	[Fact]
	public void GettingOnKeepsTheViewThenEasesBehindTheBoard()
	{
		var director = new CameraDirector();
		var board = new Vector3( 0.5f, 0f, 0.1f );
		var rider = new Vector3( 0f, 0f, 0f );
		var aim = default( CameraAim );
		director.Update( OnFoot( rider, Vector3.Zero, board ), Flat, 0f, 0f, Frame );
		for ( var frame = 0; frame < 30; frame++ )
			aim = director.Update( OnFoot( rider, Vector3.Zero, board ), Flat, 3f * Degree, 0f, Frame );
		for ( var frame = 0; frame < 120; frame++ )
			aim = director.Update( OnFoot( rider, Vector3.Zero, board ), Flat, 0f, 0f, Frame );
		var before = aim.Yaw;
		// Mounting a board whose nearer end is 75 degrees off the view: the first riding frame looks the same way.
		var mounted = director.Update( Riding( board, Vector3.Zero, 0.3f ), Flat, 0f, 0f, Frame );
		Assert.Equal( before, mounted.Yaw, 3 );
		var previous = mounted.Yaw;
		var largest = 0f;
		for ( var frame = 0; frame < 300; frame++ )
		{
			aim = director.Update( Riding( board, Vector3.Zero, 0.3f ), Flat, 0f, 0f, Frame );
			largest = MathF.Max( largest, MathF.Abs( CameraMath.Wrap( aim.Yaw - previous ) ) );
			previous = aim.Yaw;
		}
		// Behind the nearer end of the board in the end, having turned no more than 4 degrees in any frame.
		Assert.True( MathF.Abs( CameraMath.Wrap( aim.Yaw - 0.3f ) ) < 3f * Degree, $"ended at {aim.Yaw / Degree} deg" );
		Assert.True( largest < 4f * Degree, $"turned {largest / Degree} deg in a frame" );
	}

	[Fact]
	public void ABailHandsTheFollowToTheBodyWithoutAJump()
	{
		var director = new CameraDirector();
		var board = new Vector3( 0f, 0f, 0.1f );
		var previous = Vector3.Zero;
		var largest = 0f;
		for ( var frame = 0; frame < 240; frame++ )
		{
			var time = frame * Frame;
			board.X = time * 5f;
			CameraSubject subject;
			if ( time < 1f )
				subject = Riding( board, new Vector3( 5f, 0f, 0f ), 0f );
			else
			{
				// The ragdoll tumbles on, bouncing about, while the board shoots off sideways.
				var body = new Vector3( 5f + (time - 1f) * 3f, 0.3f * MathF.Sin( time * 20f ), 0.5f + 0.2f * MathF.Sin( time * 17f ) );
				subject = new( CameraSubjectMode.Bailed, board + new Vector3( 0f, (time - 1f) * 4f, 0f ), body, Vector3.Zero, 0f,
					false, false, false, Focus );
			}
			var aim = director.Update( subject, Flat, 0f, 0f, Frame );
			if ( frame > 0 )
				largest = MathF.Max( largest, Vector3.Distance( aim.Focus, previous ) );
			previous = aim.Focus;
		}
		// Riding at 5 m/s moves the focus about 0.083 m a frame; the hand-over never moves it much more than that.
		Assert.True( largest < 0.11f, $"focus moved {largest} m in one frame" );
	}
}
