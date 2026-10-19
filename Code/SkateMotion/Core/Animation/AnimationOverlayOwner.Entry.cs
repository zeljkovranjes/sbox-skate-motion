using SkateMotion.Core.Input;

namespace SkateMotion.Core.Animation;

internal sealed partial class AnimationOverlayOwner
{
	private sealed class Entry : IAnimationChannelTreeFactory, IAnimationPosePlaybackNode,
		IAnimationAttributePlaybackNode, IAnimationEvaluatedAttributeNode
	{
		private readonly AnimationOverlayOwner _parent;
		internal readonly int Index;
		internal readonly ChildSlot Base = new();
		internal readonly AnimationChannelBlend Blend = new( default );
		internal AnimationChannelPosePlayback Node;
		internal AnimationChannelTreeOwner Owner;
		internal InputCommandKey Key;
		internal int Priority;
		internal Entry Previous;
		internal Entry Next;
		internal Entry HashNext;

		internal Entry( AnimationOverlayOwner parent, int index )
		{
			_parent = parent;
			Index = index;
		}

		internal void Initialize( IAnimationPlaybackTree tree )
		{
			if ( Owner is null )
				Owner = new AnimationChannelTreeOwner( tree, this, Blend, true );
			else
				Owner.InitializeReleased( tree );
			Node ??= new AnimationChannelPosePlayback( Base, this, Blend, true );
		}

		private IAnimationPosePlaybackNode Target => (IAnimationPosePlaybackNode)Owner.Root;
		public IAnimationPlaybackTree Create( AnimationChannelReplacement request ) => _parent.CreateTree( request );
		public void Commit( IAnimationPlaybackTree tree ) => _parent.CommitTree( tree );
		public void Release( IAnimationPlaybackTree tree ) => _parent.ReleaseTree( tree );
		public SkeletonPose Pose => Target.Pose;
		public float Duration => Target.Duration;
		public float Elapsed => Target.Elapsed;
		public bool Prepare( bool enabled, bool consumeHistory, uint flags ) => Target.Prepare( enabled, consumeHistory, flags );
		public void SamplePoseStreams( AnimationPoseStreamContext context ) => Target.SamplePoseStreams( context );
		public void Advance( float delta, float phase, ref AnimationPlaybackCompletion completion ) =>
			Target.Advance( delta, phase, ref completion );
		public void Seek( float time ) => Target.Seek( time );
		public void SampleEvaluatedAttributes( AnimationEvaluatedAttributeContext context,
			AnimationEvaluatedAttributeBuffer output, byte flags ) =>
			AnimationEvaluatedAttributeContext.Require( Target ).SampleEvaluatedAttributes( context, output, flags );
		public bool TryGetAuthoredAttribute( AnimationAttributeKey name, byte flags, ref AnimationAttributeQueryResult result ) =>
			((IAnimationAttributePlaybackNode)Target).TryGetAuthoredAttribute( name, flags, ref result );
	}

	private sealed class ChildSlot : IAnimationPosePlaybackNode, IAnimationAttributePlaybackNode, IAnimationEvaluatedAttributeNode
	{
		internal IAnimationPosePlaybackNode Target;
		public SkeletonPose Pose => Target.Pose;
		public float Duration => Target.Duration;
		public float Elapsed => Target.Elapsed;
		public bool Prepare( bool enabled, bool consumeHistory, uint flags ) => Target.Prepare( enabled, consumeHistory, flags );
		public void SamplePoseStreams( AnimationPoseStreamContext context ) => Target.SamplePoseStreams( context );
		public void Advance( float delta, float phase, ref AnimationPlaybackCompletion completion ) =>
			Target.Advance( delta, phase, ref completion );
		public void Seek( float time ) => Target.Seek( time );
		public void SampleEvaluatedAttributes( AnimationEvaluatedAttributeContext context,
			AnimationEvaluatedAttributeBuffer output, byte flags ) =>
			AnimationEvaluatedAttributeContext.Require( Target ).SampleEvaluatedAttributes( context, output, flags );
		public bool TryGetAuthoredAttribute( AnimationAttributeKey name, byte flags, ref AnimationAttributeQueryResult result ) =>
			((IAnimationAttributePlaybackNode)Target).TryGetAuthoredAttribute( name, flags, ref result );
	}
}
