using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class BalanceLayerTests
{
	// A rider on its board: +X forward, +Y left, +Z up, metres.
	private static readonly (string Name, int Parent, Vector3 Position)[] Bones =
	{
		("root", -1, Vector3.Zero),
		("hips", 0, new Vector3( 0f, 0f, 1f )),
		("spine", 1, new Vector3( 0f, 0f, 0.1f )),
		("spine1", 2, new Vector3( 0f, 0f, 0.1f )),
		("spine2", 3, new Vector3( 0f, 0f, 0.1f )),
		("spine3", 4, new Vector3( 0f, 0f, 0.1f )),
		("neck", 5, new Vector3( 0f, 0f, 0.15f )),
		("head", 6, new Vector3( 0f, 0f, 0.1f )),
		("left_shoulder", 5, new Vector3( 0f, 0.03f, 0.12f )),
		("left_arm", 8, new Vector3( 0f, 0.15f, 0f )),
		("left_forearm", 9, new Vector3( 0f, 0f, -0.28f )),
		("left_hand", 10, new Vector3( 0f, 0f, -0.26f )),
		("right_shoulder", 5, new Vector3( 0f, -0.03f, 0.12f )),
		("right_arm", 12, new Vector3( 0f, -0.15f, 0f )),
		("right_forearm", 13, new Vector3( 0f, 0f, -0.28f )),
		("right_hand", 14, new Vector3( 0f, 0f, -0.26f )),
		("left_upleg", 1, new Vector3( 0f, 0.1f, 0f )),
		("left_leg", 16, new Vector3( 0f, 0f, -0.45f )),
		("left_foot", 17, new Vector3( 0f, 0f, -0.45f )),
		("right_upleg", 1, new Vector3( 0f, -0.1f, 0f )),
		("right_leg", 19, new Vector3( 0f, 0f, -0.45f )),
		("right_foot", 20, new Vector3( 0f, 0f, -0.45f )),
		("board", 0, new Vector3( 0f, 0f, 0.08f ))
	};

	[Fact]
	public void NothingMovesWithoutASway()
	{
		var (skeleton, pose) = Rider();
		var before = pose.Rotations.ToArray();
		var layer = new BalanceLayer( skeleton );
		layer.Apply( pose, 0f, 0f );
		layer.Apply( pose, float.NaN, 0.5f );
		Assert.Equal( before, pose.Rotations );
	}

	[Fact]
	public void TheChestLeansAndTheArmsSwingAgainstIt()
	{
		// Forward: the chest goes forward, the hands back.
		var (forwardChest, forwardHand) = Moved( 1f, 0f );
		Assert.True( forwardChest.X > 0.003f, $"chest {forwardChest}" );
		Assert.True( forwardHand.X < forwardChest.X, $"hand {forwardHand}" );
		// To the left: the chest goes left, the hands right of it.
		var (leftChest, leftHand) = Moved( 0f, 1f );
		Assert.True( leftChest.Y > 0.002f, $"chest {leftChest}" );
		Assert.True( leftHand.Y < leftChest.Y, $"hand {leftHand}" );

		static (Vector3 Chest, Vector3 Hand) Moved( float pitch, float roll )
		{
			var (skeleton, pose) = Rider();
			var (rest, _) = World( skeleton, pose );
			new BalanceLayer( skeleton ).Apply( pose, pitch, roll );
			var (moved, _) = World( skeleton, pose );
			return (moved[ Index( "spine3" ) ] - rest[ Index( "spine3" ) ], moved[ Index( "left_hand" ) ] - rest[ Index( "left_hand" ) ]);
		}
	}

	[Fact]
	public void TheHeadHoldsSteadierThanTheChest()
	{
		var (skeleton, pose) = Rider();
		var (_, rest) = World( skeleton, pose );
		new BalanceLayer( skeleton ).Apply( pose, 1f, -1f );
		var (_, moved) = World( skeleton, pose );
		var chest = Degrees( moved[ Index( "spine3" ) ], rest[ Index( "spine3" ) ] );
		var head = Degrees( moved[ Index( "head" ) ], rest[ Index( "head" ) ] );
		Assert.InRange( chest, 1.5f, 3f );
		Assert.True( head < chest * 0.5f, $"head {head} chest {chest}" );
	}

	[Fact]
	public void NothingAtOrBelowTheHipsMoves()
	{
		var (skeleton, pose) = Rider();
		var (restPositions, restRotations) = World( skeleton, pose );
		new BalanceLayer( skeleton ).Apply( pose, -1f, 1f );
		var (positions, rotations) = World( skeleton, pose );
		foreach ( var name in new[] { "root", "hips", "left_upleg", "left_leg", "left_foot", "right_upleg", "right_leg", "right_foot", "board" } )
		{
			Assert.True( Vector3.Distance( positions[ Index( name ) ], restPositions[ Index( name ) ] ) < 1e-6f, name );
			Assert.True( Degrees( rotations[ Index( name ) ], restRotations[ Index( name ) ] ) < 1e-3f, name );
		}
	}

	[Fact]
	public void TheSwayFollowsTheBodyWhicheverWayItFaces()
	{
		var (skeleton, pose) = Rider();
		var (rest, _) = World( skeleton, pose );
		new BalanceLayer( skeleton ).Apply( pose, 0.7f, -0.4f );
		var chest = World( skeleton, pose ).Positions[ Index( "spine3" ) ] - rest[ Index( "spine3" ) ];

		var turn = Quaternion.CreateFromYawPitchRoll( 1.1f, 0.2f, -0.6f );
		var (turnedSkeleton, turnedPose) = Rider();
		turnedPose.Rotations[ 0 ] = turn;
		var (turnedRest, _) = World( turnedSkeleton, turnedPose );
		new BalanceLayer( turnedSkeleton ).Apply( turnedPose, 0.7f, -0.4f );
		var turnedChest = World( turnedSkeleton, turnedPose ).Positions[ Index( "spine3" ) ] - turnedRest[ Index( "spine3" ) ];
		Assert.True( Vector3.Distance( turnedChest, Vector3.Transform( chest, turn ) ) < 1e-5f );
	}

	[Fact]
	public void SwayingAllocatesNothing()
	{
		var (skeleton, pose) = Rider();
		var layer = new BalanceLayer( skeleton );
		void Step( int i ) => layer.Apply( pose, (i % 7) * 0.1f - 0.3f, (i % 5) * 0.1f - 0.2f );
		// Warmed up on the same steps first, as the other allocation tests are, so nothing is counted but the sway itself.
		for ( var i = 0; i < 4096; i++ )
			Step( i );
		var before = GC.GetAllocatedBytesForCurrentThread();
		for ( var i = 0; i < 2000; i++ )
			Step( i );
		Assert.Equal( 0, GC.GetAllocatedBytesForCurrentThread() - before );
	}

	private static int Index( string name ) => Array.FindIndex( Bones, bone => bone.Name == name );

	private static (ClipSkeleton Skeleton, SkeletonPose Pose) Rider()
	{
		var skeleton = new ClipSkeleton( Bones.Select( bone => bone.Name ).ToArray(), Bones.Select( bone => bone.Parent ).ToArray(),
			Bones.Select( bone => bone.Position ).ToArray(), Bones.Select( _ => Quaternion.Identity ).ToArray() );
		var pose = new SkeletonPose( skeleton.Count );
		pose.SetRest( skeleton );
		return (skeleton, pose);
	}

	private static (Vector3[] Positions, Quaternion[] Rotations) World( ClipSkeleton skeleton, SkeletonPose pose )
	{
		var positions = new Vector3[ skeleton.Count ];
		var rotations = new Quaternion[ skeleton.Count ];
		for ( var bone = 0; bone < skeleton.Count; bone++ )
		{
			var parent = skeleton.Parents[ bone ];
			var local = Quaternion.Normalize( pose.Rotations[ bone ] );
			if ( parent < 0 )
			{
				positions[ bone ] = pose.Positions[ bone ];
				rotations[ bone ] = local;
				continue;
			}
			rotations[ bone ] = Quaternion.Normalize( rotations[ parent ] * local );
			positions[ bone ] = positions[ parent ] + Vector3.Transform( pose.Positions[ bone ], rotations[ parent ] );
		}
		return (positions, rotations);
	}

	private static float Degrees( Quaternion a, Quaternion b ) =>
		2f * MathF.Acos( Math.Min( 1f, MathF.Abs( Quaternion.Dot( Quaternion.Normalize( a ), Quaternion.Normalize( b ) ) ) ) ) * 180f / MathF.PI;
}
