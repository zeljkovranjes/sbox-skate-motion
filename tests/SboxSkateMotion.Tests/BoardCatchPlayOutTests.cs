using System.Collections.Generic;
using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class BoardCatchPlayOutTests
{
	private const float Step = 1f / 120f;

	[Fact]
	public void ABoardLeftJustBeforeItsCatchIsStillCaught()
	{
		// A flip clip turns its board a whole turn over half a second and is left 0.05 s before its end, 36 degrees short of
		// the catch: the board plays the rest of the flip out under the change and is caught on time.
		var player = Player();
		player.Board = 0;
		var shown = Run( player, out var change );
		var caught = shown[ change + 6 ];
		Assert.True( Angle( caught, Quaternion.Identity ) < Radians( 10f ), $"{Degrees( caught ):F0} deg short 0.05 s after the change" );
		for ( var frame = change; frame < change + 6; frame++ )
			Assert.True( Angle( shown[ frame + 1 ], Quaternion.Identity ) < Angle( shown[ frame ], Quaternion.Identity ) + 1e-4f,
				$"frame {frame - change} turned back" );
		Assert.True( Angle( shown[ ^1 ], Quaternion.Identity ) < 0.01f, "the fade ends on the new clip" );
	}

	[Fact]
	public void WithoutABoardTheChangeEasesFromWhereTheClipWasLeft()
	{
		// Any other bone takes the new clip from where it was left, the offset easing out over the fade, and carries the
		// speed it was turning at (6 degrees a frame here) rather than stopping dead at the change; only the board plays its
		// last clip out to the catch. (Before the speed was carried the bone stopped, and 0.05 s on was still over 20 degrees
		// from the new clip.)
		var shown = Run( Player(), out var change );
		var turned = Degrees( shown[ change ] ) - Degrees( shown[ change + 1 ] );
		Assert.True( turned > 3f, $"{turned:F1} deg turned the frame after the change" );
	}

	// Plays the flip until the graph leaves it and on through the fade; the bone's rotation each frame, and the frame of the
	// change (the first frame of the new state).
	private static List<Quaternion> Run( StateGraphPlayer player, out int change )
	{
		var pose = new SkeletonPose( 1 );
		var parameters = new AnimationParameters();
		var facts = new StateFacts();
		var shown = new List<Quaternion>();
		change = -1;
		for ( var frame = 0; frame < 120; frame++ )
		{
			player.Update( facts, parameters, Step, pose );
			shown.Add( pose.Rotations[ 0 ] );
			if ( change < 0 && player.Graph.State == "Air" )
				change = frame;
		}
		Assert.True( change > 0, "the flip was left" );
		return shown;
	}

	// The flip turns its board about its length at 720 degrees a second, a whole turn in 0.5 s, then the board is held
	// flat; the flip is left 0.05 s before its end.
	private static StateGraphPlayer Player()
	{
		var skeleton = new ClipSkeleton( [ "board" ], [ -1 ], [ Vector3.Zero ], [ Quaternion.Identity ] );
		const int frames = 16;
		var turns = new Quaternion[ frames ];
		for ( var frame = 0; frame < frames; frame++ )
			turns[ frame ] = Quaternion.CreateFromAxisAngle( Vector3.UnitX, Radians( 360f * frame / (frames - 1) ) );
		var flip = new AnimationClip( "flip", (frames - 1) / 0.5f, frames, [ 0 ], [ frames ], turns, [ 0 ], [ 1 ], [ Vector3.Zero ] );
		var flat = new AnimationClip( "flat", 30f, 31, [ 0 ], [ 1 ], [ Quaternion.Identity ], [ 0 ], [ 1 ], [ Vector3.Zero ] );
		var clips = new ClipSet( skeleton, [ flip, flat ] );
		var start = new StateGraphDefinition { Name = "Flip", Play = new StatePlayDefinition { Clip = "flip", BlendTime = 0f } };
		start.Transitions.Add( new StateTransitionDefinition
		{
			Target = "Air",
			Guard = new StateTestDefinition { Test = "clip-ending", Within = 0.05f, Mask = 1 }
		} );
		var air = new StateGraphDefinition { Name = "Air", Active = false, Play = new StatePlayDefinition { Clip = "flat", BlendTime = 0.2f } };
		return new StateGraphPlayer( clips, new StateGraphDefinition { Name = "Root", States = [ start, air ] } );
	}

	private static float Angle( Quaternion a, Quaternion b ) =>
		2f * MathF.Acos( Math.Min( 1f, MathF.Abs( Quaternion.Dot( Quaternion.Normalize( a ), Quaternion.Normalize( b ) ) ) ) );

	private static float Degrees( Quaternion rotation ) => Angle( rotation, Quaternion.Identity ) * 180f / MathF.PI;

	private static float Radians( float degrees ) => degrees * MathF.PI / 180f;
}
