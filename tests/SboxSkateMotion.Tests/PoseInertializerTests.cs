using System;
using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;
using Xunit.Abstractions;

namespace SboxSkateMotion.Tests;

public class PoseInertializerTests( ITestOutputHelper output )
{
	private const float Step = 1f / 90f;

	[Fact]
	public void SteadyMotionIsFollowedExactly()
	{
		var smoother = new PoseInertializer();
		var pose = new SkeletonPose( 2 );
		for ( var frame = 0; frame < 120; frame++ )
		{
			var time = frame * Step;
			Given( pose, 0, new Vector3( 0.4f * time, 0f, 0f ), Quaternion.CreateFromAxisAngle( Vector3.UnitZ, 3f * time ) );
			smoother.Apply( pose, Step );
			Assert.True( Vector3.Distance( pose.Positions[ 0 ], new Vector3( 0.4f * time, 0f, 0f ) ) < 1e-4f );
			Assert.True( Angle( pose.Rotations[ 0 ], Quaternion.CreateFromAxisAngle( Vector3.UnitZ, 3f * time ) ) < 1e-3f );
		}
	}

	[Fact]
	public void AJumpOfThePoseIsEasedOverWithoutAJumpInSpeed()
	{
		var smoother = new PoseInertializer();
		var pose = new SkeletonPose( 1 );
		var shown = new float[ 160 ];
		var given = 0f;
		for ( var frame = 0; frame < shown.Length; frame++ )
		{
			// Turning steadily at 2 rad/s, then 40 degrees on in a single frame (a clip change caught in one frame).
			given += 2f * Step + (frame == 60 ? 0.7f : 0f);
			Given( pose, 0, Vector3.Zero, Quaternion.CreateFromAxisAngle( Vector3.UnitZ, given ) );
			smoother.Apply( pose, Step );
			shown[ frame ] = Yaw( pose.Rotations[ 0 ] );
			if ( frame > 100 )
				Assert.True( MathF.Abs( shown[ frame ] - given ) < 0.01f, $"frame {frame} still {shown[ frame ] - given:F3} off" );
		}
		var worst = 0f;
		for ( var frame = 2; frame < shown.Length; frame++ )
			worst = MathF.Max( worst, MathF.Abs( (shown[ frame ] - shown[ frame - 1 ]) - (shown[ frame - 1 ] - shown[ frame - 2 ]) ) / Step );
		output.WriteLine( $"largest change of turning speed in a frame {worst:F2} rad/s" );
		// Without easing the 0.7 rad step changes the turning speed by 63 rad/s in a frame and back; eased, no frame changes
		// it by more than the budget allows.
		Assert.True( worst <= PoseInertializer.TurnBudget * Step + 0.05f );
	}

	[Fact]
	public void ABoneStoppedDeadCarriesOnALittleAndSettlesWithoutCrossingBack()
	{
		var smoother = new PoseInertializer();
		var pose = new SkeletonPose( 1 );
		var given = 0f;
		var stopped = 0f;
		var furthest = 0f;
		for ( var frame = 0; frame < 150; frame++ )
		{
			// A flipping board at 20 rad/s that the next clip holds still.
			if ( frame < 40 )
				given += 20f * Step;
			if ( frame == 40 )
				stopped = given;
			Given( pose, 0, Vector3.Zero, Quaternion.CreateFromAxisAngle( Vector3.UnitX, given ) );
			smoother.Apply( pose, Step );
			var angle = Roll( pose.Rotations[ 0 ] );
			if ( frame >= 40 )
			{
				var past = Wrap( angle - stopped );
				// Never behind the pose it stopped at: no swing back the other way.
				Assert.True( past > -1e-3f, $"frame {frame} {past:F3}" );
				furthest = MathF.Max( furthest, past );
			}
		}
		output.WriteLine( $"carried {furthest * 180f / MathF.PI:F1} degrees past the stop" );
		Assert.InRange( furthest, 0.05f, 0.45f );
		Assert.True( MathF.Abs( Wrap( Roll( pose.Rotations[ 0 ] ) - stopped ) ) < 0.01f );
	}

	[Fact]
	public void ALargeChangeEasesOverNoFasterThanTheFastestTurn()
	{
		var smoother = new PoseInertializer();
		var pose = new SkeletonPose( 1 );
		var last = 0f;
		var fastest = 0f;
		for ( var frame = 0; frame < 200; frame++ )
		{
			// Held, then turned 100 degrees round in one frame (under the jump limit), then held again.
			var given = frame < 30 ? 0f : 1.75f;
			Given( pose, 0, Vector3.Zero, Quaternion.CreateFromAxisAngle( Vector3.UnitZ, given ) );
			smoother.Apply( pose, Step );
			var yaw = Yaw( pose.Rotations[ 0 ] );
			if ( frame > 30 )
				fastest = MathF.Max( fastest, MathF.Abs( yaw - last ) / Step );
			last = yaw;
		}
		output.WriteLine( $"fastest {fastest:F2} rad/s" );
		Assert.True( fastest <= PoseInertializer.FastestTurn * 1.2f );
		Assert.True( MathF.Abs( last - 1.75f ) < 0.01f );
	}

