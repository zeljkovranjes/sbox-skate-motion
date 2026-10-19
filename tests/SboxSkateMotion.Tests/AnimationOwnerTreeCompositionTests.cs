using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class AnimationOwnerTreeCompositionTests
{
	[Fact]
	public void OrderedReferencesAndDuplicatePostureRequestsPreservePersistentFlags()
	{
		var events = new List<string>();
		ulong flags = 0x12345678c0600000;
		var composition = new AnimationOwnerTreeComposition(
			() => Reference( "owner", 10f ), () => Reference( "before", 100f ),
			() => Reference( "after", 1000f ), () => new Provider( events ),
			() => flags, value => flags = value );
		var result = composition.Compose( Node( 1f ), true );
		Assert.Equal( new[] { "owner", "before", "after", "posture2", "posture1", "posture2" }, events );
		Assert.Equal( 0x12345678c0000000UL, flags );
		Assert.True( result.Prepare( true, false, 1 ) );
		Assert.Equal( 1111f, result.Pose.Positions[ 0 ].X );
		Assert.Null( composition.Compose( null, true ) );
		Assert.Equal( 6, events.Count );

		IAnimationPosePlaybackNode Reference( string name, float x )
		{
			events.Add( name );
			return Node( x );
		}
	}

	private static IAnimationPosePlaybackNode Node( float x ) => new AnimationReferencePosePlayback(
		new[] { new AnimationBoneTransform( new Vector4( x, 0f, 0f, 1f ), Quaternion.Identity, Vector4.One ) } );

	private sealed class Provider( List<string> events ) : IAnimationPostureTreeProvider
	{
		public IAnimationPosePlaybackNode Wrap( IAnimationPosePlaybackNode root, int mode )
		{
			events.Add( "posture" + mode );
			return root;
		}
	}
}
