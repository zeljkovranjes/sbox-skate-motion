using SkateMotion.Core.Animation;
using System.Numerics;
using Xunit;

namespace SboxSkateMotion.Tests;

public sealed class AnimationRidingFakieTests
{
	private static readonly AnimationRidingFakieObservation Riding = new(
		1, 0, Vector3.UnitX, -Vector3.UnitX, -Vector3.UnitX, 2f );

	[Fact]
	public void TeleportUsesPreviousTimerAndTrickRetainsTheSharedOwnerFlag()
	{
		var instance = new AnimationRidingFakieInstance();
		var gameplay = new AnimationGameplayFlags( 0 );
		var definition = new AnimationRidingFakieDefinition( 1f, .5f, .2f, 1f );
		var writes = new List<bool>();
		instance.Update( definition, Riding with { MovementState = 5 }, .1f, gameplay, writes.Add );
		instance.Update( definition, Riding, 1f, gameplay, writes.Add );
		instance.Update( definition, Riding, .125f, gameplay, writes.Add );
		Assert.Equal( new[] { false, false, false }, writes );
		instance.Update( definition, Riding, .1f, gameplay, writes.Add );
		Assert.True( writes[ 3 ] );
		gameplay.Enter( AnimationGameplayFlag.Trick );
		instance.Update( definition, Riding, .1f, gameplay, writes.Add );
		instance.Update( definition, Riding with { MovementState = 2 }, .1f, gameplay, writes.Add );
		Assert.Equal( 4, writes.Count );
		Assert.Equal( 0x3fb66667, BitConverter.SingleToInt32Bits( instance.TimeSinceTeleport ) );
		Assert.Equal( 0f, instance.ConfirmationTime );
		instance.Update( definition, Riding with { MovementState = 4 }, .1f, gameplay, writes.Add );
		Assert.False( writes[ 4 ] );
		instance.Update( definition, Riding with { MovementState = 5 }, .1f, gameplay, writes.Add );
		Assert.Equal( 0f, instance.TimeSinceTeleport );
	}

	[Fact]
	public void SlowConfirmationSurvivesLifecycleAndUsesTheSelectedPhysicalDirection()
	{
		var instance = new AnimationRidingFakieInstance();
		var gameplay = new AnimationGameplayFlags( 0 );
		var definition = new AnimationRidingFakieDefinition( 1f, .5f, .2f, 0f );
		var writes = new List<bool>();
		var slow = Riding with { Speed = .75f };
		for ( var i = 0; i < 3; i++ )
			instance.Update( definition, slow, .1f, gameplay, writes.Add );
		Assert.Equal( .2f, instance.ConfirmationTime );
		Assert.Equal( new[] { false, false, false }, writes );
		instance.Exit();
		instance.Enter();
		instance.Update( definition, slow, .1f, gameplay, writes.Add );
		Assert.True( writes[ 3 ] );
		instance.Update( definition, Riding with { MovementState = 2, BoardDirection = Vector3.UnitX, Speed = 1f },
			.1f, gameplay, writes.Add );
		Assert.True( writes[ 4 ] );
		Assert.Equal( .4f, instance.ConfirmationTime );
		instance.Update( definition, Riding with { MovementState = 6, ControllerCode = 0x1f7 },
			.1f, gameplay, writes.Add );
		Assert.True( writes[ 5 ] );
		gameplay.Enter( AnimationGameplayFlag.Trick );
		instance.Update( definition, Riding with { MovementState = 6, ControllerCode = 0x1f7 },
			.1f, gameplay, writes.Add );
		Assert.False( writes[ 6 ] );
		gameplay.Exit( AnimationGameplayFlag.Trick );
		instance.Update( definition, Riding with { ReferenceDirection = Vector3.UnitX * .5f },
			.1f, gameplay, writes.Add );
		Assert.False( writes[ 7 ] );
	}
}