	[Fact]
	public void AMovePastTheJumpLimitIsFollowedAtOnce()
	{
		var smoother = new PoseInertializer();
		var pose = new SkeletonPose( 1 );
		for ( var frame = 0; frame < 20; frame++ )
		{
			// A looping clip's root coming back round from 1 m on to the start.
			var position = frame < 10 ? new Vector3( 0.1f * frame, 0f, 0f ) : new Vector3( 0.1f * (frame - 10), 0f, 0f );
			Given( pose, 0, position, Quaternion.Identity );
			smoother.Apply( pose, Step );
			Assert.True( Vector3.Distance( pose.Positions[ 0 ], position ) < 1e-4f, $"frame {frame}" );
		}
	}

	[Fact]
	public void AHitchStartsAgainRatherThanEasingAcrossIt()
	{
		var smoother = new PoseInertializer();
		var pose = new SkeletonPose( 1 );
		Given( pose, 0, Vector3.Zero, Quaternion.Identity );
		smoother.Apply( pose, Step );
		smoother.Apply( pose, Step );
		var turned = Quaternion.CreateFromAxisAngle( Vector3.UnitY, 1f );
		Given( pose, 0, Vector3.Zero, turned );
		smoother.Apply( pose, 0.5f );
		Assert.True( Angle( pose.Rotations[ 0 ], turned ) < 1e-4f );
	}

	[Fact]
	public void TurnsAboutDifferentAxesComposeWithoutAJump()
	{
		var smoother = new PoseInertializer();
		var pose = new SkeletonPose( 1 );
		var shown = new Quaternion[ 120 ];
		var worst = 0f;
		for ( var frame = 0; frame < shown.Length; frame++ )
		{
			// Held, turned 70 degrees about one axis in a frame, and while that is still easing over, 100 degrees about
			// another in a frame (a grab's arm as the other hand's grab joins it).
			var given = Quaternion.Identity;
			if ( frame >= 20 )
				given = Quaternion.CreateFromAxisAngle( Vector3.UnitZ, 1.2f );
			if ( frame >= 24 )
				given = Quaternion.Normalize( Quaternion.CreateFromAxisAngle( Vector3.UnitX, 1.75f ) * given );
			Given( pose, 0, Vector3.Zero, given );
			smoother.Apply( pose, Step );
			shown[ frame ] = pose.Rotations[ 0 ];
			if ( frame >= 2 )
			{
				var speed = Turn( shown[ frame - 1 ], shown[ frame ] ) / Step;
				var before = Turn( shown[ frame - 2 ], shown[ frame - 1 ] ) / Step;
				worst = MathF.Max( worst, (speed - before).Length() );
			}
		}
		output.WriteLine( $"largest change of turning speed in a frame {worst:F2} rad/s" );
		// Added as rotation vectors, the two offsets composed wrongly and the bone jumped some 30 degrees in the second turn's
		// frame; composed as rotations no frame changes the turning speed by more than the budget allows.
		Assert.True( worst <= PoseInertializer.TurnBudget * Step + 0.05f, $"{worst:F2} rad/s" );
		Assert.True( Angle( shown[ ^1 ], Quaternion.Normalize( Quaternion.CreateFromAxisAngle( Vector3.UnitX, 1.75f )
			* Quaternion.CreateFromAxisAngle( Vector3.UnitZ, 1.2f ) ) ) < 0.01f );
	}

	// The turn taking one rotation onto the next, as a rotation vector the shorter way round.
	private static Vector3 Turn( Quaternion from, Quaternion to )
	{
		var turn = Quaternion.Normalize( to * Quaternion.Conjugate( from ) );
		if ( turn.W < 0f )
			turn = Quaternion.Negate( turn );
		var axis = new Vector3( turn.X, turn.Y, turn.Z );
		var sine = axis.Length();
		return sine < 1e-7f ? axis * 2f : axis * (2f * MathF.Atan2( sine, turn.W ) / sine);
	}

	private static void Given( SkeletonPose pose, int bone, Vector3 position, Quaternion rotation )
	{
		pose.Positions[ bone ] = position;
		pose.Rotations[ bone ] = rotation;
		for ( var other = 0; other < pose.Rotations.Length; other++ )
		{
			if ( other != bone && pose.Rotations[ other ] == default )
				pose.Rotations[ other ] = Quaternion.Identity;
		}
	}

	private static float Angle( Quaternion a, Quaternion b ) =>
		2f * MathF.Acos( Math.Min( 1f, MathF.Abs( Quaternion.Dot( Quaternion.Normalize( a ), Quaternion.Normalize( b ) ) ) ) );

	// Angles about the one axis a test turns about, unwrapped by the caller where it needs to be.
	private static float Yaw( Quaternion q ) => 2f * MathF.Atan2( q.Z, q.W );

	private static float Roll( Quaternion q ) => 2f * MathF.Atan2( q.X, q.W );

	private static float Wrap( float angle )
	{
		while ( angle > MathF.PI )
			angle -= 2f * MathF.PI;
		while ( angle < -MathF.PI )
			angle += 2f * MathF.PI;
		return angle;
	}
}
