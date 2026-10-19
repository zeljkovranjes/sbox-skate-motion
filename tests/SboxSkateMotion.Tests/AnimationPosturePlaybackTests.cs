using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class AnimationPosturePlaybackTests
{
	[Fact]
	public void NameMappingUsesFirstFullKeyAndPreservesMetadataAndUnusedPayload()
	{
		var first = new AnimationAttributeKey( 1, 2, 3, 4, 5 );
		var second = first with { E = 6 };
		var fallback = new AnimationAttributeKey( 9, 8, 7, 6, 5 );
		var map = new AnimationPostureAttributeMap( new[] { first, second, first },
			new[] { second, first, fallback }, fallback );
		var record = Record( 3, new( first.A, first.B, first.C, first.D, first.E, 0xabcdef01 ) );
		Assert.Equal( record with { Payload = new( 1, 2, 3, 4, 6, 0xabcdef01 ) }, map.Transform( record ) );
		Assert.Equal( record, map.Transform( map.Transform( record ) ) );
		var missing = record with { Payload = new( 1, 2, 3, 4, 7, 0xabcdef01 ) };
		Assert.Equal( new AnimationAttributePayload( 9, 8, 7, 6, 5, 0xabcdef01 ), map.Transform( missing ).Payload );
		var vector = Record( 1, new( 0xffc12345, 2, 3, 4, 5, 6 ) );
		Assert.Equal( vector with { Payload = vector.Payload with { A = 0x7fc12345 } }, map.Transform( vector ) );
		var other = vector with { Kind = 4 };
		Assert.Equal( other, map.Transform( other ) );
	}

	[Fact]
	public void WrapperRetainsChildReadinessClocksAndTransformsTheWholeAttributeList()
	{
		var child = new Child();
		var map = new AnimationPostureAttributeMap( Array.Empty<AnimationAttributeKey>(),
			Array.Empty<AnimationAttributeKey>(), default );
		var provider = new AnimationPostureTreeProvider( new( new uint[] { 1 }, new[] { 0 } ), map );
		var wrapper = (AnimationPosturePosePlayback)provider.Wrap( child, 2 );
		Assert.Same( child.Pose, wrapper.Pose );
		Assert.True( wrapper.Prepare( false, true, 17 ) );
		Assert.Equal( (false, true, 17u), child.Preparation );
		Assert.Equal( 3f, wrapper.Pose.Positions[ 0 ].Z );
		child.Ready = false;
		Assert.False( wrapper.Prepare( true, false, 5 ) );
		Assert.Equal( 3f, wrapper.Pose.Positions[ 0 ].Z );
		child.Ready = true;
		Assert.True( wrapper.Prepare( true, false, 5 ) );
		Assert.Equal( -1f, wrapper.Pose.Positions[ 0 ].X );
		Assert.Equal( 3f, wrapper.Pose.Positions[ 0 ].Z );
		wrapper.SetRate( 2f );
		wrapper.Seek( .25f );
		var completion = AnimationPlaybackCompletion.Empty;
		wrapper.Advance( .5f, .75f, ref completion );
		Assert.Equal( 2f, child.Rate );
		Assert.Equal( .75f, wrapper.Elapsed );
		Assert.Equal( child.Duration, wrapper.Duration );
		Assert.Equal( .75f, child.Phase );

		var streams = new AnimationPoseStreamContext( 4 );
		wrapper.SamplePoseStreams( streams );
		Assert.False( child.SawSwap );
		Assert.True( streams.SwapPairs );
		wrapper.SamplePoseStreams( streams );
		Assert.True( child.SawSwap );
		Assert.True( streams.SwapPairs );

		var initial = Record( 1, new( 0, 2, 3, 4, 5, 6 ) );
		var result = initial;
		child.Found = false;
		Assert.False( wrapper.TryGetAuthoredAttribute( default, 15, ref result ) );
		Assert.Equal( initial, result );
		child.Found = true;
		Assert.True( wrapper.TryGetAuthoredAttribute( default, 15, ref result ) );
		Assert.Equal( 0xc0000000u, result.Payload.A );
		var output = new AnimationEvaluatedAttributeBuffer( 2 );
		output.Append( initial );
		wrapper.SampleEvaluatedAttributes( new( 0, 0 ), output, 15 );
		Assert.Equal( 0x80000000u, output.Values[ 0 ].Payload.A );
		Assert.Equal( 0xc0000000u, output.Values[ 1 ].Payload.A );
	}

	private static AnimationAttributeQueryResult Record( sbyte kind, AnimationAttributePayload payload ) =>
		new( payload, new( 111, 222, 333, 444, 555 ), -1f, .5f, 0x5a, kind );

	private sealed class Child : IAnimationPosePlaybackNode, IAnimationRatePlaybackNode,
		IAnimationAttributePlaybackNode, IAnimationEvaluatedAttributeNode
	{
		internal bool Ready = true;
		internal bool Found = true;
		internal bool SawSwap;
		internal float Rate;
		internal float Phase;
		internal (bool, bool, uint) Preparation;
		public SkeletonPose Pose { get; } = new( 1 );
		public float Duration => 2f;
		public float Elapsed { get; private set; }

		internal Child() => Pose.SetTransforms( new[]
		{
			new AnimationBoneTransform( new Vector4( 1, 2, 3, 1 ), Quaternion.Identity, Vector4.One )
		} );

		public bool Prepare( bool enabled, bool consumeHistory, uint flags )
		{
			Preparation = (enabled, consumeHistory, flags);
			return Ready;
		}
		public void SamplePoseStreams( AnimationPoseStreamContext context )
		{
			SawSwap = context.SwapPairs;
			context.SwapPairs = false;
		}
		public void Advance( float delta, float phase, ref AnimationPlaybackCompletion completion )
		{
			Elapsed += delta;
			Phase = phase;
		}
		public void Seek( float time ) => Elapsed = time;
		public void SetRate( float rate ) => Rate = rate;
		public bool TryGetAuthoredAttribute( AnimationAttributeKey name, byte flags, ref AnimationAttributeQueryResult result )
		{
			if ( Found ) result = Record( 1, new( 0x40000000, 2, 3, 4, 5, 6 ) );
			return Found;
		}
		public void SampleEvaluatedAttributes( AnimationEvaluatedAttributeContext context,
			AnimationEvaluatedAttributeBuffer output, byte flags ) =>
			output.Append( Record( 1, new( 0x40000000, 2, 3, 4, 5, 6 ) ) );
	}
}
