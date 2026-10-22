using System;
using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;
using Xunit.Abstractions;

namespace SboxSkateMotion.Tests;

// A root followed in a frame that turns round under it (the rider's model turning with the stance): carried into the new
// frame it is followed on from where it was shown; left in the old one it was eased back to where that frame had it.
public class PoseInertializerReframeTests( ITestOutputHelper output )
{
	private const float Step = 1f / 60f;

	// The frame turns half round about up and moves 15 cm along and 5 cm across, as a stance turning round does.
	private static readonly Quaternion Turn = Quaternion.CreateFromAxisAngle( Vector3.UnitZ, MathF.PI );
	private static readonly Vector3 Shift = new( 0.15f, 0.05f, 0f );

	[Fact]
	public void ARootCarriedIntoATurnedFrameIsFollowedOnWithoutAJump()
	{
		var smoother = new PoseInertializer();
		var pose = new SkeletonPose( 1 );
		var world = new Vector3[ 120 ];
		for ( var frame = 0; frame < world.Length; frame++ )
		{
			var time = frame * Step;
			// Moving and turning steadily in the world; from frame 60 given in the turned frame.
			var place = new Vector3( 0.4f * time, 0.1f * time, 0f );
			var turnNow = Quaternion.CreateFromAxisAngle( Vector3.UnitZ, 1.5f * time );
			var turned = frame >= 60;
			if ( frame == 60 )
				smoother.Reframe( [ 0 ], Turn, Shift );
			pose.Positions[ 0 ] = turned ? Vector3.Transform( place, Turn ) + Shift : place;
			pose.Rotations[ 0 ] = turned ? Turn * turnNow : turnNow;
			var given = pose.Positions[ 0 ];
			var givenTurn = pose.Rotations[ 0 ];
			smoother.Apply( pose, Step );
			Assert.True( Vector3.Distance( pose.Positions[ 0 ], given ) < 1e-4f, $"frame {frame} shown {pose.Positions[ 0 ]} given {given}" );
			Assert.True( Angle( pose.Rotations[ 0 ], givenTurn ) < 1e-3f, $"frame {frame} turned off the given turn" );
			world[ frame ] = turned ? Vector3.Transform( pose.Positions[ 0 ] - Shift, Quaternion.Conjugate( Turn ) ) : pose.Positions[ 0 ];
		}
		var worst = 0f;
		for ( var frame = 2; frame < world.Length; frame++ )
			worst = MathF.Max( worst, ((world[ frame ] - world[ frame - 1 ]) - (world[ frame - 1 ] - world[ frame - 2 ])).Length() / (Step * Step) );
		output.WriteLine( $"largest change of speed in the world in a frame: {worst:F3} m/s^2" );
		Assert.True( worst < 0.01f );
	}

	[Fact]
	public void LeftInTheOldFrameTheRootIsPulledBackAcrossTheTurn()
	{
		// The fault Reframe answers: the frame's change is taken for a move of the pose and eased against. The root sits near
		// the turn's centre (a rider's pelvis over the deck), so it moves under the jump limit and is eased, not started again.
		var smoother = new PoseInertializer();
		var pose = new SkeletonPose( 1 );
		var off = 0f;
		for ( var frame = 0; frame < 70; frame++ )
		{
			var time = frame * Step;
			var place = new Vector3( 0.05f * time, 0f, 0f );
			pose.Positions[ 0 ] = frame >= 60 ? Vector3.Transform( place, Turn ) + Shift : place;
			pose.Rotations[ 0 ] = frame >= 60 ? Turn : Quaternion.Identity;
			var given = pose.Positions[ 0 ];
			smoother.Apply( pose, Step );
			if ( frame >= 60 )
				off = MathF.Max( off, Vector3.Distance( pose.Positions[ 0 ], given ) );
		}
		output.WriteLine( $"shown off the given place after the turn: up to {off * 100f:F1} cm" );
		Assert.True( off > 0.02f );
	}

	[Fact]
	public void AnOffsetStillEasingOutIsCarriedAcross()
	{
		var smoother = new PoseInertializer();
		var pose = new SkeletonPose( 1 );
		var world = new Vector3[ 100 ];
		for ( var frame = 0; frame < world.Length; frame++ )
		{
			var time = frame * Step;
			// A 10 cm jump at frame 40 (eased over), and the frame turning round at frame 44 while it still eases out.
			var place = new Vector3( 0.3f * time + (frame >= 40 ? 0.1f : 0f), 0f, 0f );
			var turned = frame >= 44;
			if ( frame == 44 )
				smoother.Reframe( [ 0 ], Turn, Shift );
			pose.Positions[ 0 ] = turned ? Vector3.Transform( place, Turn ) + Shift : place;
			pose.Rotations[ 0 ] = turned ? Turn : Quaternion.Identity;
			smoother.Apply( pose, Step );
			world[ frame ] = turned ? Vector3.Transform( pose.Positions[ 0 ] - Shift, Quaternion.Conjugate( Turn ) ) : pose.Positions[ 0 ];
		}
		var worst = 0f;
		for ( var frame = 2; frame < world.Length; frame++ )
			worst = MathF.Max( worst, ((world[ frame ] - world[ frame - 1 ]) - (world[ frame - 1 ] - world[ frame - 2 ])).Length() / Step );
		output.WriteLine( $"largest change of speed in the world in a frame: {worst:F3} m/s" );
		// The jump's easing never changes the speed by more than the budget allows in a frame, turn or no turn.
		Assert.True( worst <= PoseInertializer.MoveBudget * Step + 0.01f );
		Assert.True( MathF.Abs( world[ ^1 ].X - (0.3f * (world.Length - 1) * Step + 0.1f) ) < 0.002f );
	}

	private static float Angle( Quaternion a, Quaternion b ) =>
		2f * MathF.Acos( Math.Min( 1f, MathF.Abs( Quaternion.Dot( Quaternion.Normalize( a ), Quaternion.Normalize( b ) ) ) ) );
}
