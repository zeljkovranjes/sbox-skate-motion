using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class OneShotClipTests
{
	private const float Step = 1f / 60f;

	[Fact]
	public void AOneShotHeldPastItsEndHoldsItsLastPose()
	{
		// A state outlasting its one-shot clip (a crouch held longer than its anticipation) shows the clip's end, not its
		// start again.
		var player = Player();
		var pose = new SkeletonPose( 1 );
		var facts = new StateFacts();
		var parameters = new AnimationParameters();
		var previous = 0f;
		for ( var frame = 0; frame < 60; frame++ )
		{
			player.Update( facts, parameters, Step, pose );
			Assert.True( pose.Positions[ 0 ].X >= previous - 1e-4f, $"went back round at frame {frame}" );
			previous = pose.Positions[ 0 ].X;
		}
		Assert.Equal( 6f, pose.Positions[ 0 ].X, 3 );
	}

	[Fact]
	public void AOneShotHeldPastItsEndHandsOverFromItsEnd()
	{
		// Left long after its end into a state keeping its phase, the next clip starts from its start: the one-shot has run
		// its pass rather than being part way round another.
		var player = Player();
		var pose = new SkeletonPose( 1 );
		var facts = new StateFacts();
		var parameters = new AnimationParameters();
		for ( var frame = 0; frame < 33; frame++ )
			player.Update( facts, parameters, Step, pose );
		facts.Intents[ "go" ] = 1f;
		player.Update( facts, parameters, Step, pose );
		Assert.EndsWith( "Cycle", player.Graph.State );
		Assert.True( player.Time < Step, $"{player.Time:F3} s into the cycle" );
	}

	// A one-shot moving its one bone 6 along x over 0.2 s, played by a state that lasts until "go", then a 1 s cycle that
	// keeps the phase it is handed.
	private static StateGraphPlayer Player()
	{
		var skeleton = new ClipSkeleton( [ "root" ], [ -1 ], [ Vector3.Zero ], [ Quaternion.Identity ] );
		var once = new AnimationClip( "once", 30f, 7, [ 0 ], [ 0 ], [], [ 0 ], [ 7 ],
			[ Vector3.Zero, Vector3.UnitX, Vector3.UnitX * 2f, Vector3.UnitX * 3f, Vector3.UnitX * 4f, Vector3.UnitX * 5f,
				Vector3.UnitX * 6f ] ) { Looping = false };
		var cycle = new AnimationClip( "cycle", 30f, 31, [ 0 ], [ 0 ], [], [ 0 ], [ 1 ], [ Vector3.UnitX * 6f ] );
		var definition = new StateGraphDefinition
		{
			Name = "Root",
			States =
			{
				new()
				{
					Name = "Once", Play = new() { Clip = "once", BlendTime = 0f },
					Transitions = { new() { Target = "Cycle", Guard = new() { Test = "intent", Intent = "go" } } }
				},
				new() { Name = "Cycle", Active = false, Play = new() { Clip = "cycle", BlendTime = .2f, KeepPhase = true } }
			}
		};
		return new StateGraphPlayer( new ClipSet( skeleton, [ once, cycle ] ), definition );
	}
}
