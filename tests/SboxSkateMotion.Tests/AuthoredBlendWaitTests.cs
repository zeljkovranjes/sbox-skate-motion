using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class AuthoredBlendWaitTests
{
	private const float Step = 1f / 60f;

	[Fact]
	public void AShortClipEnteredFarAwayLeavesAsItEnds()
	{
		// The change into the short clip is a 170 degree turn, so its fade lasts longer than the clip (0.35 s against 0.2 s).
		// Its way out waits for the blend in as authored (0.1 s), so the state leaves as the clip ends, not once the fade
		// has run out with the clip held still at its end.
		var player = Player();
		var pose = new SkeletonPose( 1 );
		var facts = new StateFacts();
		var parameters = new AnimationParameters();
		for ( var frame = 0; frame < 10; frame++ )
			player.Update( facts, parameters, Step, pose );
		facts.Intents[ "go" ] = 1f;
		var left = -1f;
		for ( var frame = 0; frame < 40 && left < 0f; frame++ )
		{
			player.Update( facts, parameters, Step, pose );
			if ( player.Graph.State.EndsWith( "After" ) )
				left = frame * Step;
		}
		Assert.InRange( left, 0.19f, 0.24f );
	}

	[Fact]
	public void AShortClipStillWaitsForItsAuthoredBlend()
	{
		// A clip shorter than its state's own blend in still plays the blend out before leaving.
		var player = Player( shortBlend: 0.3f );
		var pose = new SkeletonPose( 1 );
		var facts = new StateFacts();
		var parameters = new AnimationParameters();
		for ( var frame = 0; frame < 10; frame++ )
			player.Update( facts, parameters, Step, pose );
		facts.Intents[ "go" ] = 1f;
		var left = -1f;
		for ( var frame = 0; frame < 40 && left < 0f; frame++ )
		{
			player.Update( facts, parameters, Step, pose );
			if ( player.Graph.State.EndsWith( "After" ) )
				left = frame * Step;
		}
		Assert.True( left >= 0.29f, $"left after {left:F3} s" );
	}

	// A still clip, then on "go" a 0.2 s one-shot turned 170 degrees from it, leaving as it ends for a still clip again.
	private static StateGraphPlayer Player( float shortBlend = 0.1f )
	{
		var skeleton = new ClipSkeleton( [ "root" ], [ -1 ], [ Vector3.Zero ], [ Quaternion.Identity ] );
		var still = new AnimationClip( "still", 30f, 31, [ 0 ], [ 1 ], [ Quaternion.Identity ], [ 0 ], [ 1 ], [ Vector3.Zero ] );
		var turned = Quaternion.CreateFromAxisAngle( Vector3.UnitZ, 170f * MathF.PI / 180f );
		var once = new AnimationClip( "once", 30f, 7, [ 0 ], [ 1 ], [ turned ], [ 0 ], [ 1 ], [ Vector3.Zero ] ) { Looping = false };
		var definition = new StateGraphDefinition
		{
			Name = "Root",
			States =
			{
				new()
				{
					Name = "Start", Play = new() { Clip = "still", BlendTime = 0f },
					Transitions = { new() { Target = "Short", Guard = new() { Test = "intent", Intent = "go" } } }
				},
				new()
				{
					Name = "Short", Active = false, Play = new() { Clip = "once", BlendTime = shortBlend },
					Transitions = { new() { Target = "After", Guard = new() { Test = "clip-ending", Within = 0.01f } } }
				},
				new() { Name = "After", Active = false, Play = new() { Clip = "still", BlendTime = 0.1f } }
			}
		};
		return new StateGraphPlayer( new ClipSet( skeleton, [ still, once ] ), definition );
	}
}
