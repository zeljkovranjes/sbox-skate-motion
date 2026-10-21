using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class RiderBreathingTests
{
	private const float Frame = 1f / 60f;

	private static readonly RiderAnimationInput Riding = new() { Activity = RiderActivity.Riding, Speed = 3f, MoveSpeed = 3f };

	[Fact]
	public void BreathesOnlyRidingOnTheGroundWithTheHandsFree()
	{
		Assert.True( RiderBreathing.Shown( Riding, false ) );
		Assert.True( RiderBreathing.Shown( Riding with { Speed = 0f, MoveSpeed = 0f }, false ) );
		Assert.True( RiderBreathing.Shown( Riding with { Steer = 1f, Pushing = true, Balance = 1 }, false ) );
		Assert.False( RiderBreathing.Shown( Riding, true ) );
		foreach ( var input in new[]
		{
			Riding with { Airborne = true }, Riding with { Grind = "fiftyfifty" }, Riding with { Grab = "mutegrab" },
			Riding with { Handplant = "cycle" }, Riding with { HoldingBoard = true }, Riding with { OffBoard = true },
			Riding with { WallRiding = true }, Riding with { Skitching = true },
			Riding with { Activity = RiderActivity.Walking }, Riding with { Activity = RiderActivity.Carrying },
			Riding with { Activity = RiderActivity.Bailed }, Riding with { Activity = RiderActivity.Mounting }
		} )
			Assert.False( RiderBreathing.Shown( input, false ), input.ToString() );
	}

	[Fact]
	public void RollingWithNoInputBreathesAtTheRestingPace()
	{
		var breathing = new RiderBreathing( Skeleton() );
		for ( var time = 0f; time < 5f; time += Frame )
			breathing.Update( Riding, false, Frame );
		Assert.Equal( 1f, breathing.Rhythm.Weight );
		Assert.Equal( BreathingRhythm.RestingRate, breathing.Rhythm.Rate, 4 );
	}

	[Fact]
	public void PushingWindsTheRiderMoreTheLongerItGoesOn()
	{
		var brief = Winded( 0.3f );
		var longer = Winded( 3f );
		Assert.True( brief > 0.2f && longer > brief + 0.2f, $"{brief} then {longer}" );
		Assert.True( longer <= RiderBreathing.LongPush );

		static float Winded( float seconds )
		{
			var breathing = new RiderBreathing( Skeleton() );
			for ( var time = 0f; time < seconds; time += Frame )
				breathing.Update( Riding with { Pushing = true }, false, Frame );
			for ( var time = 0f; time < 0.5f; time += Frame )
				breathing.Update( Riding, false, Frame );
			return breathing.Rhythm.Exertion;
		}
	}

	[Fact]
	public void AHarderLandingWindsTheRiderMoreAndItSettles()
	{
		var light = Landing( 1.5f, out _ );
		var heavy = Landing( 7f, out var settled );
		Assert.True( light > 0.15f && heavy > light + 0.25f, $"{light} then {heavy}" );
		Assert.True( settled < 0.05f );

		static float Landing( float fall, out float later )
		{
			var breathing = new RiderBreathing( Skeleton() );
			for ( var time = 0f; time < 2f; time += Frame )
				breathing.Update( Riding, false, Frame );
			for ( var time = 0f; time < 0.6f; time += Frame )
				breathing.Update( Riding with { Airborne = true, VerticalSpeed = -fall * time / 0.6f }, false, Frame );
			for ( var time = 0f; time < 1f; time += Frame )
				breathing.Update( Riding with { TimeSinceLanding = time }, false, Frame );
			var winded = breathing.Rhythm.Exertion;
			for ( var time = 0f; time < 12f; time += Frame )
				breathing.Update( Riding, false, Frame );
			later = breathing.Rhythm.Exertion;
			return winded;
		}
	}

	[Fact]
	public void ABailLeavesTheRiderWindedAfterGettingBackOn()
	{
		var breathing = new RiderBreathing( Skeleton() );
		for ( var time = 0f; time < 2f; time += Frame )
			breathing.Update( Riding with { Activity = RiderActivity.Bailed }, false, Frame );
		Assert.Equal( 0f, breathing.Rhythm.Weight );
		for ( var time = 0f; time < 1.5f; time += Frame )
			breathing.Update( Riding with { Activity = RiderActivity.Walking }, false, Frame );
		for ( var time = 0f; time < 1.5f; time += Frame )
			breathing.Update( Riding, false, Frame );
		Assert.True( breathing.Rhythm.Weight > 0.99f );
		Assert.True( breathing.Rhythm.Exertion > 0.3f && breathing.Rhythm.Rate > BreathingRhythm.RestingRate + 0.08f );
	}

	[Fact]
	public void TheBreathEasesOutForAnOllieAndBackInAfterTheLanding()
	{
		var breathing = new RiderBreathing( Skeleton() );
		var previous = 0f;
		var largest = 0f;
		void Run( RiderAnimationInput input, float seconds )
		{
			for ( var time = 0f; time < seconds; time += Frame )
			{
				breathing.Update( input, false, Frame );
				largest = MathF.Max( largest, MathF.Abs( breathing.Rhythm.Inhale - previous ) );
				previous = breathing.Rhythm.Inhale;
			}
		}
		Run( Riding, 3f );
		Run( Riding with { Airborne = true, Trick = "ollie", VerticalSpeed = -3f }, 0.6f );
		Assert.Equal( 0f, breathing.Rhythm.Weight );
		Run( Riding, 2f );
		Assert.Equal( 1f, breathing.Rhythm.Weight );
		Assert.True( largest < 0.15f, $"largest step {largest}" );
	}

	[Fact]
	public void TheBreathLeavesThePoseAloneWhileItDoesNotShow()
	{
		var skeleton = Skeleton();
		var breathing = new RiderBreathing( skeleton );
		var pose = new SkeletonPose( skeleton.Count );
		pose.SetRest( skeleton );
		breathing.Update( Riding with { Airborne = true }, false, Frame );
		breathing.Apply( pose );
		Assert.All( pose.Rotations, rotation => Assert.Equal( Quaternion.Identity, rotation ) );
	}

	private static ClipSkeleton Skeleton()
	{
		var names = new[] { "root", "hips", "spine", "spine3", "neck", "head", "left_shoulder", "left_arm", "right_shoulder",
			"right_arm", "left_upleg", "right_upleg" };
		var parents = new[] { -1, 0, 1, 2, 3, 4, 3, 6, 3, 8, 1, 1 };
		var positions = new[]
		{
			Vector3.Zero, new Vector3( 0f, 0f, 1f ), new Vector3( 0f, 0f, 0.1f ), new Vector3( 0f, 0f, 0.3f ),
			new Vector3( 0f, 0f, 0.15f ), new Vector3( 0f, 0f, 0.1f ), new Vector3( 0f, 0.03f, 0.12f ), new Vector3( 0f, 0.15f, 0f ),
			new Vector3( 0f, -0.03f, 0.12f ), new Vector3( 0f, -0.15f, 0f ), new Vector3( 0f, 0.1f, 0f ), new Vector3( 0f, -0.1f, 0f )
		};
		return new ClipSkeleton( names, parents, positions, names.Select( _ => Quaternion.Identity ).ToArray() );
	}
}
