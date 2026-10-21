using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class FadeTurnRateTests
{
	[Fact]
	public void AChangeFarFromThePoseShownTakesLongerSoNoBoneWhips()
	{
		// A clip turned nearly half way round hands over to one facing ahead with a short blend: the fade stretches so no
		// frame turns the bone faster than a quick arm swing.
		var turned = Quaternion.CreateFromAxisAngle( Vector3.UnitZ, 170f * MathF.PI / 180f );
		var player = Player( turned, 0.1f );
		var pose = new SkeletonPose( 1 );
		var parameters = new AnimationParameters();
		var facts = new StateFacts();
		const float step = 1f / 120f;
		for ( var frame = 0; frame < 6; frame++ )
			player.Update( facts, parameters, step, pose );
		facts.Intents[ "go" ] = 1f;
		var fastest = 0f;
		var last = pose.Rotations[ 0 ];
		for ( var frame = 0; frame < 120; frame++ )
		{
			player.Update( facts, parameters, step, pose );
			fastest = MathF.Max( fastest, Angle( pose.Rotations[ 0 ], last ) / step );
			last = pose.Rotations[ 0 ];
		}
		Assert.Equal( "Ahead", player.Graph.State );
		Assert.True( Angle( pose.Rotations[ 0 ], Quaternion.Identity ) < 0.01f, "the fade ends on the new clip" );
		Assert.True( fastest < 16.5f, $"turned {fastest * 180f / MathF.PI:F0} deg/s" );
	}

	[Fact]
	public void AChangeCloseToThePoseShownKeepsItsBlendTime()
	{
		// A small change fades in its authored time: 10 degrees over a 0.1 s blend is done within it.
		var player = Player( Quaternion.CreateFromAxisAngle( Vector3.UnitZ, 10f * MathF.PI / 180f ), 0.1f );
		var pose = new SkeletonPose( 1 );
		var parameters = new AnimationParameters();
		var facts = new StateFacts();
		const float step = 1f / 120f;
		for ( var frame = 0; frame < 6; frame++ )
			player.Update( facts, parameters, step, pose );
		facts.Intents[ "go" ] = 1f;
		for ( var frame = 0; frame < 13; frame++ )
			player.Update( facts, parameters, step, pose );
		Assert.True( Angle( pose.Rotations[ 0 ], Quaternion.Identity ) < 0.01f );
	}

	private static StateGraphPlayer Player( Quaternion from, float blend )
	{
		var skeleton = new ClipSkeleton( [ "root" ], [ -1 ], [ Vector3.Zero ], [ Quaternion.Identity ] );
		var clips = new ClipSet( skeleton, [ Held( "turned", from ), Held( "ahead", Quaternion.Identity ) ] );
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
}
