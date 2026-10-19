using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class AnimationPostureProfileTests
{
	[Fact]
	public void LoadedProfileOwnsResolvedMappingsAndPreservesUnpairedBones()
	{
		var profile = Profile();
		var provider = profile.CreateProvider( Skeleton() );
		profile.PairedBones[ 0 ] = -1;
		profile.MappedAttributes[ 0 ][ 0 ] = 99;
		var child = new Child();
		child.Pose.Positions[ 0 ] = new( 1, 2, 3 );
		child.Pose.Positions[ 1 ] = new( 4, 5, 6 );
		var wrapper = (AnimationPosturePosePlayback)provider.Wrap( child, 2 );
		Assert.True( wrapper.Prepare( true, false, 0 ) );
		Assert.Equal( new Vector3( -1, 2, 3 ), child.Pose.Positions[ 0 ] );
		Assert.Equal( new Vector3( 4, 5, 6 ), child.Pose.Positions[ 1 ] );
		var value = new AnimationAttributeQueryResult( new( 1, 2, 3, 4, 5, 6 ), default, 0, 1, 0, 3 );
		Assert.True( wrapper.TryGetAuthoredAttribute( default, 0, ref value ) );
		Assert.Equal( new AnimationAttributePayload( 7, 8, 9, 10, 11, 6 ), value.Payload );
		value = value with { Payload = new( 0, 0, 0, 0, 0, 6 ) };
		Assert.True( wrapper.TryGetAuthoredAttribute( default, 0, ref value ) );
		Assert.Equal( new AnimationAttributePayload( 12, 13, 14, 15, 16, 6 ), value.Payload );
	}

	[Fact]
	public void RejectsDifferentSkeletonsAndIncompleteAttributeKeys()
	{
		var skeleton = Skeleton();
		var profile = Profile();
		profile.BoneNames[ 1 ] = "other";
		Assert.Throws<FormatException>( () => profile.CreateProvider( skeleton ) );
		profile = Profile();
		profile.Parents[ 1 ] = -1;
		Assert.Throws<FormatException>( () => profile.CreateProvider( skeleton ) );
		profile = Profile();
		profile.MappedAttributes[ 0 ] = new uint[ 4 ];
		Assert.Throws<FormatException>( () => profile.CreateProvider( skeleton ) );
		profile = Profile();
		profile.Version = 2;
		Assert.Throws<FormatException>( () => profile.CreateProvider( skeleton ) );
	}

	private static ClipSkeleton Skeleton() => new( new[] { "root", "child" }, new[] { -1, 0 },
		new Vector3[ 2 ], new[] { Quaternion.Identity, Quaternion.Identity } );

	private static AnimationPostureProfile Profile() => new()
	{
		Version = 1,
		BoneNames = new[] { "root", "child" },
		Parents = new[] { -1, 0 },
		BoneKinds = new uint[] { uint.MaxValue, 0 },
		PairedBones = new[] { 0, -1 },
		AttributeNames = new[] { new uint[] { 1, 2, 3, 4, 5 } },
		MappedAttributes = new[] { new uint[] { 7, 8, 9, 10, 11 } },
		MissingAttribute = new uint[] { 12, 13, 14, 15, 16 }
	};

	private sealed class Child : IAnimationPosePlaybackNode, IAnimationRatePlaybackNode,
		IAnimationAttributePlaybackNode, IAnimationEvaluatedAttributeNode
	{
		public SkeletonPose Pose { get; } = new( 2 );
		public float Duration => 1;
		public float Elapsed => 0;
		public bool Prepare( bool enabled, bool consumeHistory, uint flags ) => true;
		public void SamplePoseStreams( AnimationPoseStreamContext context ) { }
		public void Advance( float delta, float phase, ref AnimationPlaybackCompletion completion ) { }
		public void Seek( float time ) { }
		public void SetRate( float rate ) { }
		public bool TryGetAuthoredAttribute( AnimationAttributeKey name, byte flags,
			ref AnimationAttributeQueryResult result ) => true;
		public void SampleEvaluatedAttributes( AnimationEvaluatedAttributeContext context,
			AnimationEvaluatedAttributeBuffer output, byte flags ) { }
	}
}
