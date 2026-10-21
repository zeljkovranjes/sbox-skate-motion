using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class BreathingLayerTests
{
	// A rider standing on its board, leaning a little forward: +X forward, +Y left, +Z up, metres.
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
	public void NothingMovesWithoutABreath()
	{
		var (skeleton, pose) = Rider();
		var before = pose.Rotations.ToArray();
		var layer = new BreathingLayer( skeleton );
		foreach ( var inhale in new[] { 0f, -1f, float.NaN, float.PositiveInfinity } )
			layer.Apply( pose, inhale );
		Assert.Equal( before, pose.Rotations );
	}

	[Fact]
	public void BreathingInLeansTheChestBackAndLiftsTheShoulders()
	{
		var (skeleton, pose) = Rider();
		var (restPositions, restRotations) = World( skeleton, pose );
		new BreathingLayer( skeleton ).Apply( pose, 1f );
		var (positions, rotations) = World( skeleton, pose );
		var forward = Vector3.UnitX;

		// The chest goes back, and each shoulder rises: about 3 degrees of a 15 cm collarbone, 8 mm.
		var chest = positions[ Index( "spine3" ) ] - restPositions[ Index( "spine3" ) ];
		Assert.True( Vector3.Dot( chest, forward ) < -0.002f, $"chest moved {chest}" );
		foreach ( var arm in new[] { "left_arm", "right_arm" } )
		{
			var shoulder = positions[ Index( arm ) ] - restPositions[ Index( arm ) ];
			Assert.InRange( shoulder.Z, 0.005f, 0.012f );
		}
		// The head stays nearly level.
		Assert.True( Degrees( rotations[ Index( "head" ) ], restRotations[ Index( "head" ) ] ) < 1.5f );
		// Nothing at or below the hips moves: the feet stay where the clip put them on the board.
		foreach ( var name in new[] { "root", "hips", "left_upleg", "left_leg", "left_foot", "right_upleg", "right_leg", "right_foot", "board" } )
		{
			Assert.True( Vector3.Distance( positions[ Index( name ) ], restPositions[ Index( name ) ] ) < 1e-6f, name );
			Assert.True( Degrees( rotations[ Index( name ) ], restRotations[ Index( name ) ] ) < 1e-3f, name );
		}
	}

	[Fact]
	public void TheArmsKeepHangingAsTheClipHeldThem()
	{
		var (skeleton, pose) = Rider();
		pose.Rotations[ Index( "left_arm" ) ] = Quaternion.CreateFromAxisAngle( Vector3.UnitX, -0.4f );
		pose.Rotations[ Index( "right_forearm" ) ] = Quaternion.CreateFromAxisAngle( Vector3.UnitY, 0.6f );
		var (restPositions, restRotations) = World( skeleton, pose );
		new BreathingLayer( skeleton ).Apply( pose, 1.8f );
		var (positions, rotations) = World( skeleton, pose );
		foreach ( var name in new[] { "left_arm", "left_forearm", "left_hand", "right_arm", "right_forearm", "right_hand" } )
			Assert.True( Degrees( rotations[ Index( name ) ], restRotations[ Index( name ) ] ) < 0.01f, name );
		// The hands only follow their shoulders, a couple of centimetres even on a deep breath.
		foreach ( var name in new[] { "left_hand", "right_hand" } )
			Assert.InRange( Vector3.Distance( positions[ Index( name ) ], restPositions[ Index( name ) ] ), 0.002f, 0.035f );
	}

	[Fact]
	public void TheBreathFollowsTheBodyWhicheverWayItFaces()
	{
		var (skeleton, pose) = Rider();
		var (restPositions, _) = World( skeleton, pose );
		new BreathingLayer( skeleton ).Apply( pose, 1f );
		var (positions, _) = World( skeleton, pose );
		var chest = positions[ Index( "spine3" ) ] - restPositions[ Index( "spine3" ) ];
		var head = positions[ Index( "head" ) ] - restPositions[ Index( "head" ) ];

		// The same rider turned to face the other way, and rolled onto a slope: the same breath, turned with it.
		var turn = Quaternion.CreateFromYawPitchRoll( 0f, 0.3f, 2.4f );
		var (turnedSkeleton, turnedPose) = Rider();
		turnedPose.Rotations[ 0 ] = turn;
		var (turnedRest, _) = World( turnedSkeleton, turnedPose );
		new BreathingLayer( turnedSkeleton ).Apply( turnedPose, 1f );
		var (turnedPositions, _) = World( turnedSkeleton, turnedPose );
		var expectedChest = Vector3.Transform( chest, turn );
		var expectedHead = Vector3.Transform( head, turn );
		Assert.True( Vector3.Distance( turnedPositions[ Index( "spine3" ) ] - turnedRest[ Index( "spine3" ) ], expectedChest ) < 1e-5f );
		Assert.True( Vector3.Distance( turnedPositions[ Index( "head" ) ] - turnedRest[ Index( "head" ) ], expectedHead ) < 1e-5f );
	}

	[Fact]
	public void ADeeperBreathMovesTheChestFurther()
	{
		var (skeleton, resting) = Rider();
		var (rest, _) = World( skeleton, resting );
		var layer = new BreathingLayer( skeleton );
		layer.Apply( resting, 1f );
		var (_, deep) = Rider();
		layer.Apply( deep, 1.8f );
		var chest = Index( "spine3" );
		var shallow = Vector3.Distance( World( skeleton, resting ).Positions[ chest ], rest[ chest ] );
		var deeper = Vector3.Distance( World( skeleton, deep ).Positions[ chest ], rest[ chest ] );
		Assert.Equal( 1.8f, deeper / shallow, 1 );
	}

	[Fact]
	public void ASkeletonMissingSomeBonesStillBreathesWithWhatItHas()
	{
		// No collarbones, neck or upper spine: the spine it has still leans and the head still moves.
		var names = new[] { "root", "hips", "spine", "head", "left_upleg", "right_upleg", "left_arm", "right_arm" };
		var parents = new[] { -1, 0, 1, 2, 1, 1, 2, 2 };
		var positions = new[]
		{
			Vector3.Zero, new Vector3( 0f, 0f, 1f ), new Vector3( 0f, 0f, 0.3f ), new Vector3( 0f, 0f, 0.3f ),
			new Vector3( 0f, 0.1f, 0f ), new Vector3( 0f, -0.1f, 0f ), new Vector3( 0f, 0.2f, 0.25f ), new Vector3( 0f, -0.2f, 0.25f )
		};
		var skeleton = new ClipSkeleton( names, parents, positions, names.Select( _ => Quaternion.Identity ).ToArray() );
		var pose = new SkeletonPose( skeleton.Count );
		pose.SetRest( skeleton );
		var (rest, _) = World( skeleton, pose );
		new BreathingLayer( skeleton ).Apply( pose, 1f );
		var (moved, _) = World( skeleton, pose );
		Assert.True( Vector3.Distance( moved[ 3 ], rest[ 3 ] ) > 0.003f );
		Assert.True( Vector3.Distance( moved[ 4 ], rest[ 4 ] ) < 1e-6f );

		// Without hips there is nothing to measure the body from: the pose is left alone.
		var bare = new ClipSkeleton( new[] { "root", "spine", "head" }, new[] { -1, 0, 1 },
			new[] { Vector3.Zero, new Vector3( 0f, 0f, 1f ), new Vector3( 0f, 0f, 0.5f ) },
			new[] { Quaternion.Identity, Quaternion.Identity, Quaternion.Identity } );
		var barePose = new SkeletonPose( bare.Count );
		barePose.SetRest( bare );
		new BreathingLayer( bare ).Apply( barePose, 1f );
		Assert.All( barePose.Rotations, rotation => Assert.Equal( Quaternion.Identity, rotation ) );
	}

	[Fact]
	public void BreathingAllocatesNothing()
	{
		var (skeleton, pose) = Rider();
		var layer = new BreathingLayer( skeleton );
		layer.Apply( pose, 1f );
		var before = GC.GetAllocatedBytesForCurrentThread();
		for ( var i = 0; i < 2000; i++ )
			layer.Apply( pose, 0.5f + (i % 10) * 0.1f );
		Assert.Equal( 0, GC.GetAllocatedBytesForCurrentThread() - before );
	}

	private static int Index( string name ) => Array.FindIndex( Bones, bone => bone.Name == name );

	private static (ClipSkeleton Skeleton, SkeletonPose Pose) Rider()
	{
		var skeleton = new ClipSkeleton( Bones.Select( bone => bone.Name ).ToArray(), Bones.Select( bone => bone.Parent ).ToArray(),
			Bones.Select( bone => bone.Position ).ToArray(), Bones.Select( _ => Quaternion.Identity ).ToArray() );
		var pose = new SkeletonPose( skeleton.Count );
		pose.SetRest( skeleton );
		pose.Rotations[ Index( "spine" ) ] = Quaternion.CreateFromAxisAngle( Vector3.UnitY, 0.17f );
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
