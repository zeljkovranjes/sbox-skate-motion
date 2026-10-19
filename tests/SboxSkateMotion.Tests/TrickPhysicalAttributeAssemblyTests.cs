using System;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class TrickPhysicalAttributeAssemblyTests
{
	private static readonly AnimationAttributeKey Height = AnimationAttributeKey.Encode( "TRICKHEIGHT" );
	private static readonly AnimationAttributeKey Override = AnimationAttributeKey.Encode( "JUMPHEIGHTOVERRIDE" );
	private static readonly AnimationAttributeKey Jump = AnimationAttributeKey.Encode( "JUMP" );

	[Fact]
	public void HeightPublicationReachesThePhysicalJumpConsumer()
	{
		var sink = new TrickHeightOutputSink( new( 1, [] ), 2, _ => Height );
		sink.Publish( 0, new( .5f, .75f ) );
		var assembly = new PhysicalAnimationAttributeAssembly( 3 );
		var state = new PhysicalAnimationAttributeState();
		assembly.Update( state, Input(), sink.Queued, [ Scalar( Jump, 1f ), Scalar( Height, .25f ) ] );
		Assert.Equal( .75f, state.JumpStrength );
		Assert.True( state.HeightOverrideActive );
		Assert.Equal( 1, sink.QueuedCount );
		Assert.Equal( 1, sink.Attributes.Count );
	}

	[Fact]
	public void LaterSampledOverrideWinsAfterEveryQueuedDuplicate()
	{
		var assembly = new PhysicalAnimationAttributeAssembly( 5 );
		var state = new PhysicalAnimationAttributeState();
		assembly.Update( state, Input(), [ new( Override, .5f ), new( Override, .75f ) ],
			[ Scalar( Jump, 1f ), Scalar( Height, .1f ), Scalar( Override, .2f ) ] );
		Assert.Equal( 5, assembly.Count );
		Assert.Equal( .5f, assembly.Values[ 0 ].Value );
		Assert.Equal( .75f, assembly.Values[ 1 ].Value );
		Assert.Equal( .2f, state.JumpStrength );
	}

	[Fact]
	public void ReusingCapacityDoesNotReplayStaleTailAttributes()
	{
		var assembly = new PhysicalAnimationAttributeAssembly( 3 );
		var state = new PhysicalAnimationAttributeState();
		assembly.Update( state, Input(), [ new( Override, .75f ) ],
			[ Scalar( Jump, 1f ), Scalar( Height, .25f ) ] );
		assembly.Update( state, Input(), [], [] );
		Assert.Equal( 0, assembly.Count );
		Assert.False( state.HeightOverrideActive );
		Assert.Equal( 0f, state.HeightOverride );
	}

	[Fact]
	public void QueueEntriesBecomeScalarRecordsWithFixedFlags()
	{
		var assembly = new PhysicalAnimationAttributeAssembly( 1 );
		assembly.Gather( [ new( Override, .5f, 3, 12, Height ) ], [] );
		Assert.Equal( (sbyte)0, assembly.Values[ 0 ].Kind );
		Assert.Equal( (byte)6, assembly.Values[ 0 ].EventFlags );
		Assert.Equal( default, assembly.Values[ 0 ].Bone );
	}

	[Fact]
	public void BoneContactsKeepTheirEncodedBoneAndSixthPayloadWord()
	{
		var bone = AnimationAttributeKey.Encode( "ROOT" );
		var contact = new AnimationAttributeQueryResult(
			new( bone.A, bone.B, bone.C, bone.D, bone.E, 0x80000000 ),
			AnimationAttributeKey.Encode( "RIGHT_HAND_GRAB" ), -1f, -1f, 12, 3 );
		var assembly = new PhysicalAnimationAttributeAssembly( 1 );
		assembly.Gather( [], [ contact ] );
		Assert.Equal( bone, assembly.Values[ 0 ].Bone );
		Assert.Equal( 0x80000000u, BitConverter.SingleToUInt32Bits( assembly.Values[ 0 ].Value ) );
		Assert.Equal( (byte)12, assembly.Values[ 0 ].EventFlags );
	}

	[Fact]
	public void UnsupportedPayloadKindsRemainIgnoredByThePhysicalConsumer()
	{
		var sampled = Scalar( Override, 99f ) with { Kind = 2 };
		var assembly = new PhysicalAnimationAttributeAssembly( 3 );
		var state = new PhysicalAnimationAttributeState();
		assembly.Update( state, Input(), [], [ sampled, Scalar( Jump, 1f ), Scalar( Height, .25f ) ] );
		Assert.Equal( (sbyte)2, assembly.Values[ 0 ].Kind );
		Assert.False( state.HeightOverrideActive );
		Assert.Equal( .25f, state.JumpStrength );
	}

	[Fact]
	public void InsufficientCapacityPreservesThePreviousAssembly()
	{
		var assembly = new PhysicalAnimationAttributeAssembly( 1 );
		assembly.Gather( [ new( Override, .5f ) ], [] );
		Assert.Throws<InvalidOperationException>( () =>
			assembly.Gather( [ new( Override, .75f ) ], [ Scalar( Jump, 1f ) ] ) );
		Assert.Equal( 1, assembly.Count );
		Assert.Equal( .5f, assembly.Values[ 0 ].Value );
	}

	[Fact]
	public void DisabledHeightOverrideStillClearsTheRetainedRequest()
	{
		var assembly = new PhysicalAnimationAttributeAssembly( 3 );
		var state = new PhysicalAnimationAttributeState();
		assembly.Update( state, Input() with { AllowHeightOverride = false },
			[ new( Override, .75f ) ], [ Scalar( Jump, 1f ), Scalar( Height, .25f ) ] );
		Assert.Equal( .25f, state.JumpStrength );
		Assert.False( state.HeightOverrideActive );
	}

	private static AnimationAttributeQueryResult Scalar( AnimationAttributeKey key, float value ) =>
		new( new( BitConverter.SingleToUInt32Bits( value ), 0, 0, 0, 0, 0 ), key, -1f, -1f, 6, 0 );

	private static PhysicalAnimationAttributeInput Input() =>
		new( [], [], default, -1, 1f / 60f, false, default, false, false, .2f, .8f, true, false, 0 );
}
