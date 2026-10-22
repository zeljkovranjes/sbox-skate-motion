using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class BoardEndForEndTests
{
	[Fact]
	public void ABoardSpunHalfRoundIsNotSpunBack()
	{
		// A shove-it's clip ends with its board 172 degrees round and the next clip has it ahead: the board is the same end
		// for end, so it is shown going on the last 8 degrees to its other end, never back across the spin.
		var player = Player( 172f, 0.2f );
		player.Board = 0;
		var shown = Change( player );
		var farthest = shown.Max( rotation => EndForEnd( rotation, Quaternion.Identity ) );
		Assert.True( farthest < Radians( 9f ), $"went {farthest * 180f / MathF.PI:F1} deg from the new clip's board, end for end" );
		for ( var frame = 1; frame < shown.Count; frame++ )
			Assert.True( EndForEnd( shown[ frame ], shown[ frame - 1 ] ) < Radians( 1f ), $"frame {frame} turned on the spot" );
		Assert.True( EndForEnd( shown[ ^1 ], Quaternion.Identity ) < 0.01f, "the fade ends on the new clip's board" );
	}

	[Fact]
	public void ABoardHeldAcrossGoesBackTheWayItCame()
	{
		// A board held 100 degrees across the rider (a shifty) is nowhere near its other end: it eases back as before.
		var player = Player( 100f, 0.2f );
		player.Board = 0;
		var shown = Change( player );
		Assert.True( Degrees( shown[ 0 ] ) > 95f, $"first frame {Degrees( shown[ 0 ] ):F0} deg round" );
		for ( var frame = 1; frame < shown.Count; frame++ )
			Assert.True( Degrees( shown[ frame ] ) <= Degrees( shown[ frame - 1 ] ) + 0.01f, $"frame {frame} turned away" );
		Assert.True( Degrees( shown[ ^1 ] ) < 0.6f );
	}

	[Fact]
	public void OtherBonesStillTurnBackTheShorterWay()
	{
		// Only the board is the same end for end: any other bone turned 172 degrees turns back through the fade.
		var shown = Change( Player( 172f, 0.2f ) );
		Assert.True( Degrees( shown[ 0 ] ) > 160f, $"first frame {Degrees( shown[ 0 ] ):F0} deg round" );
		Assert.True( Degrees( shown[ ^1 ] ) < 0.6f );
	}

	// Plays the turned clip, changes to the one ahead, and returns the bone's rotation each frame from the change on.
	private static List<Quaternion> Change( StateGraphPlayer player )
	{
		var pose = new SkeletonPose( 1 );
		var parameters = new AnimationParameters();
		var facts = new StateFacts();
		const float step = 1f / 120f;
		for ( var frame = 0; frame < 6; frame++ )
			player.Update( facts, parameters, step, pose );
		facts.Intents[ "go" ] = 1f;
		var shown = new List<Quaternion>();
		for ( var frame = 0; frame < 121; frame++ )
		{
			player.Update( facts, parameters, step, pose );
			shown.Add( pose.Rotations[ 0 ] );
		}
		return shown;
	}

	private static StateGraphPlayer Player( float degrees, float blend )
	{
		var skeleton = new ClipSkeleton( [ "board" ], [ -1 ], [ Vector3.Zero ], [ Quaternion.Identity ] );
		var turned = Quaternion.CreateFromAxisAngle( Vector3.UnitZ, Radians( degrees ) );
		var clips = new ClipSet( skeleton, [ Held( "turned", turned ), Held( "ahead", Quaternion.Identity ) ] );
		var start = new StateGraphDefinition { Name = "Turned", Play = new StatePlayDefinition { Clip = "turned", BlendTime = 0f } };
		start.Transitions.Add( new StateTransitionDefinition
		{
			Target = "Ahead",
			Guard = new StateTestDefinition { Test = "intent", Intent = "go", Mask = 1 }
		} );
		var ahead = new StateGraphDefinition { Name = "Ahead", Active = false, Play = new StatePlayDefinition { Clip = "ahead", BlendTime = blend } };
		return new StateGraphPlayer( clips, new StateGraphDefinition { Name = "Root", States = [ start, ahead ] } );
	}

	// A second-long clip holding one rotation.
	private static AnimationClip Held( string name, Quaternion rotation ) =>
		new( name, 30f, 31, [ 0 ], [ 1 ], [ rotation ], [ 0 ], [ 1 ], [ Vector3.Zero ] );

	private static float Angle( Quaternion a, Quaternion b ) =>
		2f * MathF.Acos( Math.Min( 1f, MathF.Abs( Quaternion.Dot( Quaternion.Normalize( a ), Quaternion.Normalize( b ) ) ) ) );

	// How far a board is from another, either end for end.
	private static float EndForEnd( Quaternion board, Quaternion other ) =>
		MathF.Min( Angle( board, other ), Angle( board * new Quaternion( 0f, 0f, 1f, 0f ), other ) );

	private static float Degrees( Quaternion rotation ) => Angle( rotation, Quaternion.Identity ) * 180f / MathF.PI;

	private static float Radians( float degrees ) => degrees * MathF.PI / 180f;
}
