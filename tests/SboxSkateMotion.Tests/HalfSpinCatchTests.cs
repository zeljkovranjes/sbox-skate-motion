using System;
using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class HalfSpinCatchTests
{
	private const float Step = 1f / 60f;

	// Where the leg is as the spin is left, and where the clip after it holds it.
	private const float Caught = 24f;

	[Fact]
	public void LegsCaughtOffAHalfSpinStartTheNextClipFromRest()
	{
		// A shove-it's clip is left with its board 138 degrees round (172 at its end) and the leg swinging across at 30 a
		// second; the clip after it holds the leg just where it is. The board is taken over to its other end, and the legs,
		// which were following the board round, start the new clip from rest: the foot stays on the deck instead of swinging
		// on past it.
		var farthest = Farthest( 172f, legs: true );
		Assert.True( farthest < 0.2f, $"the leg swung {farthest:F2} past where the new clip holds it" );
	}

	[Fact]
	public void WithoutLegsTheSwingCarriesOn()
	{
		// The same change with no legs given: the leg carries the speed it had into the new clip, as every bone does.
		var farthest = Farthest( 172f, legs: false );
		Assert.True( farthest > 1f, $"the leg went only {farthest:F2} past where the new clip holds it" );
	}

	[Fact]
	public void LegsKeepTheirSpeedWhenTheBoardIsNotCaughtEndForEnd()
	{
		// A board 100 degrees across as the clip changes (125 at its end) is nowhere near its other end: the change is an
		// ordinary one, and the legs carry their speed through it as every other bone does.
		var farthest = Farthest( 125f, legs: true );
		Assert.True( farthest > 1f, $"the leg went only {farthest:F2} past where the new clip holds it" );
	}

	[Fact]
	public void OtherBonesKeepTheirSpeedThroughAHalfSpinCatch()
	{
		// Only the legs start from rest: an arm swinging across as the spin is caught carries its speed on.
		var farthest = Farthest( 172f, legs: true, legBone: -1 );
		Assert.True( farthest > 1f, $"the arm went only {farthest:F2} past where the new clip holds it" );
	}

	[Fact]
	public void TheLegsAreTheHipsAndEachLegToItsToes()
	{
		string[] names = [ "root", "hips", "spine", "left_upleg", "left_leg", "left_foot", "left_toe", "right_upleg", "right_leg",
			"right_foot", "board" ];
		int[] parents = [ -1, 0, 1, 1, 3, 4, 5, 1, 7, 8, 0 ];
		var skeleton = new ClipSkeleton( names, parents, new Vector3[ names.Length ], Rest( names.Length ) );
		var legs = StanceBones.Legs( skeleton );
		Assert.Equal( [ "hips", "left_toe", "left_foot", "left_leg", "left_upleg", "right_foot", "right_leg", "right_upleg" ],
			Array.ConvertAll( legs, bone => names[ bone ] ) );
	}

	[Fact]
	public void ASkeletonWithoutHipsHasNoLegs()
	{
		string[] names = [ "root", "left_foot", "board" ];
		var skeleton = new ClipSkeleton( names, [ -1, 0, 0 ], new Vector3[ names.Length ], Rest( names.Length ) );
		Assert.Empty( StanceBones.Legs( skeleton ) );
	}

	// Plays the spin, changes to the clip after it, and returns how far (at most) the swinging bone goes past where the new
	// clip holds it.
	private static float Farthest( float degrees, bool legs, int legBone = 1 )
	{
		var player = Player( degrees );
		player.Board = 0;
		if ( legs )
			player.Legs = legBone < 0 ? [] : [ legBone ];
		var pose = new SkeletonPose( 2 );
		var facts = new StateFacts();
		var parameters = new AnimationParameters();
		for ( var frame = 0; frame < 48; frame++ )
			player.Update( facts, parameters, Step, pose );
		facts.Intents[ "go" ] = 1f;
		var farthest = 0f;
		for ( var frame = 0; frame < 60; frame++ )
		{
			player.Update( facts, parameters, Step, pose );
			farthest = MathF.Max( farthest, pose.Positions[ 1 ].Y - Caught );
		}
		return farthest;
	}

	// A second-long spin turning the board to the given angle (0.8 of it as the change is made) while a second bone swings across at 30 a second (reaching
	// Caught 0.8 s in), then (on "go") a clip with the board ahead and the bone held at Caught, faded in over 0.2 s.
	private static StateGraphPlayer Player( float degrees )
	{
		var skeleton = new ClipSkeleton( [ "board", "leg" ], [ -1, -1 ], new Vector3[ 2 ], Rest( 2 ) );
		var turns = new Quaternion[ 32 ];
		var places = new Vector3[ 32 ];
		places[ 0 ] = Vector3.Zero;
		for ( var frame = 0; frame < 31; frame++ )
		{
			turns[ frame ] = Quaternion.CreateFromAxisAngle( Vector3.UnitZ, degrees * frame / 30f * MathF.PI / 180f );
			places[ frame + 1 ] = Vector3.UnitY * frame;
		}
		turns[ 31 ] = Quaternion.Identity;
		var spin = new AnimationClip( "spin", 30f, 31, [ 0, 31 ], [ 31, 1 ], turns, [ 0, 1 ], [ 1, 31 ], places ) { Looping = false };
		var after = new AnimationClip( "after", 30f, 2, [ 0, 1 ], [ 1, 1 ], [ Quaternion.Identity, Quaternion.Identity ], [ 0, 1 ],
			[ 1, 1 ], [ Vector3.Zero, Vector3.UnitY * Caught ] );
		var definition = new StateGraphDefinition
		{
			Name = "Root",
			States =
			{
				new()
				{
					Name = "Spin", Play = new() { Clip = "spin", BlendTime = 0f },
					Transitions = { new() { Target = "After", Guard = new() { Test = "intent", Intent = "go" } } }
				},
				new() { Name = "After", Active = false, Play = new() { Clip = "after", BlendTime = .2f } }
			}
		};
		return new StateGraphPlayer( new ClipSet( skeleton, [ spin, after ] ), definition );
	}

	private static Quaternion[] Rest( int count )
	{
		var rest = new Quaternion[ count ];
		Array.Fill( rest, Quaternion.Identity );
		return rest;
	}
}
