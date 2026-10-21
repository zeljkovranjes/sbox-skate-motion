using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class RiderBalanceTests
{
	private const float Frame = 1f / 60f;

	private static readonly RiderAnimationInput Sliding = new()
	{
		Activity = RiderActivity.Riding, Speed = 3f, MoveSpeed = 3f, Grind = "tailslide", Airborne = true
	};

	[Fact]
	public void OnARailWithTheHandsFreeTheRiderBreathesAndBalances()
	{
		Assert.True( RiderBreathing.OnRail( Sliding, false ) );
		Assert.True( RiderBreathing.OnRail( Sliding with { Airborne = false, Grind = "fiftyfifty" }, false ) );
		Assert.False( RiderBreathing.OnRail( Sliding, true ) );
		foreach ( var input in new[]
		{
			Sliding with { Grind = null }, Sliding with { Grab = "mutegrab" }, Sliding with { Handplant = "cycle" },
			Sliding with { HoldingBoard = true }, Sliding with { OffBoard = true }, Sliding with { Skitching = true },
			Sliding with { Activity = RiderActivity.Walking }, Sliding with { Activity = RiderActivity.Bailed }
		} )
			Assert.False( RiderBreathing.OnRail( input, false ), input.ToString() );

		// The breath shows on the rail although the board counts as off the ground there.
		var (skeleton, pose) = Rider();
		var breathing = new RiderBreathing( skeleton );
		for ( var time = 0f; time < 2f; time += Frame )
		{
			breathing.Update( Sliding, false, Frame );
			breathing.Apply( pose );
			pose.SetRest( skeleton );
		}
		Assert.Equal( 1f, breathing.Rhythm.Weight );
		Assert.True( breathing.Balance.Weight > 0.99f );
	}

	[Fact]
	public void AStillSlideBalancesAndAMovingGrindOnlyBreathes()
	{
		Assert.True( Balance( 0f ) > 0.99f );
		Assert.True( Balance( 25f ) < 0.01f );

		static float Balance( float degreesPerSecond )
		{
			var (skeleton, pose) = Rider();
			var breathing = new RiderBreathing( skeleton );
			var spine = skeleton.IndexOf( "spine1" );
			for ( var frame = 0; frame < 60 * 3; frame++ )
			{
				pose.SetRest( skeleton );
				pose.Rotations[ spine ] = Quaternion.CreateFromAxisAngle( Vector3.UnitY,
					MathF.Sin( frame * Frame * 2f ) * degreesPerSecond / 2f * MathF.PI / 180f );
				breathing.Update( Sliding, false, Frame );
				breathing.Apply( pose );
			}
			return breathing.Balance.Weight;
		}
	}

	[Fact]
	public void TheFeetAndTheBoardNeverMoveOnARail()
	{
		var (skeleton, pose) = Rider();
		var breathing = new RiderBreathing( skeleton );
		var (rest, restTurns) = World( skeleton, pose );
		var moved = 0f;
		for ( var frame = 0; frame < 60 * 6; frame++ )
		{
			pose.SetRest( skeleton );
			breathing.Rhythm.Exert( frame % 120 == 0 ? 1f : 0f );
			breathing.Update( Sliding, false, Frame );
			breathing.Apply( pose );
			var (positions, turns) = World( skeleton, pose );
			foreach ( var name in new[] { "hips", "left_leg", "left_foot", "right_leg", "right_foot", "board" } )
			{
				var bone = skeleton.IndexOf( name );
				Assert.True( Vector3.Distance( positions[ bone ], rest[ bone ] ) < 1e-6f, name );
				Assert.True( Quaternion.Dot( turns[ bone ], restTurns[ bone ] ) > 0.999999f, name );
			}
			moved = MathF.Max( moved, Vector3.Distance( positions[ skeleton.IndexOf( "spine3" ) ], rest[ skeleton.IndexOf( "spine3" ) ] ) );
		}
		// The chest does move: a centimetre or so with the sway and the breath.
		Assert.InRange( moved, 0.004f, 0.03f );
	}

	[Fact]
	public void LeavingTheRailEasesTheSwayOut()
	{
		var (skeleton, pose) = Rider();
		var breathing = new RiderBreathing( skeleton );
		for ( var time = 0f; time < 2f; time += Frame )
		{
			// Each frame starts from the clip's own pose, as the animation's does.
			pose.SetRest( skeleton );
			breathing.Update( Sliding, false, Frame );
			breathing.Apply( pose );
		}
		Assert.True( breathing.Balance.Weight > 0.99f );
		var airborne = Sliding with { Grind = null };
		breathing.Update( airborne, false, Frame );
		Assert.True( breathing.Balance.Weight > 0.9f );
		for ( var time = 0f; time < GrindBalance.EaseOut; time += Frame )
			breathing.Update( airborne, false, Frame );
		Assert.Equal( 0f, breathing.Balance.Weight );
	}

	private static (ClipSkeleton Skeleton, SkeletonPose Pose) Rider()
	{
		var names = new[] { "root", "hips", "spine", "spine1", "spine3", "neck", "head", "left_shoulder", "left_arm", "right_shoulder",
			"right_arm", "left_upleg", "left_leg", "left_foot", "right_upleg", "right_leg", "right_foot", "board" };
		var parents = new[] { -1, 0, 1, 2, 3, 4, 5, 4, 7, 4, 9, 1, 11, 12, 1, 14, 15, 0 };
		var positions = new[]
		{
			Vector3.Zero, new Vector3( 0f, 0f, 1f ), new Vector3( 0f, 0f, 0.1f ), new Vector3( 0f, 0f, 0.15f ), new Vector3( 0f, 0f, 0.15f ),
			new Vector3( 0f, 0f, 0.15f ), new Vector3( 0f, 0f, 0.1f ), new Vector3( 0f, 0.03f, 0.12f ), new Vector3( 0f, 0.15f, 0f ),
			new Vector3( 0f, -0.03f, 0.12f ), new Vector3( 0f, -0.15f, 0f ), new Vector3( 0f, 0.1f, 0f ), new Vector3( 0f, 0f, -0.45f ),
			new Vector3( 0f, 0f, -0.45f ), new Vector3( 0f, -0.1f, 0f ), new Vector3( 0f, 0f, -0.45f ), new Vector3( 0f, 0f, -0.45f ),
			new Vector3( 0f, 0f, 0.08f )
		};
		var skeleton = new ClipSkeleton( names, parents, positions, names.Select( _ => Quaternion.Identity ).ToArray() );
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
}
